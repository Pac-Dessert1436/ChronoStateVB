# `ChronoStateVB` - Game State Management Toolkit for VB.NET

`ChronoStateVB` is a robust game state management toolkit tailored for VB.NET, offering two core capabilities: **parallel background computation** with lock-free double buffering, and **event-driven rewind/undo functionality** with state checkpointing.

Built specifically for VB.NET game development, this library integrates seamlessly with popular frameworks including [MonoGame](https://www.monogame.net/) and DualBrain's [vbPixelGameEngine](https://github.com/DualBrain/vbPixelGameEngine), as well as other custom VB.NET game projects.

## Features

- **BackgroundComputeAgent** — Run parallel computations on a background thread and safely exchange data with the main thread via lock-free double buffering
- **GameRewindSystem** — Event-driven, thread-safe singleton that executes operations in batch and rewinds them in reverse order
- **IRewindable / RewindReason** — Interface and enum for implementing rewindable game operations with categorized reasons

## Requirements

- [.NET SDK 10.0+](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- VB.NET development tools

## Installation

### NuGet

```bash
dotnet add package ChronoStateVB
```

### Manual

Clone the repository and add the `.vb` source files directly to your project, or reference the compiled `ChronoStateVB.dll`.

## Usage

### `BackgroundComputeAgent`

Run a data array computation in parallel on a background thread, and consume the results on the main thread without stalling either side.

```vb
' Define a value type for the computed data
Private Structure ParticleState
    Dim X As Single
    Dim Y As Single
    Dim Velocity As Single
End Structure

Private _agent As BackgroundComputeAgent(Of ParticleState)

' During `Initialize` phase:
_agent = New BackgroundComputeAgent(Of ParticleState)(
    dataLength:=1024,
    computeFunc:=Function(i)
                     ' Compute particle i — runs on a background thread
                     Dim rng As Single = CSng(Random.Shared.NextDouble())
                     Return New ParticleState With {
                         .X = i * 0.5F,
                         .Y = rng * 100.0F,
                         .Velocity = rng * 2.0F
                     }
                 End Function,
    onDataReady:=Sub(data)
                     ' Called on the main thread when new data is available
                     ' e.g., update vertex buffers, trigger rendering
                 End Sub,
    targetIntervalMs:=33  ' ≈ 30 fps
)
_agent.BeginCompute()

' During `Update` phase (main thread):
_agent.TrySwap()  ' Returns True if a buffer swap occurred

' Access the latest data at any time:
Dim currentData = _agent.CurrentBufferData

' During UnloadContent or shutdown:
_agent.Dispose()
```

**How it works:**

1. The background thread runs `Parallel.For` over all indices, writing results into the background buffer
2. When a computation cycle completes, it sets an atomic swap flag
3. The main thread calls `TrySwap()` each frame — if the flag is set, the two buffer pointers are exchanged atomically and the `onDataReady` callback fires
4. No locks are needed on the hot path — only `Interlocked` operations for the swap flag and pointer swap

### `GameRewindSystem`

Register operations that can be undone, execute them in batch, and rewind all executed operations in reverse order when needed.

```vb
' Define your save data type
Private Structure PlayerCheckpoint
    Dim PositionX As Single
    Dim PositionY As Single
    Dim Health As Integer
End Structure

' Implement IRewindable(Of T)
Public Class MoveAction
    Implements IRewindable(Of PlayerCheckpoint)

    Public Property Checkpoint As PlayerCheckpoint Implements IRewindable(Of PlayerCheckpoint).Checkpoint

    Private ReadOnly _player As Player

    Public Sub New(player As Player)
        _player = player
        ' Save checkpoint before execution
        Checkpoint = New PlayerCheckpoint With {
            .PositionX = player.X,
            .PositionY = player.Y,
            .Health = player.Health
        }
    End Sub

    Public Sub Execute() Implements IRewindable(Of PlayerCheckpoint).Execute
        ' Advance the game state
        _player.MoveTo(_player.TargetX, _player.TargetY)
    End Sub

    Public Sub Rewind(reason As RewindReason, message As String) Implements IRewindable(Of PlayerCheckpoint).Rewind
        ' Restore from checkpoint
        _player.X = Checkpoint.PositionX
        _player.Y = Checkpoint.PositionY
        _player.Health = Checkpoint.Health
    End Sub
End Class

' Usage in game flow:
Dim rewindSystem = GameRewindSystem(Of PlayerCheckpoint).Instance

' Register operations before execution
rewindSystem.RegisterRewindable(New MoveAction(player1))
rewindSystem.RegisterRewindable(New MoveAction(player2))

' Execute all pending operations in batch
rewindSystem.ExecuteBatch(Sub(msg) Debug.WriteLine(msg))

' Later, when the player dies:
rewindSystem.TriggerRewind(RewindReason.PlayerDeath, "Player fell into lava")

' Or when the player presses undo:
rewindSystem.TriggerRewind(RewindReason.PlayerUndo)

' When a level ends and history is no longer needed:
rewindSystem.ClearExecutedHistory()
```

**How it works:**

1. Register `IRewindable(Of T)` operations before execution
2. `ExecuteBatch` runs all pending operations and accumulates them in history (multiple batches accumulate)
3. `TriggerRewind` rewinds all successfully executed operations in reverse order, then clears history
4. Failed operations (exceptions during `Execute`) are skipped during rewind
5. The `RewindRequested` event fires before any rewind, allowing subscribers to react

### `RewindReason` Enum

| Member | Value | Description |
|---|---|---|
| `Custom` | 0 | Custom rewind scenario |
| `PlayerDeath` | 1 | Player character death |
| `PlayerUndo` | 2 | Player-initiated undo operation |
| `LevelReset` | 3 | Level reset or restart |
| `GameExiting` | 4 | Game exiting or shutdown |

## API Reference

### `BackgroundComputeAgent(Of T As Structure)`

| Member | Description |
|---|---|
| `New(dataLength, computeFunc, onDataReady, targetIntervalMs)` | Constructor. `targetIntervalMs` defaults to 33 (≈ 30 fps) |
| `BeginCompute()` | Starts the background computation loop |
| `EndCompute()` | Signals the background task to stop |
| `WaitForStop(timeoutMs)` | Waits for the background task to fully stop (default 3000 ms) |
| `TrySwap() As Boolean` | Main thread call — checks for new data and swaps buffers if available |
| `CurrentBufferData As T()` | Gets the current buffer (safe to read from the main thread) |
| `Dispose()` | Stops computation and releases resources |

### `GameRewindSystem(Of T)`

| Member | Description |
|---|---|
| `Instance` | Thread-safe singleton instance |
| `RegisterRewindable(rewindable)` | Registers a rewindable operation for execution |
| `UnregisterRewindable(rewindable)` | Removes a previously registered operation |
| `ExecuteBatch(logAction)` | Executes all pending operations and accumulates history |
| `TriggerRewind(reason, message, logAction)` | Rewinds all executed operations in reverse order |
| `PendingRewindableCount As Integer` | Number of pending (not yet executed) operations |
| `ExecutedCount As Integer` | Number of items in execution history |
| `ClearRewindables()` | Clears all pending operations |
| `ClearHistory()` | Clears execution history (prevents rewinding cleared items) |
| `RewindRequested` | Event raised when a rewind is triggered |

### `IRewindable(Of T)`

| Member | Description |
|---|---|
| `Checkpoint As T` | State snapshot saved before execution |
| `Execute()` | Execute the operation to advance the game |
| `Rewind(reason, message)` | Rewind to the checkpoint state |

## License

[BSD 3-Clause](LICENSE)