''' <summary>
''' Rewind system for game scenarios (event-driven, thread-safe singleton).
''' </summary>
''' <typeparam name="T">Save data type.</typeparam>
Public NotInheritable Class GameRewindSystem(Of T)
    Private Shared ReadOnly _instance As New Lazy(Of GameRewindSystem(Of T))(
        Function() New GameRewindSystem(Of T),
        Threading.LazyThreadSafetyMode.ExecutionAndPublication
    )

    ''' <summary>
    ''' Singleton instance of the rewind system.
    ''' </summary>
    Public Shared ReadOnly Property Instance As GameRewindSystem(Of T)
        Get
            Return _instance.Value
        End Get
    End Property

    Private Sub New()
    End Sub

    ' Rewindable item queue
    Private ReadOnly _rewindables As New List(Of IRewindable(Of T))
    Private ReadOnly _lock As New Object
    ' Executed items list (used for rewind operation)
    Private ReadOnly _executed As New List(Of (rw As IRewindable(Of T), done As Boolean))

    ''' <summary>
    ''' Event raised when a rewind request is triggered.
    ''' </summary>
    ''' <param name="reason">Reason for rewind request.</param>
    ''' <param name="message">Message for rewind request.</param>
    Public Event RewindRequested(reason As RewindReason, message As String)

    ''' <summary>
    ''' Register a rewindable operation for execution.
    ''' </summary>
    Public Sub RegisterRewindable(rewindable As IRewindable(Of T))
        ArgumentNullException.ThrowIfNull(rewindable)
        SyncLock _lock
            _rewindables.Add(rewindable)
        End SyncLock
    End Sub

    ''' <summary>
    ''' Execute all pending rewindables in batch (no exceptions, just normal progression).
    ''' </summary>
    Public Sub ExecuteBatch(Optional logAction As Action(Of String) = Nothing)
        SyncLock _lock
            If _rewindables.Count = 0 Then
                logAction?.Invoke("No pending operations to execute")
                Exit Sub
            End If
            ' Transfer to executed items list and clear the queue
            _executed.Clear()
            _rewindables.ForEach(Sub(rw) _executed.Add((rw, False)))
            _rewindables.Clear()
        End SyncLock

        ' Execute all rewindable operations in batch
        For i As Integer = 0 To _executed.Count - 1
            Dim itm = _executed(i)
            itm.rw.Execute()
            logAction?.Invoke($"Operation executed: {itm.rw.GetType().Name}")
            _executed(i) = (itm.rw, True)
        Next i
        logAction?.Invoke($"All {_executed.Count} operations executed successfully")
    End Sub

    ''' <summary>
    ''' Actively trigger rewind (called by game flow).
    ''' </summary>
    ''' <param name="reason">Reason for rewind.</param>
    ''' <param name="message">Optional message for logging.</param>
    ''' <param name="logAction">Optional logging action.</param>
    Public Sub TriggerRewind(reason As RewindReason, Optional message As String = Nothing,
            Optional logAction As Action(Of String) = Nothing)

        message = If(message, $"Rewind triggered for '{reason}'")
        RaiseEvent RewindRequested(reason, message)
        For Each itm In _executed.AsEnumerable().Reverse()
            If Not itm.done Then Continue For
            Try
                itm.rw.Rewind(reason, message)
                logAction?.Invoke($"Rewind completed: {itm.rw.GetType().Name} | Message: {message}")
            Catch ex As Exception
                logAction?.Invoke($"Rewind failed: {ex.Message}")
            End Try
        Next itm
        _executed.Clear()
    End Sub

    ''' <summary>
    ''' Get current number (integer count) of pending rewindables.
    ''' </summary>
    ''' <returns>Integer count of pending rewindables.</returns>
    Public ReadOnly Property PendingRewindableCount As Integer
        Get
            SyncLock _lock
                Return _rewindables.Count
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' Clear all pending rewindables.
    ''' </summary>
    Public Sub ClearRewindables()
        SyncLock _lock
            _rewindables.Clear()
        End SyncLock
    End Sub
End Class