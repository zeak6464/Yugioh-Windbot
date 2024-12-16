using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using System.Linq;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;
using WindBot.Game.AI.Learning;

namespace WindBot.Game.AI.Decks
{
    public class TearlamentsExecutor : DefaultExecutor
    {
        bool spsummoned = false;
        private DuelLearningAgent learningAgent;
        private string currentState;
        
        public TearlamentsExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            learningAgent = new DuelLearningAgent();
            Logger.WriteLine("Initialized TearlamentsExecutor with learning agent");
            
            // Basic card activation logic
            AddExecutor(ExecutorType.Activate, DefaultActivateCheck);
            
            // Monster summoning logic
            AddExecutor(ExecutorType.SummonOrSet, DefaultMonsterSummon);
            
            // Spell/Trap setting logic
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            
            // Monster repositioning logic
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);

            // Common staple card effects
            AddExecutor(ExecutorType.Activate, CardId.MysticalSpaceTyphoon, DefaultMysticalSpaceTyphoon);
            AddExecutor(ExecutorType.Activate, CardId.CosmicCyclone, DefaultCosmicCyclone);
            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, CardId.CalledByTheGrave, DefaultCalledByTheGrave);
            AddExecutor(ExecutorType.Activate, CardId.InfiniteImpermanence, DefaultInfiniteImpermanence);
        }

        public override bool OnSelectHand()
        {
            // Update state before making decision
            UpdateGameState();
            return true;
        }

        private void UpdateGameState()
        {
            currentState = learningAgent.GetStateKey(Bot, Enemy);
            Logger.WriteLine($"[Learning] Current game state: {currentState}");
        }

        private void UpdateLearning(ExecutorType actionType, int cardId, float reward)
        {
            if (learningAgent == null) return;

            string nextState = learningAgent.GetStateKey(Bot, Enemy);
            var action = new ExecutorAction(actionType, cardId, () => true);
            
            learningAgent.UpdateValue(currentState, action, reward, nextState);
            currentState = nextState;
        }

        public override void OnChainEnd()
        {
            base.OnChainEnd();
            // Reward successful chain completion
            if (Duel.LastChainPlayer == 0) // If we activated the chain
            {
                UpdateLearning(ExecutorType.Activate, Duel.LastChainCards[0]?.Id ?? 0, 0.5f);
            }
        }

        public override void OnDuelEnd()
        {
            base.OnDuelEnd();
            // Reward based on game outcome
            float reward = (Bot.LifePoints > Enemy.LifePoints) ? 1.0f : -1.0f;
            UpdateLearning(ExecutorType.None, 0, reward);
            Logger.WriteLine($"[Learning] Duel ended. Final reward: {reward}");
        }

        private bool HasInList(IList<ClientCard> cards, int id)
        {
            if (cards == null || cards.Count <= 0) return false;
            return cards.Any(card => card != null && card.Id == id);
        }

        private bool IsCanSpSummon()
        {
            if ((Bot.HasInMonstersZone(CardId.ElShaddollWinda, true, false, true)
                || Enemy.HasInMonstersZone(CardId.ElShaddollWinda, true, false, true)) && spsummoned) return false;
            return true;
        }

        private void SetSpSummon()
        {
            if (Bot.HasInMonstersZone(CardId.ElShaddollWinda, true, false, true) ||
                Enemy.HasInMonstersZone(CardId.ElShaddollWinda, true, false, true)) spsummoned = true;
        }

        private List<ClientCard> GetZoneCards(CardLocation loc, ClientField player)
        {
            List<ClientCard> res = new List<ClientCard>();
            List<ClientCard> temp = new List<ClientCard>();
            if ((loc & CardLocation.Hand) > 0) { temp = player.Hand.Where(card => card != null).ToList(); if (temp.Count > 0) res.AddRange(temp); }
            if ((loc & CardLocation.MonsterZone) > 0) { temp = player.GetMonsters(); if (temp.Count > 0) res.AddRange(temp); }
            if ((loc & CardLocation.SpellZone) > 0) { temp = player.GetSpells(); if (temp.Count > 0) res.AddRange(temp); }
            if ((loc & CardLocation.Grave) > 0) { temp = player.Graveyard.Where(card => card != null).ToList(); if (temp.Count > 0) res.AddRange(temp); }
            if ((loc & CardLocation.Removed) > 0) { temp = player.Banished.Where(card => card != null).ToList(); if (temp.Count > 0) res.AddRange(temp); }
            if ((loc & CardLocation.Extra) > 0) { temp = player.ExtraDeck.Where(card => card != null).ToList(); if (temp.Count > 0) res.AddRange(temp); }
            return res;
        }
    }
}
