''' <summary>
''' Rewindable object interface for game scenarios.
''' </summary>
''' <typeparam name="T">Save data type.</typeparam>
Public Interface IRewindable(Of T)
    ''' <summary>
    ''' Checkpoint state before execution.
    ''' </summary>
    Property Checkpoint As T
    ''' <summary>
    ''' Execute operation to advance the game.
    ''' </summary>
    Sub Execute()
    ''' <summary>
    ''' Rewind to checkpoint in normal game flow.
    ''' </summary>
    ''' <param name="reason">Reason for rewind request.</param>
    ''' <param name="message">Message for rewind request.</param>
    Sub Rewind(reason As RewindReason, message As String)
End Interface

''' <summary>
''' Rewind reason enumeration for game scenarios.
''' </summary>
Public Enum RewindReason As Integer
    ''' <summary>
    ''' Custom rewind scenario.
    ''' </summary>
    Custom = 0
    ''' <summary>
    ''' Player character death.
    ''' </summary>
    PlayerDeath = 1
    ''' <summary>
    ''' Player-initiated undo operation.
    ''' </summary>
    PlayerUndo = 2
    ''' <summary>
    ''' Level reset or restart.
    ''' </summary>
    LevelReset = 3
    ''' <summary>
    ''' Game exiting or shutdown.
    ''' </summary>
    GameExiting = 4
End Enum