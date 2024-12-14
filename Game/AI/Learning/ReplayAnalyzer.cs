using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YGOSharp.OCGWrapper.Enums;
using System.Linq;
using WindBot.Game.AI.Enums;

namespace WindBot.Game.AI.Learning
{
    public class ReplayAnalyzer
    {
        private readonly string _replayPath;
        private Dictionary<int, CardPlayPattern> _cardPatterns;

        public ReplayAnalyzer(string replayPath)
        {
            _replayPath = replayPath;
            _cardPatterns = new Dictionary<int, CardPlayPattern>();
            LoadExistingPatterns();
        }

        public void LoadExistingPatterns()
        {
            string patternPath = Path.Combine("TrainingData", "card_patterns.json");
            if (File.Exists(patternPath))
            {
                string json = File.ReadAllText(patternPath);
                var patterns = JsonConvert.DeserializeObject<Dictionary<int, CardPlayPattern>>(json);
                if (patterns != null)
                    _cardPatterns = patterns;
            }
        }

        public void AnalyzeReplayFile(string replayFile)
        {
            if (!File.Exists(replayFile))
            {
                throw new FileNotFoundException("Replay file not found", replayFile);
            }

            byte[] replayData = File.ReadAllBytes(replayFile);
            AnalyzeReplay(replayData);
        }

        public void AnalyzeReplay(byte[] replayData)
        {
            // Parse replay data
            using (var ms = new MemoryStream(replayData))
            using (var reader = new BinaryReader(ms))
            {
                while (ms.Position < ms.Length)
                {
                    var action = ReadNextAction(reader);
                    if (action != null)
                        ProcessAction(action);
                }
            }
            
            // Save learned patterns
            SavePatterns();
        }

        private ReplayAction ReadNextAction(BinaryReader reader)
        {
            try
            {
                byte actionType = reader.ReadByte();
                int cardId = reader.ReadInt32();
                return new ReplayAction
                {
                    Type = actionType,
                    CardId = cardId,
                    GameState = CaptureGameState(reader)
                };
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private GameState CaptureGameState(BinaryReader reader)
        {
            return new GameState
            {
                Turn = reader.ReadInt32(),
                Phase = reader.ReadByte(),
                PlayerLP = reader.ReadInt32(),
                OpponentLP = reader.ReadInt32(),
                CardsInHand = reader.ReadInt32(),
                MonsterCount = reader.ReadInt32(),
                SpellTrapCount = reader.ReadInt32()
            };
        }

        private void ProcessAction(ReplayAction action)
        {
            if (!_cardPatterns.ContainsKey(action.CardId))
                _cardPatterns[action.CardId] = new CardPlayPattern();

            _cardPatterns[action.CardId].AddPattern(action);
        }

        public void SavePatterns()
        {
            string patternPath = Path.Combine("TrainingData", "card_patterns.json");
            Directory.CreateDirectory(Path.GetDirectoryName(patternPath));
            string json = JsonConvert.SerializeObject(_cardPatterns, Formatting.Indented);
            File.WriteAllText(patternPath, json);
        }
    }

    public class ReplayAction
    {
        public byte Type { get; set; }
        public int CardId { get; set; }
        public GameState GameState { get; set; }
    }

    public class GameState
    {
        public int Turn { get; set; }
        public int Phase { get; set; }
        public int PlayerLP { get; set; }
        public int OpponentLP { get; set; }
        public int CardsInHand { get; set; }
        public int MonsterCount { get; set; }
        public int SpellTrapCount { get; set; }
    }

    public class CardPlayPattern
    {
        public List<PatternEntry> Patterns { get; set; } = new List<PatternEntry>();

        public void AddPattern(ReplayAction action)
        {
            Patterns.Add(new PatternEntry
            {
                GameState = action.GameState,
                ActionType = action.Type,
                SuccessCount = 1
            });
        }

        public byte GetBestAction(GameState currentState)
        {
            var bestPattern = Patterns
                .OrderByDescending(p => p.SuccessCount)
                .FirstOrDefault(p => IsStateSimilar(p.GameState, currentState));

            return bestPattern?.ActionType ?? 0;
        }

        private bool IsStateSimilar(GameState state1, GameState state2)
        {
            return Math.Abs(state1.MonsterCount - state2.MonsterCount) <= 1 &&
                   Math.Abs(state1.SpellTrapCount - state2.SpellTrapCount) <= 1 &&
                   state1.Phase == state2.Phase;
        }
    }

    public class PatternEntry
    {
        public GameState GameState { get; set; }
        public byte ActionType { get; set; }
        public int SuccessCount { get; set; }
    }
}
