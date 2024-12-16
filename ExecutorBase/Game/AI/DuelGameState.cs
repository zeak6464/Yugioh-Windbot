using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI
{
    public class DuelGameState
    {
        public int BotLifePoints { get; set; }
        public int EnemyLifePoints { get; set; }
        public int BotCardsInHand { get; set; }
        public int EnemyCardsInHand { get; set; }
        public int BotMonsterCount { get; set; }
        public int BotSpellTrapCount { get; set; }
        public int EnemyMonsterCount { get; set; }
        public int EnemySpellTrapCount { get; set; }
        public int Turn { get; set; }
        public DuelPhase Phase { get; set; }
        public int Player { get; set; }
        public int DeckCount { get; set; }
        public int EnemyDeckCount { get; set; }
        public int GraveyardCount { get; set; }
        public int EnemyGraveyardCount { get; set; }

        // Properties used by learning code
        public int LifePointsDifference { get; set; }
        public int CardsInHand { get; set; }
        public int MonsterCount { get; set; }
        public int SpellTrapCount { get; set; }

        public int PlayerLP { get; set; }
        public int OpponentLP { get; set; }
        public CardLocation CardPhase { get; set; }
        public List<string> BoardState { get; set; }

        public DuelGameState()
        {
            BoardState = new List<string>();
            for (int i = 0; i < 10; i++) // 5 monster zones + 5 spell/trap zones
            {
                BoardState.Add("");
            }
        }

        public string GetStateHash()
        {
            return $"{Player}_{MonsterCount}_{SpellTrapCount}_{CardsInHand}_{LifePointsDifference}_{EnemyMonsterCount}_{EnemySpellTrapCount}_{DeckCount}_{EnemyDeckCount}_{GraveyardCount}_{EnemyGraveyardCount}_{(int)Phase}";
        }

        public string GetStateHashAlternative()
        {
            var stateString = $"{PlayerLP}_{OpponentLP}_{CardPhase}_";
            foreach (var card in BoardState.OrderBy(c => c))
            {
                stateString += $"{card}_";
            }
            return stateString;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            var other = (DuelGameState)obj;
            return BotLifePoints == other.BotLifePoints &&
                   EnemyLifePoints == other.EnemyLifePoints &&
                   BotCardsInHand == other.BotCardsInHand &&
                   EnemyCardsInHand == other.EnemyCardsInHand &&
                   BotMonsterCount == other.BotMonsterCount &&
                   BotSpellTrapCount == other.BotSpellTrapCount &&
                   EnemyMonsterCount == other.EnemyMonsterCount &&
                   EnemySpellTrapCount == other.EnemySpellTrapCount &&
                   Turn == other.Turn &&
                   Phase == other.Phase &&
                   Player == other.Player &&
                   DeckCount == other.DeckCount &&
                   EnemyDeckCount == other.EnemyDeckCount &&
                   GraveyardCount == other.GraveyardCount &&
                   EnemyGraveyardCount == other.EnemyGraveyardCount &&
                   BoardState.SequenceEqual(other.BoardState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + BotLifePoints.GetHashCode();
                hash = hash * 23 + EnemyLifePoints.GetHashCode();
                hash = hash * 23 + BotCardsInHand.GetHashCode();
                hash = hash * 23 + EnemyCardsInHand.GetHashCode();
                hash = hash * 23 + BotMonsterCount.GetHashCode();
                hash = hash * 23 + BotSpellTrapCount.GetHashCode();
                hash = hash * 23 + EnemyMonsterCount.GetHashCode();
                hash = hash * 23 + EnemySpellTrapCount.GetHashCode();
                hash = hash * 23 + Turn.GetHashCode();
                hash = hash * 23 + Phase.GetHashCode();
                hash = hash * 23 + Player.GetHashCode();
                hash = hash * 23 + DeckCount.GetHashCode();
                hash = hash * 23 + EnemyDeckCount.GetHashCode();
                hash = hash * 23 + GraveyardCount.GetHashCode();
                hash = hash * 23 + EnemyGraveyardCount.GetHashCode();
                foreach (var state in BoardState)
                {
                    hash = hash * 23 + state.GetHashCode();
                }
                return hash;
            }
        }

        public static DuelGameState FromDuel(Duel duel)
        {
            var state = new DuelGameState
            {
                PlayerLP = duel.Fields[0].LifePoints,
                OpponentLP = duel.Fields[1].LifePoints,
                Phase = duel.Phase
            };

            // Add board state information
            state.BoardState = new List<string>();
            foreach (var card in duel.Fields[0].GetCards(CardLocation.MonsterZone))
            {
                if (card != null)
                    state.BoardState.Add($"M:{card.Id}");
            }
            foreach (var card in duel.Fields[0].GetCards(CardLocation.SpellZone))
            {
                if (card != null)
                    state.BoardState.Add($"S:{card.Id}");
            }

            return state;
        }
    }
}
