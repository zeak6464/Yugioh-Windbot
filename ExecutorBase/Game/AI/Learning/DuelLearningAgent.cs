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
        private readonly float learningRate = 0.1f;
        private readonly float discountFactor = 0.9f;
        private readonly float explorationRate = 0.1f;
        private Random random;
        private const int SAVE_FREQUENCY = 100; // Save every 100 updates
        private int updateCounter = 0;

        public DuelLearningAgent()
        {
            stateActionValues = TrainingDataManager.LoadStateActionValues();
            random = new Random();
        }

        public string GetStateKey(ClientField playerField, ClientField opponentField)
        {
            // Create a unique key representing the game state
            // Consider: Life points, cards in hand, field presence, etc.
            return $"{playerField.LifePoints}_{opponentField.LifePoints}_" +
                   $"{playerField.Hand.Count}_{playerField.GetMonsterCount()}_" +
                   $"{playerField.GetSpellCount()}_{opponentField.GetMonsterCount()}";
        }

        public ExecutorAction ChooseAction(string stateKey, List<ExecutorAction> possibleActions)
        {
            // Epsilon-greedy action selection
            if (random.NextDouble() < explorationRate)
            {
                return possibleActions[random.Next(possibleActions.Count)];
            }

            float maxValue = float.MinValue;
            ExecutorAction bestAction = null;

            foreach (var action in possibleActions)
            {
                string stateActionKey = $"{stateKey}_{action.Type}_{action.CardId}";
                float value = stateActionValues.GetValueOrDefault(stateActionKey, 0.0f);
                
                if (value > maxValue)
                {
                    maxValue = value;
                    bestAction = action;
                }
            }

            return bestAction ?? possibleActions[0];
        }

        public void UpdateValue(string stateKey, ExecutorAction action, float reward, string nextStateKey)
        {
            string stateActionKey = $"{stateKey}_{action.Type}_{action.CardId}";
            
            if (!stateActionValues.ContainsKey(stateActionKey))
            {
                stateActionValues[stateActionKey] = 0.0f;
            }

            float currentValue = stateActionValues[stateActionKey];
            float maxNextValue = GetMaxValueForState(nextStateKey);

            // Q-learning update rule
            float newValue = currentValue + learningRate * (reward + discountFactor * maxNextValue - currentValue);
            stateActionValues[stateActionKey] = newValue;

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
                TrainingDataManager.SaveStateActionValues(stateActionValues);
                updateCounter = 0;
            }
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
                float currentValue = stateActionValues.GetValueOrDefault(exp.ActionKey, 0.0f);
                float newValue = currentValue + learningRate * (exp.Reward + discountFactor * maxNextValue - currentValue);
                stateActionValues[exp.ActionKey] = newValue;
            }
            TrainingDataManager.SaveStateActionValues(stateActionValues);
        }
    }
}
