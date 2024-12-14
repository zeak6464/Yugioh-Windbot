# WindBot Configuration Guide

This document describes the configuration options available in WindBot's `appsettings.json` file.

## Configuration File Location

The configuration file (`appsettings.json`) should be placed in the same directory as the WindBot executable. If the file is not found, WindBot will use default values.

## Configuration Structure

The configuration file uses a JSON structure with the following main sections:

```json
{
  "WindBot": {
    "Server": { ... },
    "Database": { ... },
    "Game": { ... },
    "Logging": { ... },
    "AI": { ... }
  }
}
```

## Server Configuration

Controls the WindBot server settings.

```json
"Server": {
  "Port": 2399,              // Port number for the WindBot server
  "EnableHttps": false,      // Enable/disable HTTPS
  "MaxConcurrentBots": 10,   // Maximum number of concurrent bot instances
  "RequestTimeout": 30000    // Request timeout in milliseconds
}
```

## Database Configuration

Configures the card database paths.

```json
"Database": {
  "DefaultPath": "cards.cdb",                          // Primary card database path
  "AlternativePaths": [                               // Fallback database paths
    "../cards.cdb",
    "../expansions/cards.cdb"
  ]
}
```

## Game Configuration

Controls game-related settings.

```json
"Game": {
  "DefaultDeckPath": "Decks",       // Path to deck files
  "DefaultName": "WindBot",         // Default bot name
  "DefaultPort": 7911,              // Default game server port
  "DefaultHost": "127.0.0.1",       // Default game server host
  "MessageTimeout": 3000,           // Message timeout in milliseconds
  "HandshakeTimeout": 10000,        // Connection handshake timeout
  "DefaultHostInfo": "",            // Default host information
  "DefaultVersion": 4946,           // Game version
  "DefaultHand": 0,                 // Starting hand count (0 for default)
  "DefaultRoomId": 0                // Default room ID
}
```

## Logging Configuration

Controls logging behavior.

```json
"Logging": {
  "LogLevel": "Info",        // Log level (Info, Debug, Error)
  "EnableDebug": false,      // Enable detailed debug logging
  "LogFile": "windbot.log",  // Log file name
  "MaxLogSize": 10485760,    // Maximum log file size in bytes (10MB)
  "MaxLogFiles": 5           // Maximum number of log files to keep
}
```

## AI Configuration

Controls AI behavior settings.

```json
"AI": {
  "ResponseDelay": 100,      // Delay before AI responses in milliseconds
  "ChainDelay": 200,        // Delay before chain reactions in milliseconds
  "DefaultBehavior": "Smart" // AI behavior mode
}
```

## Legacy Configuration Options

These options are maintained for compatibility with older versions:

```json
{
  "AssetPath": "",          // Path to game assets
  "ServerMode": false,      // Enable server mode
  "Train": false,           // Enable AI training mode
  "ReplayDir": "Replays",   // Directory for replay files
  "Deck": "",              // Default deck name
  "Name": "WindBot",       // Bot name
  "DeckFile": "",          // Specific deck file path
  "Dialog": "",            // Custom dialog file
  "Debug": false,          // Enable debug mode
  "Chat": true,            // Enable chat functionality
  "CreateGame": null       // Game creation parameters
}
```

## Configuration Loading

The configuration is loaded when WindBot starts. If any errors occur during loading, WindBot will use default values and log the error.

## Best Practices

1. Always validate the configuration file after making changes
2. Keep backups of working configurations
3. Use descriptive values for bot names and deck paths
4. Adjust timeouts based on network conditions
5. Enable debug logging temporarily when troubleshooting

## Example Configuration

Here's a complete example configuration:

```json
{
  "WindBot": {
    "Server": {
      "Port": 2399,
      "EnableHttps": false,
      "MaxConcurrentBots": 10,
      "RequestTimeout": 30000
    },
    "Database": {
      "DefaultPath": "cards.cdb",
      "AlternativePaths": [
        "../cards.cdb",
        "../expansions/cards.cdb"
      ]
    },
    "Game": {
      "DefaultDeckPath": "Decks",
      "DefaultName": "WindBot",
      "DefaultPort": 7911,
      "DefaultHost": "127.0.0.1",
      "MessageTimeout": 3000,
      "HandshakeTimeout": 10000,
      "DefaultHostInfo": "",
      "DefaultVersion": 4946,
      "DefaultHand": 0,
      "DefaultRoomId": 0
    },
    "Logging": {
      "LogLevel": "Info",
      "EnableDebug": false,
      "LogFile": "windbot.log",
      "MaxLogSize": 10485760,
      "MaxLogFiles": 5
    },
    "AI": {
      "ResponseDelay": 100,
      "ChainDelay": 200,
      "DefaultBehavior": "Smart"
    }
  }
}
```
