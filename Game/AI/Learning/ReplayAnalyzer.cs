using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Learning
{
    public class ReplayAnalyzer
    {
        private Dictionary<string, CardPlayPattern> Patterns;
        private string SavePath;

        public ReplayAnalyzer(string savePath)
        {
            SavePath = savePath;
            Patterns = LoadExistingPatterns();
        }

        public Dictionary<string, CardPlayPattern> LoadExistingPatterns()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    return JsonConvert.DeserializeObject<Dictionary<string, CardPlayPattern>>(json);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error loading patterns: {ex.Message}");
            }
            return new Dictionary<string, CardPlayPattern>();
        }

        public void AnalyzeReplayFile(string replayFile)
        {
            try
            {
                byte[] data = File.ReadAllBytes(replayFile);
                AnalyzeReplay(data);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error analyzing replay file: {ex.Message}");
            }
        }

        public void AnalyzeReplay(byte[] data)
        {
            int index = 0;
            GameState currentState = new GameState
            {
                PlayerLP = 8000,
                OpponentLP = 8000,
                CardsInHand = 5,  // Starting hand size
                CardsOnField = 0,
                OpponentCardsOnField = 0
            };
            ReplayAction lastAction = null;

            while (index < data.Length)
            {
                ReplayAction action = ReadNextAction(data, ref index);
                if (action == null)
                    break;

                action.GameState = CaptureGameState(currentState);
                ProcessAction(action, lastAction);
                lastAction = action;
            }

            SavePatterns();
        }

        private ReplayAction ReadNextAction(byte[] data, ref int index)
        {
            if (index + 2 > data.Length)
                return null;

            byte type = data[index++];
            int cardId = BitConverter.ToInt32(data, index);
            index += 4;

            return new ReplayAction
            {
                Type = type,
                CardId = cardId
            };
        }

        private GameState CaptureGameState(GameState state)
        {
            // Deep copy current state
            return new GameState
            {
                PlayerLP = state.PlayerLP,
                OpponentLP = state.OpponentLP,
                CardsInHand = state.CardsInHand,
                CardsOnField = state.CardsOnField,
                OpponentCardsOnField = state.OpponentCardsOnField
            };
        }

        private void ProcessAction(ReplayAction action, ReplayAction lastAction)
        {
            if (lastAction == null)
                return;

            string key = GetPatternKey(lastAction.CardId, lastAction.Type);
            if (!Patterns.ContainsKey(key))
            {
                Patterns[key] = new CardPlayPattern();
            }

            Patterns[key].AddPattern(lastAction.GameState, action);
        }

        private string GetPatternKey(int cardId, byte type)
        {
            return $"{cardId}_{type}";
        }

        public void SavePatterns()
        {
            try
            {
                string json = JsonConvert.SerializeObject(Patterns, Formatting.Indented);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error saving patterns: {ex.Message}");
            }
        }
    }

    public class ReplayAction
    {
        public byte Type { get; set; }
        public int CardId { get; set; }
        public GameState GameState { get; set; }
    }

    public class CardPlayPattern
    {
        private List<PatternEntry> Patterns;
        private const double SimilarityThreshold = 0.8;

        public CardPlayPattern()
        {
            Patterns = new List<PatternEntry>();
        }

        public void AddPattern(GameState state, ReplayAction nextAction)
        {
            Patterns.Add(new PatternEntry
            {
                State = state,
                NextAction = nextAction,
                SuccessCount = 1
            });
        }

        public ReplayAction GetBestAction(GameState currentState)
        {
            PatternEntry bestPattern = null;
            double bestSimilarity = 0;

            foreach (var pattern in Patterns)
            {
                double similarity = IsStateSimilar(pattern.State, currentState);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    bestPattern = pattern;
                }
            }

            if (bestSimilarity >= SimilarityThreshold && bestPattern != null)
            {
                return bestPattern.NextAction;
            }

            return null;
        }

        private double IsStateSimilar(GameState state1, GameState state2)
        {
            int totalFeatures = 5;
            int matchingFeatures = 0;

            if (Math.Abs(state1.PlayerLP - state2.PlayerLP) < 2000) matchingFeatures++;
            if (Math.Abs(state1.OpponentLP - state2.OpponentLP) < 2000) matchingFeatures++;
            if (Math.Abs(state1.CardsInHand - state2.CardsInHand) <= 1) matchingFeatures++;
            if (Math.Abs(state1.CardsOnField - state2.CardsOnField) <= 1) matchingFeatures++;
            if (Math.Abs(state1.OpponentCardsOnField - state2.OpponentCardsOnField) <= 1) matchingFeatures++;

            return (double)matchingFeatures / totalFeatures;
        }
    }

    public class PatternEntry
    {
        public GameState State { get; set; }
        public ReplayAction NextAction { get; set; }
        public int SuccessCount { get; set; }
    }
}
