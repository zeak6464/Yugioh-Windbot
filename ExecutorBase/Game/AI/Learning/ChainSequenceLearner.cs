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
        private Dictionary<string, ChainSequence> _chainSequences;
        private List<int> _currentChain;
        private GameState _chainStartState;
        private const string ChainsFile = "chain_sequences.json";

        public ChainSequenceLearner() : base()
        {
            _chainSequences = LoadFromFile<Dictionary<string, ChainSequence>>(ChainsFile);
            _currentChain = new List<int>();
        }

        public void StartChain(GameState state)
        {
            _currentChain.Clear();
            _chainStartState = state;
        }

        public void AddChainLink(int cardId)
        {
            _currentChain.Add(cardId);
        }

        public void CompleteChain(GameState finalState, double reward)
        {
            if (_currentChain.Count == 0 || _chainStartState == null)
                return;

            string sequenceKey = GetSequenceKey(_currentChain);
            if (!_chainSequences.ContainsKey(sequenceKey))
            {
                _chainSequences[sequenceKey] = new ChainSequence
                {
                    CardSequence = new List<int>(_currentChain)
                };
            }

            var sequence = _chainSequences[sequenceKey];
            sequence.TimesUsed++;
            sequence.SuccessRate = ((sequence.SuccessRate * (sequence.TimesUsed - 1)) + (reward > 0 ? 1 : 0)) / sequence.TimesUsed;

            string stateHash = _chainStartState.GetStateHash();
            if (!sequence.StateRewards.ContainsKey(stateHash))
            {
                sequence.StateRewards[stateHash] = reward;
            }
            else
            {
                sequence.StateRewards[stateHash] = (sequence.StateRewards[stateHash] + reward) / 2;
            }

            SaveToFile(ChainsFile, _chainSequences);
            _currentChain.Clear();
            _chainStartState = null;
        }

        private string GetSequenceKey(List<int> sequence)
        {
            return string.Join("-", sequence);
        }

        public bool ShouldRespond(GameState currentState, int cardId)
        {
            var relevantSequences = _chainSequences.Values
                .Where(s => s.CardSequence.Contains(cardId))
                .ToList();

            if (!relevantSequences.Any())
                return Random.NextDouble() < 0.3;

            foreach (var sequence in relevantSequences)
            {
                foreach (var state in sequence.StateRewards.Keys)
                {
                    if (AreStatesSimilar(JsonConvert.DeserializeObject<GameState>(state), currentState))
                    {
                        if (sequence.StateRewards[state] > 0 && sequence.SuccessRate > 0.5)
                            return true;
                    }
                }
            }

            return false;
        }

        public List<int> SuggestNextChainCards(GameState currentState)
        {
            if (_currentChain.Count == 0)
                return new List<int>();

            var possibleSequences = _chainSequences.Values
                .Where(s => s.CardSequence.Take(_currentChain.Count).SequenceEqual(_currentChain))
                .OrderByDescending(s => s.SuccessRate)
                .ToList();

            if (!possibleSequences.Any())
                return new List<int>();

            var bestSequence = possibleSequences.First();
            return bestSequence.CardSequence.Skip(_currentChain.Count).ToList();
        }
    }
}
