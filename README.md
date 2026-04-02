# `GameRewindSystemVB` - A Rewind System for Games, Inspired by Transaction Model

## Description
`GameRewindSystemVB` is a meticulous rewind system for game development that brings database transaction concepts to interactive gaming scenarios. This innovative framework enables game developers to implement robust undo/redo functionality, checkpoint systems, and state rollback mechanisms with enterprise-level reliability.

The system adapts the proven transaction model from database systems - where operations are atomic, consistent, isolated, and durable (ACID properties) - to game development contexts. This approach provides a structured way to manage game state changes, allowing developers to:

- **Implement reliable undo/redo functionality** for player actions
- **Create checkpoint systems** that save game progress at specific points
- **Handle game state rollbacks** for scenarios like player death or level resets
- **Manage complex state transitions** with transactional guarantees

Unlike traditional game development approaches that often rely on ad-hoc state management, `GameRewindSystemVB` provides a systematic framework that ensures state consistency and simplifies the implementation of rewind capabilities across various game genres and platforms.

## Requirements

- [.NET 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or higher
- Visual Basic .NET compatible development environment, such as:
  - [Visual Studio 2022/2026](https://visualstudio.microsoft.com/vs/)
  - [Visual Studio Code](https://code.visualstudio.com/)
  - Any other VB.NET-compatible IDE

## Features

- **Transaction-Inspired Design**: Implements a transaction-like model for game state management
- **Thread-Safe Singleton**: Thread-safe implementation using lazy initialization
- **Event-Driven Architecture**: Supports rewind request events for game flow integration
- **Batch Execution**: Execute multiple operations in batch with logging support
- **Flexible Rewind Reasons**: Built-in enumeration for common game scenarios (player death, undo, level reset, etc.)
- **Generic Type Support**: Supports any save data type through generics
- **Exception Handling**: Robust error handling with logging capabilities

## Architecture

The system is built around two main components:

### 1. GameRewindSystem(Of T)
A thread-safe singleton class that manages the rewind operations:
- **RegisterRewindable()**: Register operations for execution
- **ExecuteBatch()**: Execute all pending operations in batch
- **TriggerRewind()**: Trigger rewind to previous checkpoint
- **PendingRewindableCount**: Get count of pending operations
- **ClearRewindables()**: Clear all pending operations

### 2. IRewindable(Of T) Interface
Interface that game objects must implement to support rewind functionality:
- **Checkpoint Property**: Save state before execution
- **Execute()**: Advance the game state
- **Rewind()**: Return to checkpoint state

## Usage Example

1. Define a rewindable class:
```vb
' Example implementation for a player movement system
Public Class PlayerMovementRewindable
    Implements IRewindable(Of PlayerState)

    Public Property Checkpoint As PlayerState Implements IRewindable(Of PlayerState).Checkpoint
    
    Public Sub Execute() Implements IRewindable(Of PlayerState).Execute
        ' Move player forward in the game
        ' Update player position, velocity, etc.
    End Sub
    
    Public Sub Rewind(reason As RewindReason, message As String) Implements IRewindable(Of PlayerState).Rewind
        ' Restore player to checkpoint state
        ' Reset position, velocity, animation state, etc.
    End Sub
End Class
```

2. Register the rewindable in the game loop:
```vb
With GameRewindSystem(Of PlayerState).Instance
    ' Register operations during gameplay
    .RegisterRewindable(New PlayerMovementRewindable)
    ' Execute batch of operations
    .ExecuteBatch(Sub(msg) Console.WriteLine(msg))
    ' Trigger rewind when needed (e.g., player dies)
    .TriggerRewind(RewindReason.PlayerDeath, "Player died - rewinding")
End With
```

## Rewind Reasons

The system includes predefined rewind reasons:
- **Custom**: Custom rewind scenario
- **PlayerDeath**: Player character death
- **PlayerUndo**: Player-initiated undo operation
- **LevelReset**: Level reset or restart
- **GameExiting**: Game exit or shutdown

## Installation & Usage

1. Clone or download the repository, and navigate to the project directory:
```bash
git clone https://github.com/Pac-Dessert1436/GameRewindSystemVB.git
cd GameRewindSystemVB
```
2. Reference this library into your VB.NET project:
```bash
dotnet add path/to/YourProject.vbproj reference GameRewindSystemVB.vbproj
```
> **Note**: You can also manually copy `GameRewindSystem.vb` and `IRewindable.vb` to your VB.NET project.
3. Implement the `IRewindable(Of T)` interface for your game objects
4. Use the `GameRewindSystem(Of T).Instance` singleton in your game loop

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bug reports and feature requests.

## License
This project is licensed under the BSD 3-Clause License. See the [LICENSE](LICENSE) file for details.