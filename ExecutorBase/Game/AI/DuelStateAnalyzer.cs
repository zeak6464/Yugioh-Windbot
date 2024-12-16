using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI
{
    public class DuelStateAnalyzer
    {
        private readonly Duel _duel;

        public DuelStateAnalyzer(Duel duel)
        {
            _duel = duel;
        }

        public DuelGameState CaptureGameState(Duel duel, int player)
        {
            var state = new DuelGameState();

            // Set basic properties
            state.BotLifePoints = duel.Fields[0].LifePoints;
            state.EnemyLifePoints = duel.Fields[1].LifePoints;
            state.BotCardsInHand = duel.Fields[0].Hand.Count(c => c != null);
            state.EnemyCardsInHand = duel.Fields[1].Hand.Count(c => c != null);
            state.BotMonsterCount = duel.Fields[0].MonsterZone.Count(c => c != null);
            state.BotSpellTrapCount = duel.Fields[0].SpellZone.Count(c => c != null);
            state.EnemyMonsterCount = duel.Fields[1].MonsterZone.Count(c => c != null);
            state.EnemySpellTrapCount = duel.Fields[1].SpellZone.Count(c => c != null);
            state.Turn = duel.Turn;
            state.Phase = duel.Phase;
            state.Player = player;
            state.DeckCount = duel.Fields[0].Deck.Count(c => c != null);
            state.EnemyDeckCount = duel.Fields[1].Deck.Count(c => c != null);
            state.GraveyardCount = duel.Fields[0].Graveyard.Count(c => c != null);
            state.EnemyGraveyardCount = duel.Fields[1].Graveyard.Count(c => c != null);

            // Set computed properties
            state.LifePointsDifference = state.BotLifePoints - state.EnemyLifePoints;
            state.CardsInHand = state.BotCardsInHand;
            state.MonsterCount = state.BotMonsterCount;
            state.SpellTrapCount = state.BotSpellTrapCount;

            // Set board state
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

        public double CalculateReward(ClientField prevBot, ClientField prevEnemy, ClientField newBot, ClientField newEnemy)
        {
            double reward = 0;

            // Life points difference
            int prevLifePointsDiff = prevBot.LifePoints - prevEnemy.LifePoints;
            int newLifePointsDiff = newBot.LifePoints - newEnemy.LifePoints;
            reward += (newLifePointsDiff - prevLifePointsDiff) * 0.001; // Small weight for life points

            // Card advantage
            int prevCardAdvantage = (prevBot.Hand.Count(c => c != null) + prevBot.MonsterZone.Count(c => c != null) + prevBot.SpellZone.Count(c => c != null)) -
                                  (prevEnemy.Hand.Count(c => c != null) + prevEnemy.MonsterZone.Count(c => c != null) + prevEnemy.SpellZone.Count(c => c != null));
            int newCardAdvantage = (newBot.Hand.Count(c => c != null) + newBot.MonsterZone.Count(c => c != null) + newBot.SpellZone.Count(c => c != null)) -
                                 (newEnemy.Hand.Count(c => c != null) + newEnemy.MonsterZone.Count(c => c != null) + newEnemy.SpellZone.Count(c => c != null));
            reward += (newCardAdvantage - prevCardAdvantage) * 0.5; // Higher weight for card advantage

            // Field presence
            int prevFieldPresence = prevBot.MonsterZone.Count(c => c != null) - prevEnemy.MonsterZone.Count(c => c != null);
            int newFieldPresence = newBot.MonsterZone.Count(c => c != null) - newEnemy.MonsterZone.Count(c => c != null);
            reward += (newFieldPresence - prevFieldPresence) * 0.3; // Medium weight for field presence

            return reward;
        }
    }
}
