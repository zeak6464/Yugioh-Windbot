using System;
using System.Collections.Generic;
using YGOSharp.OCGWrapper.Enums;
using Newtonsoft.Json;

namespace WindBot.Game.AI.Learning
{
    [Serializable]
    public class DuelGameState
    {
        public DuelPhase Phase { get; set; }
        public int Turn { get; set; }
        public int Player { get; set; }
        public int MonsterCount { get; set; }
        public int SpellTrapCount { get; set; }
        public int CardsInHand { get; set; }
        public int LifePointsDifference { get; set; }
        public int EnemyMonsterCount { get; set; }
        public int EnemySpellTrapCount { get; set; }
        public Dictionary<int, string> BoardState { get; set; }
        public int DeckCount { get; set; }
        public int EnemyDeckCount { get; set; }
        public int GraveyardCount { get; set; }
        public int EnemyGraveyardCount { get; set; }

        public DuelGameState()
        {
            BoardState = new Dictionary<int, string>();
        }

        public string GetStateHash()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override bool Equals(object obj)
        {
            DuelGameState other = obj as DuelGameState;
            if (other != null)
            {
                return GetStateHash() == other.GetStateHash();
            }
            return false;
        }

        public override int GetHashCode()
        {
            return GetStateHash().GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("Phase:{0} Turn:{1} LP_Diff:{2} Mon:{3}/{4} ST:{5}/{6}",
                Phase, Turn, LifePointsDifference,
                MonsterCount, EnemyMonsterCount,
                SpellTrapCount, EnemySpellTrapCount);
        }
    }
}
