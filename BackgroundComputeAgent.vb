Imports System.Threading

''' <summary>
''' Executes parallel computations in a background thread and safely exchanges data with the 
''' main thread using double buffering.
''' </summary>
Public NotInheritable Class BackgroundComputeAgent(Of T As Structure)
    Implements IDisposable
    
    Private ReadOnly _targetIntervalMs As Integer
    Private ReadOnly _dataLength As Integer
    Private ReadOnly _computeFunc As Func(Of Integer, T)   ' Delegate for computing a single element
    Private ReadOnly _onDataReady As Action(Of T())        ' Callback when data is ready (executed on main thread)

    Private _bufferCurrent As T()      ' Current buffer for rendering (read by main thread)
    Private _bufferBackground As T()   ' Background buffer for computation (written by child thread)

    ' Use an integer as an atomic flag for Interlocked operations 
    Private _swapFlag As Integer    ' 0 = no swap pending, 1 = swap pending
    Private ReadOnly _cts As New CancellationTokenSource
    Private _workerTask As Task
    Private disposedValue As Boolean

    ''' <summary>
    ''' Initializes a new instance of the <see cref="BackgroundComputeAgent(Of T)"/> class.
    ''' </summary>
    ''' <param name="dataLength">Length of the data array</param>
    ''' <param name="computeFunc">Delegate for computing a single element (index => new value)</param>
    ''' <param name="onDataReady">Callback when data is ready (executed on main thread)</param>
    ''' <param name="targetIntervalMs">Target computation interval (in milliseconds), default 33ms (≈ 30fps).</param>
    Public Sub New(dataLength As Integer, computeFunc As Func(Of Integer, T),
                   onDataReady As Action(Of T()), Optional targetIntervalMs As Integer = 33)
        If dataLength <= 0 Then 
            Throw New ArgumentException("dataLength must be greater than 0", NameOf(dataLength))
        End If
        ArgumentNullException.ThrowIfNull(computeFunc)
        ArgumentNullException.ThrowIfNull(onDataReady)

        _dataLength = dataLength
        _computeFunc = computeFunc
        _onDataReady = onDataReady
        _targetIntervalMs = targetIntervalMs

        ' Initialize double buffers
        _bufferCurrent = New T(_dataLength - 1) {}
        _bufferBackground = New T(_dataLength - 1) {}
    End Sub

    ''' <summary>
    ''' Starts background computation. No effect if already running.
    ''' </summary>
    ''' <remarks>
    ''' Call this method during the "Initialize" phase of your game app.
    ''' </remarks>
    Public Sub BeginCompute()
        If _workerTask IsNot Nothing AndAlso Not _workerTask.IsCompleted Then Exit Sub
        _workerTask = Task.Run(AddressOf WorkerLoop, _cts.Token)
    End Sub

    ''' <summary>
    ''' Stops background computation.
    ''' </summary>
    ''' <remarks>
    ''' Call this method during the "UnloadContent" or "Dispose" phases of your game app.
    ''' </remarks>
    Public Sub EndCompute()
        If Not _cts.IsCancellationRequested Then _cts.Cancel()
    End Sub

    ''' <summary>
    ''' Waits for the background task to fully stop. Returns False if timeout.
    ''' </summary>
    ''' <param name="timeoutMs">Timeout in milliseconds</param>
    ''' <returns>True if the task stopped within the timeout, False otherwise</returns>
    Public Function WaitForStop(Optional timeoutMs As Integer = 3000) As Boolean
        If _workerTask Is Nothing OrElse _workerTask.IsCompleted Then Return True

        Try
            Return _workerTask.Wait(timeoutMs)
        Catch ex As AggregateException
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Current buffer data, read by main thread.
    ''' </summary>
    ''' <returns>Array of computed values</returns>
    Public ReadOnly Property CurrentBufferData As T()
        Get
            Return _bufferCurrent
        End Get
    End Property

    ''' <summary>
    ''' Main thread call: Checks and triggers data exchange.
    ''' </summary>
    ''' <remarks>
    ''' Call this method during the main thread's "Update" phase.
    ''' </remarks>
    ''' <returns>Whether a data exchange occurred</returns>
    Public Function TrySwap() As Boolean
        ' Atomically check-and-reset swap flag (0 = no swap, 1 = swap requested)
        If Interlocked.CompareExchange(_swapFlag, 0, 1) = 1 Then
            ' Swap double buffer pointers (atomic operation)
            Dim temp = _bufferCurrent
            _bufferCurrent = _bufferBackground
            _bufferBackground = temp

            ' Trigger main thread callback
            _onDataReady?.Invoke(_bufferCurrent)
            Return True
        End If
        Return False
    End Function

    ''' <summary>
    ''' Background worker loop that runs in child thread.
    ''' </summary>
    ''' <remarks>
    ''' This method is called by the background task to perform the actual computation.
    ''' </remarks>
    ''' <returns>Task that represents the asynchronous operation</returns>
    ''' <exception cref="OperationCanceledException">If the task is canceled</exception>
    Private Async Function WorkerLoop() As Task
        Do Until _cts.Token.IsCancellationRequested
            Dim startTime = Stopwatch.GetTimestamp()
            Try
                Parallel.For(0, _dataLength, Sub(i) _bufferBackground(i) = _computeFunc(i))
            Catch ex As OperationCanceledException
                Exit Do
            End Try
            ' Signal that a swap is pending (set to 1)
            Interlocked.Exchange(_swapFlag, 1)

            ' Calculate elapsed time for this iteration
            Dim elapsedMs = (Stopwatch.GetTimestamp() - startTime) * 1000.0 / Stopwatch.Frequency
            ' Dynamic throttling - yield CPU if computation is faster than target interval.
            If elapsedMs < _targetIntervalMs Then
                Dim delayMs = CInt(_targetIntervalMs - elapsedMs)
                Try
                    Await Task.Delay(delayMs, _cts.Token)
                Catch ex As Exception When TypeOf ex Is TaskCanceledException OrElse
                        TypeOf ex Is OperationCanceledException
                    Exit Do  ' Normal cancellation, exit loop
                End Try
            End If
        Loop
    End Function

    ''' <summary>
    ''' Disposes of the background compute agent.
    ''' </summary>
    Public Sub Dispose() Implements IDisposable.Dispose
        Dispose(True)
        GC.SuppressFinalize(Me)
    End Sub

    Private Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                EndCompute()
                WaitForStop(3000)
                _cts?.Dispose()
                If _workerTask IsNot Nothing Then _workerTask = Nothing
            End If

            disposedValue = True
        End If
    End Sub
End Class