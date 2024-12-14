# WindBot AI Configuration Guide

This document describes how to configure the AI behavior in WindBot using the `ai_config.json` file and the `AIRewardConfig` class.

## Configuration Files

The configuration files are located in the `Configuration` directory:

1. `ai_config.json` - AI behavior configuration
2. `appsettings.json` - General WindBot settings
3. `bots.json` - Bot definitions and decks

### Bot Configuration

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

The difficulty levels are used to determine the bot's skill level:

- **0**: Beginner
- **1**: Intermediate
- **2**: Advanced
- **3**: Expert

## Configuration Structure

The AI configuration file (`ai_config.json`) uses a JSON structure with the following main sections:

```json
{
  "Rewards": { ... },
  "Learning": { ... },
  "DecisionMaking": { ... },
  "MemoryManagement": { ... }
}
```

## Reward Configuration

Controls how the AI values different actions and outcomes.

### General Rewards

Basic rewards for common actions:

```json
"General": {
  "default_summon": 0.8,        // Base reward for summoning monsters
  "default_activate_spell": 0.75,// Base reward for activating spells
  "default_set_trap": 0.7,      // Base reward for setting traps
  "default_attack": 0.8,        // Base reward for attacking
  "default_chain": 0.75         // Base reward for chaining effects
}
```

### Field State Rewards

Rewards based on the current field state:

```json
"FieldState": {
  "empty_field_summon": 0.9,           // Reward for summoning to empty field
  "no_monsters_activate_spell": 0.85,   // Reward for spell when no monsters
  "winning_position_attack": 0.9,       // Reward for attacking while winning
  "losing_position_activate": 0.8       // Reward for activating while losing
}
```

### Life Point Rewards

Rewards related to life point changes:

```json
"LifePoints": {
  "gain_life_points": 0.6,     // Reward for gaining life points
  "lose_life_points": -0.7,    // Penalty for losing life points
  "deal_damage": 0.8,          // Reward for dealing damage
  "lethal_damage": 1.0         // Reward for dealing game-winning damage
}
```

### Card Advantage Rewards

Rewards for card advantage:

```json
"CardAdvantage": {
  "draw_card": 0.5,            // Reward for drawing cards
  "search_deck": 0.7,          // Reward for searching deck
  "special_summon": 0.8,       // Reward for special summoning
  "destroy_opponent_card": 0.9, // Reward for destroying opponent's cards
  "negate_effect": 0.85        // Reward for negating effects
}
```

### Resource Management Rewards

Penalties for using resources:

```json
"ResourceManagement": {
  "use_normal_summon": -0.2,   // Cost of using normal summon
  "use_special_summon": -0.1,  // Cost of using special summon
  "discard_cost": -0.3,        // Cost of discarding cards
  "banish_cost": -0.4,         // Cost of banishing cards
  "life_point_cost": -0.01     // Cost per life point paid
}
```

### Combo Rewards

Rewards for card combinations:

```json
"Combos": {
  "two_card_combo": 0.9,       // Reward for 2-card combos
  "three_card_combo": 1.0,     // Reward for 3-card combos
  "archetype_synergy": 0.8,    // Reward for archetype synergy
  "type_synergy": 0.7,         // Reward for type synergy
  "attribute_synergy": 0.6     // Reward for attribute synergy
}
```

## Learning Configuration

Controls how the AI learns from experience:

```json
"Learning": {
  "ExplorationRate": 0.3,      // Initial exploration rate
  "ExplorationDecay": 0.995,   // Rate at which exploration decreases
  "MinExploration": 0.05,      // Minimum exploration rate
  "LearningRate": 0.1,         // How quickly AI adapts to new information
  "DiscountFactor": 0.9        // Value of future rewards vs immediate ones
}
```

## Decision Making Configuration

Controls how the AI makes decisions:

```json
"DecisionMaking": {
  "MaxThinkingTime": 5000,     // Maximum time to think in milliseconds
  "MinConfidence": 0.6,        // Minimum confidence to take action
  "ChainThreshold": 0.7,       // Minimum value to chain effects
  "AggressivenessLevel": 0.8   // How aggressive the AI should be
}
```

## Memory Management Configuration

Controls how the AI manages its memory:

```json
"MemoryManagement": {
  "MaxStoredGames": 1000,      // Maximum number of games to remember
  "MaxStoredStates": 10000,    // Maximum number of states to remember
  "SaveInterval": 100,         // How often to save learned data
  "PruneThreshold": 0.3        // When to remove old data
}
```

## Best Practices

1. **Start with Defaults**: Use the default values as a starting point
2. **Gradual Changes**: Make small adjustments to observe their effects
3. **Balance Rewards**: Keep rewards proportional to their importance
4. **Test Thoroughly**: Test changes against various deck types
5. **Monitor Performance**: Track win rates after making changes

## Example Configurations

### Aggressive AI

```json
{
  "Rewards": {
    "General": {
      "default_attack": 0.9,
      "default_summon": 0.85
    },
    "DecisionMaking": {
      "AggressivenessLevel": 0.9
    }
  }
}
```

### Defensive AI

```json
{
  "Rewards": {
    "General": {
      "default_set_trap": 0.8,
      "default_chain": 0.85
    },
    "DecisionMaking": {
      "AggressivenessLevel": 0.6
    }
  }
}
```

## Error Handling

- If the configuration file is missing, default values will be used
- Invalid values will be replaced with defaults
- Changes take effect after restarting WindBot
- Configuration errors are logged in the WindBot log file
