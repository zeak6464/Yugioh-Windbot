# WindBot - Yu-Gi-Oh! AI Dueling Bot

WindBot is an AI dueling bot for Yu-Gi-Oh! that implements strategic gameplay through a rule-based AI system. The bot can play various decks, make complex decisions, and handle card interactions.

## Features

- Rule-based AI system with support for complex card interactions
- Extensive deck support with specialized executors for different strategies
- Automatic card activation and chain resolution
- Intelligent monster positioning and battle decisions
- Built-in support for common staple cards and effects
- Detailed logging system for debugging and analysis

## Setup

1. Build the solution using Visual Studio or the .NET CLI:
```bash
dotnet build WindBot.sln
```

2. Place the following files in the output directory (e.g., `bin/Debug`):
   - `cards.cdb`: YGOPro card database file
   - `bots.json`: Bot configurations (copied automatically from `Configuration` directory)
   - `Decks/*.ydk`: Bot deck files

## Configuration

All configuration files are located in the `Configuration` directory:

1. `appsettings.json`: General WindBot settings
   - Server configuration
   - Database paths
   - Game settings
   - Logging options

2. `ai_config.json`: AI behavior configuration
   - Reward values for different actions
   - Decision-making parameters
   - See [AI Configuration Guide](docs/AIConfiguration.md) for details

3. `bots.json`: Bot definitions
   - Bot names and decks
   - Difficulty levels
   - Supported Master Rules

Example bot configuration in `bots.json`:
```json
{
    "name": "Blue-Eyes",
    "deck": "Blue-Eyes",
    "difficulty": 2,
    "masterRules": [ 3, 4, 5 ]
}
```

## Getting Started

### Prerequisites

- Windows operating system
- .NET Framework 4.0 or higher
- YGOPro or similar Yu-Gi-Oh! dueling platform
- Visual Studio (for development)

### Building from Source

1. Clone the repository
2. Open `WindBot.sln` in Visual Studio
3. Build the solution using Visual Studio or run:
```
dotnet build WindBot.sln
```

### Running the Bot

Basic usage:
```
WindBot.exe [options]
```

Options:
- `Name`: Bot's name (default: WindBot)
- `Deck`: Deck to use (default: random from Decks directory)
- `Host`: Host to connect to (default: 127.0.0.1)
- `Port`: Port to connect to (default: 7911)
- `Version`: Version to tell the server (default: 0x1353)
- `Debug`: Enable debug mode (default: false)
- `Chat`: Enable chat (default: false)
- `Hand`: Starting hand count (default: 5)

Example:
```
WindBot.exe Name=WindBot Deck=Blue-Eyes Host=127.0.0.1 Port=7911 Debug=true #Will run Blue-Eyes Deck 
WindBot.exe Name=WindBot Deck=AI_CustomDeck Host=127.0.0.1 Port=7911 Debug=true #Will Run your custom deck
```

### Server Mode

You can run WindBot in server mode to accept connections:

```
WindBot.exe ServerMode=true ServerPort=2399
```

Connect to the bot using:
```
http://127.0.0.1:2399/?name=WindBot&host=127.0.0.1&port=7911
```

## Deck System

### Deck Structure
Each deck in WindBot consists of:
1. A deck definition class with the `[Deck]` attribute
2. An executor class that inherits from `DefaultExecutor`
3. A corresponding `.ydk` file in the `Decks` directory

### Creating a New Deck

1. Create a new executor class in `Game/AI/Decks/`:
```csharp
[Deck("DeckName", "AI_DeckName", "AI_Level")]
public class DeckNameExecutor : DefaultExecutor
{
    public DeckNameExecutor(GameAI ai, Duel duel)
        : base(ai, duel)
    {
        // Add executors for card effects
        AddExecutor(ExecutorType.Activate, CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
        // Add more executors...
    }
}
```

2. Add card execution logic:
```csharp
private bool HandleCardEffect()
{
    // Implement card effect logic
    return true;
}
```

3. Register the deck in `Game/AI/Decks/DecksManager.cs`

## AI System

The AI uses a priority-based executor system:
- Each card effect has an assigned priority and conditions
- The bot evaluates executors in order of priority
- Common card effects (like hand traps) have default implementations
- Specialized card effects can be customized per deck

### Default Executors

The bot includes default executors for common scenarios:
- Monster summoning and positioning
- Spell/Trap activation
- Battle decisions
- Chain resolution
- Hand trap timing

### Debugging

Enable debug mode to see detailed decision logs:
```
WindBot.exe Debug=true
```

Debug logs will show:
- Card activation decisions
- Chain building logic
- Battle phase calculations
- AI state evaluations

## AI Learning System

### Overview
WindBot now includes an AI learning system that can analyze replay files to learn and improve its gameplay strategies. The system consists of:
1. `ReplayAnalyzer`: Analyzes replay files to learn card usage patterns
2. `DeckTrainer`: Command-line interface for training the AI using replay files

### Training the AI

1. Collect replay files from your duels
2. Place the replay files in a directory
3. Use the DeckTrainer to train the AI:
```
WindBot.exe Train=true ReplayDir=path/to/replays Deck=YourDeck
```

### How It Works

The AI learning system:
- Analyzes game states and card activations from replays
- Learns optimal timing for card activations
- Captures card interaction patterns
- Stores learned patterns in JSON format for future use

### Training Data
Learned patterns are stored in:
- `TrainingData/card_patterns.json`: Card activation patterns
- Each pattern includes:
  - Game state conditions
  - Success rate of actions
  - Card interaction chains

### Customizing Training

You can customize the training process by:
1. Filtering replay files by deck type
2. Setting specific learning parameters
3. Focusing on particular card interactions

The AI will continuously improve its gameplay based on the analyzed replays.

## AI Training System

### Overview
WindBot now includes a sophisticated AI training system that learns from replay files to improve its gameplay decisions. The system analyzes duels to understand optimal card activation timing and strategy patterns.

### Components
- `ReplayAnalyzer`: Core component that processes replay files to extract card usage patterns
- `CardPlayPattern`: Stores learned patterns for each card
- `GameState`: Captures game state during pattern analysis
- Pattern storage in JSON format for persistence

### Training the AI

1. **Collecting Training Data**
   - Save your duel replays (*.yrp or *.yrpX files)
   - Place replays in the `Replays` directory
   - Best practice: Use replays from successful duels with the deck you want to train

2. **Running Training Mode**
```
WindBot.exe Train=true ReplayDir=Replays Deck=YourDeckName
```
Example:
```
WindBot.exe Train=true ReplayDir=Replays Deck=AI_SimpleTest
```

3. **Training Output**
   - Patterns are saved to `TrainingData/card_patterns.json`
   - Each card's pattern includes:
     - Optimal activation timing
     - Game state conditions
     - Success rates
     - Card interaction chains

### Using Trained AI
After training, the bot will automatically use the learned patterns when:
- Playing with the trained deck
- Making card activation decisions
- Choosing optimal timing for effects
- Determining card interactions

### Training Tips
- Use high-quality replays from skilled players
- Include replays with various matchups
- Provide replays showing different strategies
- Include both winning and losing games for balanced learning

### Technical Details
The training system:
- Parses YGOPro replay format
- Extracts card activation sequences
- Analyzes game state during successful plays
- Uses pattern matching for decision making
- Supports incremental learning from new replays

## Bot Configuration

### Bot Definitions
Bots are defined in `bots.json` with the following properties:
- `name`: Display name of the bot
- `deck`: Name of the deck to use (must match a deck in the `Decks` directory)
- `difficulty`: Bot's skill level (0-3)
- `masterRules`: List of Yu-Gi-Oh! Master Rule sets the bot supports

Example bot configuration:
```json
{
    "name": "Blue-Eyes",
    "deck": "Blue-Eyes",
    "difficulty": 2,
    "masterRules": [ 3, 4, 5 ]
}
```

### Difficulty Levels
- 0: Basic (Simple card activation and battle decisions)
- 1: Intermediate (Basic combos and chain resolution)
- 2: Advanced (Complex combos and strategic plays)
- 3: Expert (Optimal decision making and advanced strategies)

### Master Rules Support
- 3: Classic Master Rules
- 4: Master Rule 4 (Link Era)
- 5: Master Rule 5 (Current)

### Adding New Bots
1. Create your deck file in the `Decks` directory
2. Add a corresponding executor class
3. Add bot configuration to `bots.json`
4. (Optional) Train the bot using replay files

## Developer Guide

### Development Environment Setup

1. **Required Software**
   - Visual Studio 2019 or later
   - .NET Framework 4.8 SDK
   - Git for version control
   - (Optional) ReSharper or similar for code analysis

2. **First-Time Setup**
   ```powershell
   # Clone the repository
   git clone https://github.com/your-username/windbot.git
   cd windbot

   # Open solution in Visual Studio
   start WindBot.sln
   ```

3. **Project Structure**
   ```
   WindBot/
   ├── Game/               # Core game logic
   │   ├── AI/            # AI implementation
   │   ├── Decks/        # Deck definitions
   │   └── Network/      # Network protocol
   ├── ExecutorBase/      # Base AI executor classes
   ├── BotWrapper/        # Bot management system
   └── YGOSharp.Network/  # Network protocol implementation
   ```

### Code Style Guidelines

1. **Naming Conventions**
   - Use PascalCase for class names and public members
   - Use camelCase for private fields
   - Prefix interfaces with 'I'
   - Use meaningful and descriptive names

2. **Code Organization**
   - One class per file
   - Group related files in appropriate namespaces
   - Keep methods focused and single-purpose
   - Maximum method length: 50 lines (recommended)

3. **Comments and Documentation**
   - Use XML documentation for public APIs
   - Document complex algorithms
   - Explain "why" rather than "what"
   - Keep comments up-to-date with code changes

### Creating New Features

1. **Adding a New Deck**
   ```csharp
   [Deck("MyNewDeck", "AI_MyNewDeck")]
   public class MyNewDeckExecutor : DefaultExecutor
   {
       public MyNewDeckExecutor(GameAI ai, Duel duel)
           : base(ai, duel)
       {
           // Define card execution order
           AddExecutor(ExecutorType.Activate, CardId.PotOfDesires);
           AddExecutor(ExecutorType.SpSummon, CardId.AccesscodeTalker);
       }

       // Implement custom card logic
       private bool HandlePotOfDesires()
       {
           if (Bot.Deck.Count >= 12)
               return true;
           return false;
       }
   }
   ```

2. **Adding New Card Effects**
   - Create effect handler in deck executor
   - Register with appropriate ExecutorType
   - Consider chain timing and priorities
   - Test with various game states

3. **Implementing AI Behavior**
   - Use DefaultExecutor as base
   - Override default behaviors when needed
   - Consider card advantage and board state
   - Implement proper error handling

### Testing

1. **Unit Testing**
   - Test individual card effects
   - Test decision-making logic
   - Test game state evaluation
   - Use mock objects for dependencies

2. **Integration Testing**
   - Test complete duels
   - Test network communication
   - Test different game scenarios
   - Verify correct chain resolution

3. **Manual Testing**
   - Test against human players
   - Verify decision making
   - Check for edge cases
   - Document unexpected behavior

### Debugging

1. **Using Debug Mode**
   ```
   WindBot.exe Debug=true LogFile=debug.log
   ```

2. **Debug Output**
   - Card activation decisions
   - Chain building process
   - Battle calculations
   - AI state evaluation

3. **Common Issues**
   - Network connection problems
   - Card effect timing issues
   - Memory management
   - Performance bottlenecks

### Performance Optimization

1. **Code Optimization**
   - Use appropriate data structures
   - Minimize object creation
   - Cache frequently used values
   - Profile critical paths

2. **Memory Management**
   - Dispose of unused resources
   - Avoid memory leaks
   - Monitor memory usage
   - Use weak references when appropriate

### Contributing

1. **Pull Request Process**
   - Fork the repository
   - Create feature branch
   - Make changes following guidelines
   - Submit pull request with description

2. **Code Review**
   - All code must be reviewed
   - Address review comments
   - Update documentation
   - Maintain test coverage

3. **Version Control**
   - Use meaningful commit messages
   - Keep commits focused
   - Rebase before merging
   - Follow Git best practices

### Troubleshooting

1. **Build Issues**
   - Clean solution
   - Restore NuGet packages
   - Check .NET version
   - Verify dependencies

2. **Runtime Issues**
   - Check debug logs
   - Verify network connection
   - Validate card database
   - Check game state

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Thanks to the YGOPro team for the game engine
- Thanks to all contributors who have helped improve WindBot
