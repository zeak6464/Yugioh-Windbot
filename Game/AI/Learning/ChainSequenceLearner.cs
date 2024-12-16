using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Learning
{
    public class ChainSequence
    {
        public List<int> CardSequence { get; set; }
        public double SuccessRate { get; set; }
        public int TimesUsed { get; set; }
        public Dictionary<string, double> StateRewards { get; set; }

        public ChainSequence()
        {
            CardSequence = new List<int>();
            StateRewards = new Dictionary<string, double>();
        }
    }

    public class ChainSequenceLearner : LearningBase
    {
        private readonly string ChainFile = "TrainingData/chain_sequences.json";
        private Dictionary<string, Dictionary<int, double>> _chainResponses;
        private DuelGameState _chainStartState;

        public ChainSequenceLearner()
        {
            _chainResponses = LoadFromFile<Dictionary<string, Dictionary<int, double>>>(ChainFile);
            if (_chainResponses == null)
            {
                _chainResponses = new Dictionary<string, Dictionary<int, double>>();
            }
        }

        public void StartChain(DuelGameState state)
        {
            _chainStartState = state;
        }

        public void CompleteChain(DuelGameState finalState, double reward)
        {
            if (_chainStartState == null)
                return;

            string stateHash = _chainStartState.GetStateHash();
            
            if (!_chainResponses.ContainsKey(stateHash))
            {
                _chainResponses[stateHash] = new Dictionary<int, double>();
            }

            // Update chain response data
            for (int i = 0; i < finalState.BoardState.Count; i++)
            {
                string boardEntry = finalState.BoardState[i];
                if (string.IsNullOrEmpty(boardEntry))
                    continue;

                // Parse the card ID from the board entry string
                int cardId = ParseCardId(boardEntry);
                if (cardId != 0)  // 0 indicates invalid or no card
                {
                    if (!_chainResponses[stateHash].ContainsKey(cardId))
                    {
                        _chainResponses[stateHash][cardId] = 0;
                    }
                    _chainResponses[stateHash][cardId] += reward;
                }
            }

            SaveToFile(ChainFile, _chainResponses);
        }

        private int ParseCardId(string boardEntry)
        {
            try
            {
                string[] parts = boardEntry.Split(':');
                if (parts.Length >= 2)
                {
                    return int.Parse(parts[1]);
                }
            }
            catch (Exception)
            {
                // Invalid format, return 0
            }
            return 0;
        }

        public bool ShouldRespond(DuelGameState currentState, int cardId)
        {
            foreach (var stateEntry in _chainResponses)
            {
                try
                {
                    DuelGameState state = JsonConvert.DeserializeObject<DuelGameState>(stateEntry.Key);
                    if (AreStatesSimilar(state, currentState))
                    {
                        // If we have positive experience with this card in a similar state
                        if (stateEntry.Value.ContainsKey(cardId) && stateEntry.Value[cardId] > 0)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception)
                {
                    // Skip invalid entries
                    continue;
                }
            }
            return false;
        }

        public List<int> SuggestNextChainCards(DuelGameState currentState)
        {
            var suggestions = new List<int>();
            var stateHash = currentState.GetStateHash();

            if (_chainResponses.ContainsKey(stateHash))
            {
                var responses = _chainResponses[stateHash];
                suggestions.AddRange(responses.Where(r => r.Value > 0).Select(r => r.Key));
            }

            return suggestions;
        }
    }
}
