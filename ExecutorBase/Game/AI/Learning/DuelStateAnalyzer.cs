using System;
using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Learning
{
    public class DuelStateAnalyzer
    {
        private readonly Duel _duel;

        public DuelStateAnalyzer(Duel duel)
        {
            _duel = duel;
        }

        public double CalculateReward(ClientField bot, ClientField enemy, ClientField prevBot, ClientField prevEnemy)
        {
            double reward = 0;

            // Life point changes
            int lifeDiff = (bot.LifePoints - prevBot.LifePoints) - (enemy.LifePoints - prevEnemy.LifePoints);
            reward += lifeDiff * 0.01;

            // Board advantage
            int currentAdvantage = (bot.MonsterZone.Count(x => x != null) + bot.SpellZone.Count(x => x != null)) -
                                 (enemy.MonsterZone.Count(x => x != null) + enemy.SpellZone.Count(x => x != null));
            int prevAdvantage = (prevBot.MonsterZone.Count(x => x != null) + prevBot.SpellZone.Count(x => x != null)) -
                               (prevEnemy.MonsterZone.Count(x => x != null) + prevEnemy.SpellZone.Count(x => x != null));
            reward += (currentAdvantage - prevAdvantage) * 0.5;

            // Hand advantage
            int handDiff = (bot.Hand.Count - prevBot.Hand.Count) - (enemy.Hand.Count - prevEnemy.Hand.Count);
            reward += handDiff * 0.3;

            return reward;
        }
    }
}
