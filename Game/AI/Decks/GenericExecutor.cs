using System.Collections.Generic;
using System.Linq;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Decks
{
    [Deck("Generic", "AI_Generic", "Easy")]
    public class GenericExecutor : DefaultExecutor
    {
        private Dictionary<int, CardType> _cardTypes;

        public override string Deck
        {
            get { return "AI_Generic"; }
        }

        public GenericExecutor(GameAI ai, Duel duel)
            : base(ai, duel)
        {
            _cardTypes = new Dictionary<int, CardType>();

            // Add basic executors for any deck
            AddExecutor(ExecutorType.Activate, DefaultActivateCheck);
            AddExecutor(ExecutorType.SpellSet, DefaultSpellSet);
            AddExecutor(ExecutorType.MonsterSet, DefaultMonsterSetCheck);
            AddExecutor(ExecutorType.Summon, DefaultMonsterSummon);
            AddExecutor(ExecutorType.SpSummon, DefaultSpSummonCheck);
            AddExecutor(ExecutorType.Repos, DefaultMonsterRepos);

            // Add common staple card effects
            AddExecutor(ExecutorType.Activate, CardId.MaxxC, DefaultMaxxC);
            AddExecutor(ExecutorType.Activate, CardId.AshBlossom, DefaultAshBlossomAndJoyousSpring);
            AddExecutor(ExecutorType.Activate, CardId.GhostOgreAndSnowRabbit, DefaultGhostOgreAndSnowRabbit);
            AddExecutor(ExecutorType.Activate, CardId.InfiniteImpermanence, DefaultInfiniteImpermanence);
            AddExecutor(ExecutorType.Activate, CardId.CalledByTheGrave, DefaultCalledByTheGrave);
            AddExecutor(ExecutorType.Activate, CardId.EffectVeiler, DefaultEffectVeiler);

            // Load deck and analyze card types
            string deckPath = Deck;
            var deck = Game.Deck.Load(deckPath);
            AnalyzeDeck(deck);
            
            Logger.WriteLine($"Loaded deck: {deckPath}");
        }

        private void AnalyzeDeck(Deck deck)
        {
            foreach (int cardId in deck.Cards.Concat(deck.ExtraCards))
            {
                var card = YGOSharp.OCGWrapper.Card.Get(cardId);
                if (card != null)
                {
                    _cardTypes[cardId] = (CardType)card.Type;
                }
            }
        }

        public override bool OnSelectHand()
        {
            // Always go first
            return true;
        }

        protected override bool DefaultMonsterSummon()
        {
            // Prioritize summoning monsters with higher ATK
            foreach (ClientCard card in Bot.Hand.Where(card => card.IsMonster() && card.Level <= 4))
            {
                if (card.Attack >= 1800)
                {
                    AI.SelectCard(card);
                    return true;
                }
            }

            return base.DefaultMonsterSummon();
        }

        protected override bool DefaultSpellSet()
        {
            // Set Traps and Quick-Play Spells
            foreach (ClientCard card in Bot.Hand.Where(card => card.IsSpell() || card.IsTrap()))
            {
                if (card.IsTrap() || (card.IsSpell() && card.HasType(CardType.QuickPlay)))
                {
                    AI.SelectCard(card);
                    return true;
                }
            }

            return false;
        }

        protected override bool DefaultMonsterRepos()
        {
            // Switch monsters to attack if they're stronger than opponent's monsters
            foreach (ClientCard monster in Bot.GetMonsters())
            {
                if (monster.IsDefense() && monster.Attack > monster.Defense && 
                    !Enemy.MonsterZone.Any(m => m?.Attack >= monster.Attack))
                {
                    AI.SelectCard(monster);
                    return true;
                }
            }

            return false;
        }

        protected override bool DefaultActivateCheck()
        {
            // Use spells that boost ATK or protect our monsters
            foreach (ClientCard card in Bot.Hand.Where(card => card.IsSpell()))
            {
                if (ShouldActivateSpell(card))
                {
                    AI.SelectCard(card);
                    return true;
                }
            }

            return false;
        }

        private bool ShouldActivateSpell(ClientCard card)
        {
            // Basic logic for spell activation
            if (card.IsSpell())
            {
                // Don't activate if we might get negated
                if (DefaultSpellWillBeNegated())
                    return false;

                // Activate field spells when we have none
                if (card.HasType(CardType.Field) && !Bot.SpellZone[5].HasType(CardType.Field))
                    return true;

                // Activate equip spells when we have monsters
                if (card.HasType(CardType.Equip) && Bot.GetMonsterCount() > 0)
                    return true;

                // Activate continuous spells early
                if (card.HasType(CardType.Continuous) && Bot.SpellZone.Count(c => c != null) < 4)
                    return true;
            }

            return false;
        }
    }
}
