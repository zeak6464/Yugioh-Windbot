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
            foreach (var card in finalState.BoardState)
            {
                if (!_chainResponses[stateHash].ContainsKey(card.Key))
                {
                    _chainResponses[stateHash][card.Key] = 0;
                }
                _chainResponses[stateHash][card.Key] += reward;
            }

            SaveToFile(ChainFile, _chainResponses);
        }

        public bool ShouldRespond(DuelGameState currentState, int cardId)
        {
            foreach (var entry in _chainResponses)
            {
                try
                {
                    if (AreStatesSimilar(JsonConvert.DeserializeObject<DuelGameState>(entry.Key), currentState))
                    {
                        if (entry.Value.ContainsKey(cardId) && entry.Value[cardId] > 0)
                            return true;
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

        public void AddChainLink(DuelGameState state, int cardId)
        {
            if (_chainStartState == null)
            {
                StartChain(state);
            }

            string stateHash = state.GetStateHash();
            if (!_chainResponses.ContainsKey(stateHash))
            {
                _chainResponses[stateHash] = new Dictionary<int, double>();
            }

            if (!_chainResponses[stateHash].ContainsKey(cardId))
            {
                _chainResponses[stateHash][cardId] = 0;
            }
        }
    }
}
