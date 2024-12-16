using System;
using System.Collections.Generic;
using System.IO;

namespace WindBot.Game.AI.Learning
{
    public class ReinforcementLearning
    {
        private Dictionary<string, Dictionary<string, double>> QTable;
        private readonly double LearningRate = 0.1;
        private readonly double DiscountFactor = 0.9;
        private readonly double ExplorationRate = 0.1;
        private readonly string SavePath;
        private GameState LastState;
        private ExecutorAction LastAction;

        public ReinforcementLearning(string savePath)
        {
            SavePath = savePath;
            QTable = LoadQTable();
            LastState = null;
            LastAction = null;
        }

        public void ObserveState(GameState state, ExecutorAction action, bool isEndState = false)
        {
            if (LastState != null && LastAction != null)
            {
                // Calculate reward based on life point changes and board state
                double reward = CalculateReward(LastState, state);
                
                // Update Q-value
                string stateHash = LastState.GetStateHash();
                string nextStateHash = state.GetStateHash();
                string actionKey = $"{LastAction.Type}_{LastAction.CardId}";

                if (!QTable.ContainsKey(stateHash))
                    QTable[stateHash] = new Dictionary<string, double>();
                if (!QTable.ContainsKey(nextStateHash))
                    QTable[nextStateHash] = new Dictionary<string, double>();

                if (!QTable[stateHash].ContainsKey(actionKey))
                    QTable[stateHash][actionKey] = 0;

                // Q-learning update formula
                double maxNextQ = isEndState ? 0 : GetMaxQValue(nextStateHash);
                double currentQ = QTable[stateHash][actionKey];
                QTable[stateHash][actionKey] = currentQ + LearningRate * (reward + DiscountFactor * maxNextQ - currentQ);
            }

            LastState = state;
            LastAction = action;

            // Periodically save the Q-table
            if (new Random().NextDouble() < 0.01) // 1% chance to save each update
                SaveQTable();
        }

        public ExecutorAction ChooseAction(GameState state, List<ExecutorAction> possibleActions)
        {
            string stateHash = state.GetStateHash();

            // Exploration: randomly choose an action
            if (new Random().NextDouble() < ExplorationRate)
                return possibleActions[new Random().Next(possibleActions.Count)];

            // Exploitation: choose the best known action
            if (!QTable.ContainsKey(stateHash))
                QTable[stateHash] = new Dictionary<string, double>();

            double maxQ = double.MinValue;
            ExecutorAction bestAction = possibleActions[0];

            foreach (ExecutorAction action in possibleActions)
            {
                string actionKey = $"{action.Type}_{action.CardId}";
                if (!QTable[stateHash].ContainsKey(actionKey))
                    QTable[stateHash][actionKey] = 0;

                if (QTable[stateHash][actionKey] > maxQ)
                {
                    maxQ = QTable[stateHash][actionKey];
                    bestAction = action;
                }
            }

            return bestAction;
        }

        private double CalculateReward(GameState previousState, GameState currentState)
        {
            double reward = 0;

            // Reward for reducing opponent's life points
            reward += (previousState.OpponentLP - currentState.OpponentLP) * 0.1;

            // Penalty for losing life points
            reward -= (previousState.PlayerLP - currentState.PlayerLP) * 0.1;

            // Reward for having more cards on field than opponent
            reward += (currentState.CardsOnField - currentState.OpponentCardsOnField) * 5;

            return reward;
        }

        private double GetMaxQValue(string stateHash)
        {
            if (!QTable.ContainsKey(stateHash) || QTable[stateHash].Count == 0)
                return 0;

            double maxQ = double.MinValue;
            foreach (double qValue in QTable[stateHash].Values)
            {
                if (qValue > maxQ)
                    maxQ = qValue;
            }
            return maxQ;
        }

        private Dictionary<string, Dictionary<string, double>> LoadQTable()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    string[] lines = File.ReadAllLines(SavePath);
                    Dictionary<string, Dictionary<string, double>> table = new Dictionary<string, Dictionary<string, double>>();
                    
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length == 3)
                        {
                            string stateHash = parts[0];
                            string actionKey = parts[1];
                            double value = double.Parse(parts[2]);

                            if (!table.ContainsKey(stateHash))
                                table[stateHash] = new Dictionary<string, double>();
                            
                            table[stateHash][actionKey] = value;
                        }
                    }
                    return table;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error loading Q-table: {ex.Message}");
            }
            return new Dictionary<string, Dictionary<string, double>>();
        }

        private void SaveQTable()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (var stateEntry in QTable)
                {
                    foreach (var actionEntry in stateEntry.Value)
                    {
                        lines.Add($"{stateEntry.Key}|{actionEntry.Key}|{actionEntry.Value}");
                    }
                }
                File.WriteAllLines(SavePath, lines);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error saving Q-table: {ex.Message}");
            }
        }
    }
}
