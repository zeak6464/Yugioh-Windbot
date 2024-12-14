# WindBot Dialog System

The dialog system allows WindBot to communicate with players during duels using customizable messages. Each bot can have its own personality through unique dialog files.

## Dialog File Structure

Dialog files are JSON files located in the `Dialogs/` directory. Each file should have the `.json` extension and follow this structure:

```json
{
  "welcome": [],      // Messages when the bot joins
  "deckerror": [],    // Messages when there's a deck error
  "duelstart": [],    // Messages at the start of a duel
  "newturn": [],      // Messages when the bot's turn begins
  "endturn": [],      // Messages when the bot ends its turn
  "directattack": [], // Messages when performing a direct attack
  "attack": [],       // Messages when attacking a monster
  "ondirectattack": [], // Messages when being attacked directly
  "facedownmonstername": "", // Name for face-down monsters
  "activate": [],     // Messages when activating cards
  "summon": [],       // Messages when summoning monsters
  "setmonster": [],   // Messages when setting monsters
  "chaining": []      // Messages when activating chain effects
}
```

## Message Formatting

Messages can include placeholders that will be replaced with actual card names or other values:

- `{0}`, `{1}`: Placeholders for card names or other values
- Example: `"I summon {0}!"` will become `"I summon Dark Magician!"` when summoning that card

## Available Dialog Types

1. **Welcome Messages** (`welcome`)
   - Triggered when the bot joins the duel
   - Example: `"Hello! Ready to duel?"`

2. **Deck Error Messages** (`deckerror`)
   - Used when there's an issue with the bot's deck
   - Supports `{0}` for the problematic card name
   - Example: `"Sorry, there's a problem with {0} in my deck."`

3. **Duel Start Messages** (`duelstart`)
   - Shown at the beginning of the duel
   - Example: `"Let's have a fair duel!"`

4. **Turn Messages** (`newturn`, `endturn`)
   - `newturn`: When the bot's turn begins
   - `endturn`: When the bot ends its turn
   - Example: `"My turn! Draw!"`

5. **Attack Messages** (`directattack`, `attack`, `ondirectattack`)
   - `directattack`: When attacking directly ({0} = attacker name)
   - `attack`: When attacking a monster ({0} = attacker, {1} = defender)
   - `ondirectattack`: When being attacked directly ({0} = attacker name)
   - Example: `"{0}, attack {1}!"`

6. **Card Action Messages** (`activate`, `summon`, `setmonster`, `chaining`)
   - `activate`: When activating a card effect ({0} = card name)
   - `summon`: When summoning a monster ({0} = monster name)
   - `setmonster`: When setting a monster face-down
   - `chaining`: When activating a chain effect ({0} = card name)

## Creating a Custom Dialog File

1. Create a new JSON file in the `Dialogs/` directory (e.g., `custom.json`)
2. Include all required message categories
3. Add multiple messages for each category for variety
4. Use appropriate placeholders where needed

Example custom dialog file:

```json
{
  "welcome": [
    "Greetings, challenger!",
    "Ready to duel?"
  ],
  "deckerror": [
    "Oops! Something's wrong with {0} in my deck."
  ],
  "duelstart": [
    "May the best duelist win!",
    "Let's have an exciting match!"
  ],
  "newturn": [
    "My turn!",
    "Here I go! Draw!"
  ],
  "endturn": [
    "I end my turn.",
    "Your move!"
  ],
  "directattack": [
    "{0}, attack directly!",
    "Go, {0}! Direct attack!"
  ],
  "attack": [
    "{0}, attack {1}!",
    "I attack {1} with {0}!"
  ],
  "ondirectattack": [
    "Ouch! That hurt!",
    "Not bad..."
  ],
  "facedownmonstername": "unknown monster",
  "activate": [
    "I activate {0}!",
    "Behold the power of {0}!"
  ],
  "summon": [
    "I summon {0}!",
    "Come forth, {0}!"
  ],
  "setmonster": [
    "I set a monster.",
    "One monster face-down."
  ],
  "chaining": [
    "Not so fast! I chain {0}!",
    "I activate {0} in response!"
  ]
}
```

## Using Custom Dialogs

To use a custom dialog file:

1. Place your dialog file in the `Dialogs/` directory
2. Set the dialog file name in the configuration:
   ```json
   {
     "WindBot": {
       "Dialog": "custom"
     }
   }
   ```
   Note: Don't include the `.json` extension in the configuration

## Best Practices

1. **Variety**: Include multiple messages for each category to make the bot feel more dynamic
2. **Personality**: Create consistent personality traits through message style
3. **Placeholders**: Use placeholders correctly to include card names and other dynamic content
4. **Testing**: Verify all messages display correctly in-game
5. **Localization**: Create separate files for different languages (e.g., `custom.en.json`, `custom.zh-CN.json`)

## Error Handling

The dialog system includes built-in error handling:

- If a dialog file is not found, it will use default messages
- Empty messages ("") will be skipped
- Missing placeholders in messages will cause format errors
- Invalid JSON format will prevent the dialog file from loading
