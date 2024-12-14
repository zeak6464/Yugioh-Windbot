using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("SimpleTest", "AI_SimpleTest")]
    public class SimpleTestExecutor : DefaultExecutor
    {
        public SimpleTestExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
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
            // Always go first
            return true;
        }
    }
}
