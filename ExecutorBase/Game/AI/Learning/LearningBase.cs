using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;
using Newtonsoft.Json;

namespace WindBot.Game.AI.Learning
{
    public abstract class LearningBase
    {
        protected readonly Random Random;
        protected const string TrainingDataPath = "TrainingData";

        protected LearningBase()
        {
            Random = new Random();
            System.IO.Directory.CreateDirectory(TrainingDataPath);
        }

        protected virtual void SaveToFile<T>(string filename, T data)
        {
            string path = System.IO.Path.Combine(TrainingDataPath, filename);
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            System.IO.File.WriteAllText(path, json);
        }

        protected virtual T LoadFromFile<T>(string filename) where T : new()
        {
            string path = System.IO.Path.Combine(TrainingDataPath, filename);
            if (!System.IO.File.Exists(path))
                return new T();

            string json = System.IO.File.ReadAllText(path);
            return JsonConvert.DeserializeObject<T>(json) ?? new T();
        }

        protected virtual bool AreStatesSimilar(GameState state1, GameState state2)
        {
            if (state1 == null || state2 == null)
                return false;

            return Math.Abs(state1.MonsterCount - state2.MonsterCount) <= 1 &&
                   Math.Abs(state1.SpellTrapCount - state2.SpellTrapCount) <= 1 &&
                   state1.Phase == state2.Phase;
        }

        public virtual GameState CaptureGameState(Duel duel, int player)
        {
            var state = new GameState
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
