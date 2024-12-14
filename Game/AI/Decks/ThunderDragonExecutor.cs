using YGOSharp.OCGWrapper.Enums;
using System.Collections.Generic;
using WindBot;
using WindBot.Game;
using WindBot.Game.AI;

namespace WindBot.Game.AI.Decks
{
    [Deck("ThunderDragon", "AI_ThunderDragon")]
    public class ThunderDragonExecutor : DefaultExecutor
    {

        public ThunderDragonExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {

        }

    }
}
