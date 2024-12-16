using System;
using System.Collections.Generic;
using System.Linq;
using WindBot.Game.AI.Enums;
using WindBot.Game;

namespace WindBot.Game.AI.Learning
{
    public class DuelLearningAgent
    {
        private Dictionary<string, float> stateActionValues;
        private Dictionary<string, Dictionary<string, double>> _qValues;
        private readonly float learningRate = 0.1f;
        private readonly float discountFactor = 0.9f;
        private readonly float explorationRate = 0.1f;
        private double _learningRate = 0.1;
        private double _discountFactor = 0.9;
        private Random random;
        private const int SAVE_FREQUENCY = 100; // Save every 100 updates
        private int updateCounter = 0;

        public DuelLearningAgent()
        {
            stateActionValues = TrainingDataManager.LoadStateActionValues();
            _qValues = new Dictionary<string, Dictionary<string, double>>();
            random = new Random();
            Logger.WriteLine($"DuelLearningAgent initialized with {stateActionValues.Count} known state-action values");
        }

        public string GetStateKey(ClientField bot, ClientField enemy)
        {
            return $"{bot.LifePoints}_{enemy.LifePoints}_{bot.GetMonsterCount()}_{enemy.GetMonsterCount()}";
        }

        private string GetStateKey(DuelGameState state)
        {
            if (state == null) return "null";
            return $"{state.BotLifePoints}_{state.EnemyLifePoints}_{state.BotMonsterCount}_{state.EnemyMonsterCount}_{state.Turn}_{state.Phase}";
        }

        public ExecutorAction ChooseAction(string stateKey, List<ExecutorAction> possibleActions)
        {
            // Epsilon-greedy action selection
            if (random.NextDouble() < explorationRate)
            {
                var randomAction = possibleActions[random.Next(possibleActions.Count)];
                Logger.WriteLine($"[Learning] Exploring: Chose random action {randomAction.Type} for card {randomAction.CardId}");
                return randomAction;
            }

            float maxValue = float.MinValue;
            ExecutorAction bestAction = null;

            foreach (var action in possibleActions)
            {
                string stateActionKey = string.Format("{0}_{1}_{2}", stateKey, action.Type, action.CardId);
                float value;
                if (!stateActionValues.TryGetValue(stateActionKey, out value))
                {
                    value = 0.0f;
                }
                
                if (value > maxValue)
                {
                    maxValue = value;
                    bestAction = action;
                }
            }

            if (bestAction != null)
            {
                Logger.WriteLine($"[Learning] Exploiting: Chose action {bestAction.Type} for card {bestAction.CardId} with value {maxValue}");
            }

            return bestAction ?? possibleActions[0];
        }

        public void UpdateValue(string stateKey, ExecutorAction action, float reward, string nextStateKey)
        {
            string stateActionKey = string.Format("{0}_{1}_{2}", stateKey, action.Type, action.CardId);
            
            float currentValue;
            if (!stateActionValues.TryGetValue(stateActionKey, out currentValue))
            {
                currentValue = 0.0f;
            }

            float maxNextValue = GetMaxValueForState(nextStateKey);

            // Q-learning update rule
            float newValue = currentValue + learningRate * (reward + discountFactor * maxNextValue - currentValue);
            stateActionValues[stateActionKey] = newValue;

            Logger.WriteLine($"[Learning] Updated value for action {action.Type} on card {action.CardId}:");
            Logger.WriteLine($"  - State: {stateKey}");
            Logger.WriteLine($"  - Reward: {reward}");
            Logger.WriteLine($"  - Old Value: {currentValue:F2} -> New Value: {newValue:F2}");

            // Save experience for replay learning
            TrainingDataManager.SaveExperience(new TrainingDataManager.ExperienceReplay
            {
                StateKey = stateKey,
                ActionKey = stateActionKey,
                Reward = reward,
                NextStateKey = nextStateKey
            });

            // Periodically save state-action values
            updateCounter++;
            if (updateCounter >= SAVE_FREQUENCY)
            {
                Logger.WriteLine($"[Learning] Saving {stateActionValues.Count} state-action values to disk");
                TrainingDataManager.SaveStateActionValues(stateActionValues);
                updateCounter = 0;
            }
        }

        public double GetActionValue(DuelGameState state, ExecutorAction action)
        {
            string stateKey = GetStateKey(state);
            string actionKey = GetActionKey(action);

            if (!_qValues.ContainsKey(stateKey))
                _qValues[stateKey] = new Dictionary<string, double>();

            if (!_qValues[stateKey].ContainsKey(actionKey))
                _qValues[stateKey][actionKey] = 0.0;

            return _qValues[stateKey][actionKey];
        }

        public void UpdateLearning(DuelGameState currentState, ExecutorAction action, double reward, DuelGameState nextState)
        {
            string stateKey = GetStateKey(currentState);
            string actionKey = GetActionKey(action);
            string nextStateKey = GetStateKey(nextState);

            if (!_qValues.ContainsKey(stateKey))
                _qValues[stateKey] = new Dictionary<string, double>();

            if (!_qValues[stateKey].ContainsKey(actionKey))
                _qValues[stateKey][actionKey] = 0.0;

            // Get max Q-value for next state
            double maxNextQ = 0.0;
            if (_qValues.ContainsKey(nextStateKey))
            {
                maxNextQ = _qValues[nextStateKey].Values.DefaultIfEmpty(0.0).Max();
            }

            // Q-learning update formula
            double oldQ = _qValues[stateKey][actionKey];
            double newQ = oldQ + _learningRate * (reward + _discountFactor * maxNextQ - oldQ);
            _qValues[stateKey][actionKey] = newQ;
        }

        private string GetActionKey(ExecutorAction action)
        {
            return $"{action.Type}_{action.CardId}";
        }

        private float GetMaxValueForState(string stateKey)
        {
            float maxValue = 0.0f;
            foreach (var entry in stateActionValues)
            {
                if (entry.Key.StartsWith(stateKey) && entry.Value > maxValue)
                {
                    maxValue = entry.Value;
                }
            }
            return maxValue;
        }

        public void SaveTrainingData()
        {
            TrainingDataManager.SaveStateActionValues(stateActionValues);
            TrainingDataManager.BackupTrainingData();
        }

        public void LearnFromExperience()
        {
            var experiences = TrainingDataManager.LoadExperiences();
            foreach (var exp in experiences)
            {
                float maxNextValue = GetMaxValueForState(exp.NextStateKey);
                float currentValue;
                if (!stateActionValues.TryGetValue(exp.ActionKey, out currentValue))
                {
                    currentValue = 0.0f;
                }
                float newValue = currentValue + learningRate * (exp.Reward + discountFactor * maxNextValue - currentValue);
                stateActionValues[exp.ActionKey] = newValue;
            }
            TrainingDataManager.SaveStateActionValues(stateActionValues);
        }
    }
}
