using System;
using System.Collections.Generic;
using System.Linq;

namespace WindBot.Game.AI.Learning
{
    public class GameState
    {
        public int PlayerLP { get; set; }
        public int OpponentLP { get; set; }
        public int CardsInHand { get; set; }
        public int CardsOnField { get; set; }
        public int OpponentCardsOnField { get; set; }
        public List<int> CardIdsInHand { get; set; }
        public List<int> CardIdsOnField { get; set; }

        public GameState()
        {
            CardIdsInHand = new List<int>();
            CardIdsOnField = new List<int>();
        }

        // Create a unique hash for this game state
        public string GetStateHash()
        {
            string stateString = $"{PlayerLP}_{OpponentLP}_{CardsInHand}_{CardsOnField}_{OpponentCardsOnField}";
            foreach (int cardId in CardIdsInHand)
                stateString += $"_h{cardId}";
            foreach (int cardId in CardIdsOnField)
                stateString += $"_f{cardId}";
            return stateString;
        }

        // Create a game state from the current duel
        public static GameState FromDuel(Duel duel)
        {
            GameState state = new GameState
            {
                PlayerLP = duel.Fields[0].LifePoints,
                OpponentLP = duel.Fields[1].LifePoints,
                CardsInHand = duel.Fields[0].Hand.Count(),
                CardsOnField = duel.Fields[0].MonsterZone.Count(x => x != null),
                OpponentCardsOnField = duel.Fields[1].MonsterZone.Count(x => x != null)
            };

            foreach (ClientCard card in duel.Fields[0].Hand)
            {
                if (card != null)
                    state.CardIdsInHand.Add(card.Id);
            }

            foreach (ClientCard card in duel.Fields[0].MonsterZone)
            {
                if (card != null)
                    state.CardIdsOnField.Add(card.Id);
            }

            return state;
        }
    }
}
