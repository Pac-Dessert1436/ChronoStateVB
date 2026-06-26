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

    Private ReadOnly _rewindables As New List(Of IRewindable(Of T))
    Private ReadOnly _lock As New Object
    Private ReadOnly _executed As New List(Of HistoryEntry)

    ''' <summary>
    ''' Represents an entry in the execution history.
    ''' </summary>
    Private Structure HistoryEntry
        ''' <summary>
        ''' The rewindable operation.
        ''' </summary>
        Public ReadOnly Rewindable As IRewindable(Of T)
        ''' <summary>
        ''' Whether the operation was successfully executed.
        ''' </summary>
        Public ReadOnly IsExecuted As Boolean

        ''' <summary>
        ''' Initializes a new instance of the <see cref="HistoryEntry"/> structure.
        ''' </summary>
        ''' <param name="rewindable">The rewindable operation.</param>
        ''' <param name="isExecuted">Whether the operation was successfully executed.</param>
        Public Sub New(rewindable As IRewindable(Of T), isExecuted As Boolean)
            Me.Rewindable = rewindable
            Me.IsExecuted = isExecuted
        End Sub
    End Structure

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
    ''' Unregister a previously registered rewindable operation.
    ''' </summary>
    Public Sub UnregisterRewindable(rewindable As IRewindable(Of T))
        SyncLock _lock
            _rewindables.Remove(rewindable)
        End SyncLock
    End Sub

    ''' <summary>
    ''' Execute all pending rewindables in batch. Executed items are accumulated
    ''' in history so that multiple batches can be rewound together.
    ''' </summary>
    ''' <param name="logAction">Optional logging action for execution status.</param>
    Public Sub ExecuteBatch(Optional logAction As Action(Of String) = Nothing)
        Dim batch As List(Of HistoryEntry) = Nothing

        SyncLock _lock
            If _rewindables.Count = 0 Then
                logAction?.Invoke("No pending operations to execute")
                Exit Sub
            End If
            batch = Aggregate rw In _rewindables
                        Select New HistoryEntry(rw, False) Into ToList()
            _rewindables.Clear()
        End SyncLock

        For i As Integer = 0 To batch.Count - 1
            Dim entry = batch(i)
            Dim rwTypeName = entry.Rewindable.GetType().Name
            Try
                entry.Rewindable.Execute()
                batch(i) = New HistoryEntry(entry.Rewindable, True)
                logAction?.Invoke($"Operation executed: {rwTypeName}")
            Catch ex As Exception
                batch(i) = New HistoryEntry(entry.Rewindable, False)
                logAction?.Invoke($"Operation failed: {rwTypeName} - {ex.Message}")
            End Try
        Next i

        SyncLock _lock
            _executed.AddRange(batch)
        End SyncLock

        logAction?.Invoke($"Batch of {batch.Count} operations processed")
    End Sub

    ''' <summary>
    ''' Actively trigger rewind (called by game flow).
    ''' Rewinds all executed operations in reverse order and clears history.
    ''' </summary>
    ''' <param name="reason">Reason for rewind.</param>
    ''' <param name="message">Optional message for logging.</param>
    ''' <param name="logAction">Optional logging action for execution status.</param>
    Public Sub TriggerRewind(reason As RewindReason, Optional message As String = Nothing,
            Optional logAction As Action(Of String) = Nothing)

        message = If(message, $"Rewind triggered for '{reason}'")
        RaiseEvent RewindRequested(reason, message)

        Dim toRewind As List(Of HistoryEntry)

        SyncLock _lock
            toRewind = _executed.ToList()
            _executed.Clear()
        End SyncLock

        For Each entry In Enumerable.Reverse(toRewind)
            Dim rwTypeName = entry.Rewindable.GetType().Name
            If Not entry.IsExecuted Then Continue For
            Try
                entry.Rewindable.Rewind(reason, message)
                logAction?.Invoke($"Rewind completed: {rwTypeName} | Message: {message}")
            Catch ex As Exception
                logAction?.Invoke($"Rewind failed: {rwTypeName} - {ex.Message}")
            End Try
        Next entry
    End Sub

    ''' <summary>
    ''' Get current number of pending rewindables.
    ''' </summary>
    Public ReadOnly Property PendingRewindableCount As Integer
        Get
            SyncLock _lock
                Return _rewindables.Count
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' Get current number of executed items in history.
    ''' </summary>
    Public ReadOnly Property ExecutedCount As Integer
        Get
            SyncLock _lock
                Return _executed.Count
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

    ''' <summary>
    ''' Clear all executed item history (prevents rewinding cleared items).
    ''' </summary>
    Public Sub ClearExecutedHistory()
        SyncLock _lock
            _executed.Clear()
        End SyncLock
    End Sub
End Class