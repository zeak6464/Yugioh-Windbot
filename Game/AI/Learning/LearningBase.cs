using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using Newtonsoft.Json;
using System.IO;

namespace WindBot.Game.AI.Learning
{
    public abstract class LearningBase
    {
        protected readonly Random Random;
        protected readonly string TrainingDataPath;

        protected LearningBase()
        {
            Random = new Random();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            TrainingDataPath = Path.Combine(baseDir, "TrainingData");
            Directory.CreateDirectory(TrainingDataPath);
        }

        protected virtual void SaveToFile<T>(string filename, T data)
        {
            try
            {
                string fullPath = Path.Combine(TrainingDataPath, filename);
                string dirPath = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dirPath))
                    Directory.CreateDirectory(dirPath);

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(fullPath, json);
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error saving training data: {ex.Message}");
            }
        }

        protected virtual T LoadFromFile<T>(string filename) where T : new()
        {
            try
            {
                string fullPath = Path.Combine(TrainingDataPath, filename);
                if (!File.Exists(fullPath))
                    return new T();

                string json = File.ReadAllText(fullPath);
                T result = JsonConvert.DeserializeObject<T>(json);
                return result == null ? new T() : result;
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"Error loading training data: {ex.Message}");
                return new T();
            }
        }

        protected virtual bool AreStatesSimilar(DuelGameState state1, DuelGameState state2)
        {
            if (state1 == null || state2 == null)
                return false;

            return Math.Abs(state1.MonsterCount - state2.MonsterCount) <= 1 &&
                   Math.Abs(state1.SpellTrapCount - state2.SpellTrapCount) <= 1 &&
                   state1.Phase == state2.Phase;
        }

        public virtual DuelGameState CaptureGameState(Duel duel, int player)
        {
            var state = new DuelGameState
            {
                LifePointsDifference = duel.Fields[player].LifePoints - duel.Fields[1-player].LifePoints,
                CardsInHand = duel.Fields[player].Hand.Count,
                MonsterCount = duel.Fields[player].MonsterZone.Count(x => x != null),
                SpellTrapCount = duel.Fields[player].SpellZone.Count(x => x != null),
                Phase = duel.Phase
            };

            for (int i = 0; i < 5; i++)
            {
                var monster = duel.Fields[player].MonsterZone[i];
                if (monster != null)
                {
                    state.BoardState[i] = string.Format("M:{0}:{1}:{2}", monster.Id, monster.Attack, monster.Defense);
                }

                var spell = duel.Fields[player].SpellZone[i];
                if (spell != null)
                {
                    state.BoardState[i+5] = string.Format("S:{0}", spell.Id);
                }
            }

            return state;
        }
    }
}
