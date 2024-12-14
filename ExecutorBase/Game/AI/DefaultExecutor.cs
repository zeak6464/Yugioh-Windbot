using System;
using System.Collections.Generic;
using System.Linq;
using WindBot.Game.AI.Enums;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game.AI.Learning;
using Newtonsoft.Json;
using WindBot.Game.AI;
using System.IO;

namespace WindBot.Game.AI
{
    public class DefaultExecutor : Executor
    {
        protected new GameAI AI { get; private set; }
        protected new Duel Duel { get; private set; }
        protected new ClientField Bot { get; private set; }
        protected new ClientField Enemy { get; private set; }
        protected DuelStateAnalyzer StateAnalyzer { get; private set; }
        protected CardEffectLearner EffectLearner { get; private set; }
        protected ChainSequenceLearner ChainLearner { get; private set; }
        private DuelGameState _lastState;

        public DefaultExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
            AI = ai;
            Duel = duel;
            Bot = Duel.Fields[0];
            Enemy = Duel.Fields[1];
            StateAnalyzer = new DuelStateAnalyzer(Duel);
            EffectLearner = new CardEffectLearner();
            ChainLearner = new ChainSequenceLearner();
            
            AI.Log(LogLevel.Info, "Initializing DefaultExecutor with learning...");
            
            // Add default executors with always-true condition
            AddExecutor(ExecutorType.Activate, -1, DefaultActivateCheck); // Try activation first
            AddExecutor(ExecutorType.SpellSet, -1, DefaultSpellSetCheck); // Then set spells/traps
            AddExecutor(ExecutorType.MonsterSet, -1, DefaultMonsterSetCheck); // Then set monsters
            AddExecutor(ExecutorType.Summon, -1, DefaultSummonCheck);  // Then summon
            AddExecutor(ExecutorType.SpSummon, -1, DefaultSpSummonCheck); // Then special summon
            AddExecutor(ExecutorType.Repos, -1, DefaultReposCheck);    // Then reposition
            
            AI.Log(LogLevel.Info, "Added default executors with learning-based checks");
        }

        protected virtual bool DefaultActivateCheck()
        {
            // Check each card in hand, monster zone, and spell/trap zone
            var allCards = new List<ClientCard>();
            foreach (var card in Bot.Hand)
            {
                if (card != null)
                {
                    allCards.Add(card);
                }
            }
            foreach (var card in Bot.MonsterZone)
            {
                if (card != null)
                {
                    allCards.Add(card);
                }
            }
            foreach (var card in Bot.SpellZone)
            {
                if (card != null)
                {
                    allCards.Add(card);
                }
            }

            foreach (var card in allCards)
            {
                // Log card being considered
                string cardName = card.Name;
                if (cardName == null) cardName = "???";
                AI.Log(LogLevel.Info, string.Format("Considering activation of {0} (ID: {1})", cardName, card.Id));
                
                var currentState = EffectLearner.CaptureGameState(Duel, 0);
                if (EffectLearner.ShouldActivateEffect(card, currentState))
                {
                    AI.Log(LogLevel.Info, string.Format("Decided to activate {0}", cardName));
                    return true;
                }
            }

            return false;
        }

        protected virtual bool DefaultSpellSetCheck()
        {
            // More likely to set spells/traps early in the game
            return Duel.Turn <= 3 || Bot.Hand.Any(card => card != null && card.HasType(CardType.Trap));
        }

        protected virtual bool DefaultMonsterSetCheck()
        {
            // Set monsters if we have no field presence or for defense
            return Bot.MonsterZone.Count(card => card != null) == 0 || 
                   Enemy.MonsterZone.Any(card => card != null && card.Attack >= 2000);
        }

        protected virtual bool DefaultSummonCheck()
        {
            // Summon if we have a good attack position monster
            return Bot.Hand.Any(card => card != null && card.HasType(CardType.Monster) && card.Attack >= 1800);
        }

        protected virtual bool DefaultSpSummonCheck()
        {
            // Always try special summons
            return true;
        }

        protected virtual bool DefaultReposCheck()
        {
            // Reposition if we can get better attack/defense positioning
            return true;
        }

        public override void OnChaining(int player, ClientCard card)
        {
            var currentState = EffectLearner.CaptureGameState(Duel, player);
            if (_lastState != null && card != null)
            {
                double reward = StateAnalyzer.CalculateReward(Bot, Enemy, Bot, Enemy);
                AI.Log(LogLevel.Info, string.Format("Learning from chain: {0}, reward: {1}", card.Name, reward));
                EffectLearner.UpdateCardKnowledge(card, _lastState, currentState, reward);
            }
            _lastState = currentState;
            
            base.OnChaining(player, card);
        }

        public override void OnChainEnd()
        {
            var currentState = EffectLearner.CaptureGameState(Duel, 0);
            double reward = StateAnalyzer.CalculateReward(Bot, Enemy, Bot, Enemy);
            ChainLearner.CompleteChain(currentState, reward);
            _lastState = currentState;
            base.OnChainEnd();
        }

        public override void OnNewPhase()
        {
            var currentState = EffectLearner.CaptureGameState(Duel, 0);
            if (_lastState != null)
            {
                double reward = StateAnalyzer.CalculateReward(Bot, Enemy, Bot, Enemy);
                EffectLearner.UpdateCardKnowledge(null, _lastState, currentState, reward);
            }
            _lastState = currentState;
            base.OnNewPhase();
        }

        public override void OnSelectChain(IList<ClientCard> cards)
        {
            var currentState = EffectLearner.CaptureGameState(Duel, 0);
            var chainableCards = cards;
            var suggestedCards = ChainLearner.SuggestNextChainCards(currentState);
            
            if (suggestedCards != null && suggestedCards.Count > 0)
            {
                ClientCard bestCard = null;
                foreach (var card in chainableCards)
                {
                    if (card != null && suggestedCards.Contains(card.Id))
                    {
                        bestCard = card;
                        break;
                    }
                }
                if (bestCard != null)
                {
                    AI.SelectCard(bestCard);
                    return;
                }
            }
            
            // Default behavior if no suggestions from learning system
            if (chainableCards.Count == 0)
            {
                AI.SelectCard((ClientCard)null);
                return;
            }
            
            // Select the first chainable card as a fallback
            AI.SelectCard(chainableCards[0]);
        }

        public override IList<ClientCard> OnSelectCard(IList<ClientCard> cards, int min, int max, long hint, bool cancelable)
        {
            var selectedCards = new List<ClientCard>();
            if (cards != null && cards.Count > 0)
            {
                var currentState = EffectLearner.CaptureGameState(Duel, 0);
                var lastChainCard = Duel.CurrentChain.LastOrDefault();
                ClientCard lastCard = null;
                if (lastChainCard != null)
                {
                    lastCard = new ClientCard(lastChainCard.Card.Id, CardLocation.Hand, -1, 0);
                }
                
                if (lastCard != null)
                {
                    var clientLastCard = new ClientCard(lastCard.Id, CardLocation.Hand, -1, 0);
                    if (SelectPreferredTarget(clientLastCard, cards.ToList(), min, max))
                    {
                        for (int i = 0; i < max; i++)
                        {
                            selectedCards.Add(cards[i]);
                        }
                        foreach (var card in selectedCards)
                        {
                            AI.SelectCard(card);
                        }
                        return selectedCards;
                    }
                    else if (SelectATKTarget(clientLastCard, cards.ToList(), min, max))
                    {
                        for (int i = 0; i < max; i++)
                        {
                            selectedCards.Add(cards[i]);
                        }
                        foreach (var card in selectedCards)
                        {
                            AI.SelectCard(card);
                        }
                        return selectedCards;
                    }
                    else if (SelectDEFTarget(clientLastCard, cards.ToList(), min, max))
                    {
                        for (int i = 0; i < max; i++)
                        {
                            selectedCards.Add(cards[i]);
                        }
                        foreach (var card in selectedCards)
                        {
                            AI.SelectCard(card);
                        }
                        return selectedCards;
                    }
                }
            }
            
            return base.OnSelectCard(cards, min, max, hint, cancelable);
        }

        public class CardSorter
        {
            public ClientCard Target { get; set; }
            public int Score { get; set; }

            public CardSorter(ClientCard target, int score)
            {
                Target = target;
                Score = score;
            }
        }

        private bool SelectPreferredTarget(ClientCard card, IList<ClientCard> targets, int min, int max)
        {
            if (targets.Count <= max)
            {
                foreach (var target in targets)
                {
                    AI.SelectCard(target);
                }
                return true;
            }

            // For tributes, prefer selecting lower ATK monsters first
            if (card != null && card.HasType(CardType.Monster))
            {
                var sortedTargets = new List<CardSorter>();
                foreach (var target in targets)
                {
                    int score = target.Attack;
                    
                    // Heavily penalize tributing high ATK monsters (over 2000)
                    if (target.Attack >= 2000)
                        score -= 2000;
                        
                    // Prefer tributing monsters with lower ATK
                    if (target.Location == CardLocation.MonsterZone)
                        score += 500; // Still slightly prefer field monsters for other effects
                        
                    // Prefer tributing non-effect monsters
                    if (!target.HasType(CardType.Effect))
                        score += 1000;
                        
                    sortedTargets.Add(new CardSorter(target, score));
                }
                sortedTargets.Sort((x, y) => x.Score.CompareTo(y.Score));
                for (int i = 0; i < max && i < sortedTargets.Count; i++)
                {
                    AI.SelectCard(sortedTargets[i].Target);
                }
                return true;
            }

            // For non-tribute effects, keep original logic prioritizing high ATK
            var defaultSortedTargets = new List<CardSorter>();
            foreach (var target in targets)
            {
                int score = target.Attack * (target.Location == CardLocation.MonsterZone ? 2 : 1);
                defaultSortedTargets.Add(new CardSorter(target, score));
            }
            defaultSortedTargets.Sort((x, y) => y.Score.CompareTo(x.Score));
            for (int i = 0; i < max && i < defaultSortedTargets.Count; i++)
            {
                AI.SelectCard(defaultSortedTargets[i].Target);
            }

            return true;
        }

        private bool SelectATKTarget(ClientCard card, IList<ClientCard> targets, int min, int max)
        {
            // Select targets with highest ATK
            var sortedTargets = new List<CardSorter>();
            foreach (var target in targets)
            {
                sortedTargets.Add(new CardSorter(target, target.Attack));
            }
            sortedTargets.Sort((x, y) => y.Score.CompareTo(x.Score));
            for (int i = 0; i < max && i < sortedTargets.Count; i++)
            {
                AI.SelectCard(sortedTargets[i].Target);
            }
            return true;
        }

        private bool SelectDEFTarget(ClientCard card, IList<ClientCard> targets, int min, int max)
        {
            // Select targets with highest DEF
            var sortedTargets = new List<CardSorter>();
            foreach (var target in targets)
            {
                sortedTargets.Add(new CardSorter(target, target.Defense));
            }
            sortedTargets.Sort((x, y) => y.Score.CompareTo(x.Score));
            for (int i = 0; i < max && i < sortedTargets.Count; i++)
            {
                AI.SelectCard(sortedTargets[i].Target);
            }
            return true;
        }

        public override bool OnSelectYesNo(long desc)
        {
            var currentState = EffectLearner.CaptureGameState(Duel, 0);
            var lastCard = Duel.CurrentChain.LastOrDefault();
            
            if (lastCard != null && lastCard.Card != null)
            {
                var clientLastCard = new ClientCard(lastCard.Card.Id, CardLocation.Hand, 0, 0);
                bool shouldActivate = EffectLearner.ShouldActivateEffect(clientLastCard, currentState);
                AI.SelectYesNo(shouldActivate);
                return shouldActivate;
            }
            
            return base.OnSelectYesNo(desc);
        }

        public override bool OnPreActivate(ClientCard card)
        {
            if (card != null)
            {
                var currentState = EffectLearner.CaptureGameState(Duel, 0);
                return EffectLearner.ShouldActivateEffect(card, currentState);
            }
            return base.OnPreActivate(card);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            return positions.FirstOrDefault();
        }

        protected float CalculateReward()
        {
            float reward = 0;
            
            // Basic reward signals
            if (Duel.Fields[0].LifePoints > Duel.Fields[1].LifePoints)
                reward += 0.5f;
                
            // Reward for board control
            int ourMonsters = Duel.Fields[0].GetMonsterCount();
            int theirMonsters = Duel.Fields[1].GetMonsterCount();
            reward += (ourMonsters - theirMonsters) * 0.3f;
            
            // Extra reward for having monsters (encourages playing cards)
            reward += ourMonsters * 0.2f;
            
            // Reward for hand advantage while having board presence
            if (ourMonsters > 0)
                reward += (Duel.Fields[0].Hand.Count - Duel.Fields[1].Hand.Count) * 0.2f;
            
            // Reward for having spells/traps (encourages setting cards)
            reward += Duel.Fields[0].GetSpellCount() * 0.15f;
            
            // Big rewards for game-winning states
            if (Duel.Fields[1].LifePoints <= 0)
                reward += 2.0f;
            if (Duel.Fields[0].LifePoints <= 0)
                reward -= 2.0f;
                
            return reward;
        }

        /// <summary>
        /// Decide which card should the attacker attack.
        /// </summary>
        /// <param name="attacker">Card that attack.</param>
        /// <param name="defenders">Cards that defend.</param>
        /// <returns>BattlePhaseAction including the target, or null (in this situation, GameAI will check the next attacker)</returns>
        public override BattlePhaseAction OnSelectAttackTarget(ClientCard attacker, IList<ClientCard> defenders)
        {
            // Calculate optimal attack target based on multiple factors
            foreach (ClientCard defender in defenders)
            {
                attacker.RealPower = attacker.Attack;
                defender.RealPower = defender.GetDefensePower();

                // Skip if pre-battle conditions prevent attack
                if (!OnPreBattleBetween(attacker, defender))
                    continue;

                // Consider if destroying this monster would be strategically valuable
                bool isStrategicTarget = defender.IsMonsterDangerous() || 
                                       defender.HasType(CardType.Tuner) ||
                                       defender.IsCode(CardId.NumberS39UtopiaTheLightning) ||
                                       defender.IsCode(CardId.CrystalWingSynchroDragon);

                // Check if we can win by attacking directly instead
                bool canWinBypassingMonster = attacker.CanDirectAttack && attacker.Attack >= Enemy.LifePoints;

                // Calculate battle outcome
                bool canDestroyMonster = attacker.RealPower > defender.RealPower;
                bool willSurviveBattle = attacker.RealPower >= defender.RealPower;
                bool isWorthyTrade = defender.RealPower - attacker.RealPower <= 1000;

                // Decide whether to attack this monster
                if (canWinBypassingMonster)
                    return AI.Attack(attacker, null);
                
                if (isStrategicTarget && canDestroyMonster)
                    return AI.Attack(attacker, defender);
                    
                if (canDestroyMonster || (willSurviveBattle && isWorthyTrade))
                    return AI.Attack(attacker, defender);
            }

            // If we can't find a good attack target but can attack directly, do so
            if (attacker.CanDirectAttack)
                return AI.Attack(attacker, null);

            return null;
        }

        /// <summary>
        /// Decide whether to declare attack between attacker and defender.
        /// Can be overrided to update the RealPower of attacker for cards like Honest.
        /// </summary>
        /// <param name="attacker">Card that attack.</param>
        /// <param name="defender">Card that defend.</param>
        /// <returns>false if the attack shouldn't be done.</returns>
        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (attacker != null && defender != null)
            {
                var currentState = EffectLearner.CaptureGameState(Duel, 0);
                double reward = StateAnalyzer.CalculateReward(Bot, Enemy, Bot, Enemy);
                EffectLearner.UpdateCardKnowledge(attacker, _lastState, currentState, reward);
                _lastState = currentState;
            }
            return base.OnPreBattleBetween(attacker, defender);
        }

        private double CalculateChainReward(DuelGameState before, DuelGameState after)
        {
            if (before == null || after == null)
                return 0;

            double reward = 0;

            // Reward for life point changes
            reward += (after.LifePointsDifference - before.LifePointsDifference) * 0.01;

            // Reward for field advantage
            int beforeAdvantage = before.MonsterCount + before.SpellTrapCount;
            int afterAdvantage = after.MonsterCount + after.SpellTrapCount;
            reward += (afterAdvantage - beforeAdvantage) * 0.5;

            // Reward for hand advantage
            reward += (after.CardsInHand - before.CardsInHand) * 0.3;

            return reward;
        }

        /// <summary>
        /// Set when this card can't beat the enemies
        /// </summary>
        public override bool OnSelectMonsterSummonOrSet(ClientCard card)
        {
            return card.Level <= 4 && Bot.GetMonsters().Count(m => m.IsFaceup()) == 0 && Util.IsAllEnemyBetterThanValue(card.Attack, true);
        }

        /// <summary>
        /// Destroy face-down cards first, in our turn.
        /// </summary>
        protected bool DefaultMysticalSpaceTyphoon()
        {
            if (Duel.CurrentChain.Any(card => card.IsCode(CardId.MysticalSpaceTyphoon)))
            {
                return false;
            }

            List<ClientCard> spells = Enemy.GetSpells();
            if (spells.Count == 0)
                return false;

            ClientCard selected = Enemy.SpellZone.GetFloodgate();

            if (selected == null)
            {
                if (Duel.Player == 0)
                    selected = spells.FirstOrDefault(card => card.IsFacedown());
                if (Duel.Player == 1)
                    selected = spells.FirstOrDefault(card => card.HasType(CardType.Continuous) || card.HasType(CardType.Equip) || card.HasType(CardType.Field));
            }

            if (selected == null)
                return false;
            AI.SelectCard(selected);
            return true;
        }

        /// <summary>
        /// Destroy face-down cards first, in our turn.
        /// </summary>
        protected bool DefaultCosmicCyclone()
        {
            foreach (ClientCard card in Duel.CurrentChain)
                if (card.IsCode(CardId.CosmicCyclone))
                    return false;
            return (Bot.LifePoints > 1000) && DefaultMysticalSpaceTyphoon();
        }

        /// <summary>
        /// Activate if avail.
        /// </summary>
        protected bool DefaultGalaxyCyclone()
        {
            List<ClientCard> spells = Enemy.GetSpells();
            if (spells.Count == 0)
                return false;

            ClientCard selected = null;

            if (Card.Location == CardLocation.Grave)
            {
                selected = Util.GetBestEnemySpell(true);
            }
            else
            {
                selected = spells.FirstOrDefault(card => card.IsFacedown());
            }

            if (selected == null)
                return false;

            AI.SelectCard(selected);
            return true;
        }

        /// <summary>
        /// Set the highest ATK level 4+ effect enemy monster.
        /// </summary>
        protected bool DefaultBookOfMoon()
        {
            if (Util.IsAllEnemyBetter(true))
            {
                ClientCard monster = Enemy.GetMonsters().GetHighestAttackMonster(true);
                if (monster != null && monster.HasType(CardType.Effect) && !monster.HasType(CardType.Link) && (monster.HasType(CardType.Xyz) || monster.Level > 4))
                {
                    AI.SelectCard(monster);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Return problematic monster, and if this card become target, return any enemy monster.
        /// </summary>
        protected bool DefaultCompulsoryEvacuationDevice()
        {
            ClientCard target = Util.GetProblematicEnemyMonster(0, true);
            if (target != null)
            {
                AI.SelectCard(target);
                return true;
            }
            if (Util.IsChainTarget(Card))
            {
                ClientCard monster = Util.GetBestEnemyMonster(false, true);
                if (monster != null)
                {
                    AI.SelectCard(monster);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Revive the best monster when we don't have better one in field.
        /// </summary>
        protected bool DefaultCallOfTheHaunted()
        {
            if (!Util.IsAllEnemyBetter(true))
                return false;
            ClientCard selected = Bot.Graveyard.GetMatchingCards(card => card.IsCanRevive()).OrderByDescending(card => card.Attack).FirstOrDefault();
            AI.SelectCard(selected);
            return true;
        }

        /// <summary>
        /// Default Scapegoat effect
        /// </summary>
        protected bool DefaultScapegoat()
        {
            if (DefaultSpellWillBeNegated()) return false;
            if (Duel.Player == 0) return false;
            if (Duel.Phase == DuelPhase.End) return true;
            if (DefaultOnBecomeTarget()) return true;
            if (Duel.Phase > DuelPhase.Main1 && Duel.Phase < DuelPhase.Main2)
            {
                if (Enemy.HasInMonstersZone(CardId.UltimateConductorTytanno, true) &&
                    Enemy.HasInMonstersZone(CardId.InvokedPurgatrio, true) &&
                    Enemy.HasInMonstersZone(CardId.ChaosAncientGearGiant, true) &&
                    Enemy.HasInMonstersZone(CardId.UltimateAncientGearGolem, true) &&
                    Enemy.HasInMonstersZone(CardId.RedDragonArchfiend, true))
                    return false;
                if (Util.GetTotalAttackingMonsterAttack(1) >= Bot.LifePoints) return true;
            }
            return false;
        }
        /// <summary>
        /// Always active in opponent's turn.
        /// </summary>
        protected bool DefaultMaxxC()
        {
            return Duel.Player == 1;
        }
        /// <summary>
        /// Always disable opponent's effect except some cards like UpstartGoblin
        /// </summary>
        protected bool DefaultAshBlossomAndJoyousSpring()
        {
            int[] ignoreList = {
                CardId.MacroCosmos,
                CardId.UpstartGoblin,
                CardId.CyberEmergency
            };
            if (Util.GetLastChainCard().IsCode(ignoreList))
                return false;
            if (Util.GetLastChainCard().HasSetcode(0x11e) && Util.GetLastChainCard().Location == CardLocation.Hand) // Danger! archtype hand effect
                return false;
            return Duel.LastChainPlayer == 1;
        }
        /// <summary>
        /// Always activate unless the activating card is disabled
        /// </summary>
        protected bool DefaultGhostOgreAndSnowRabbit()
        {
            if (Util.GetLastChainCard() != null && Util.GetLastChainCard().IsDisabled())
                return false;
            return DefaultTrap();
        }
        /// <summary>
        /// Always disable opponent's effect
        /// </summary>
        protected bool DefaultGhostBelleAndHauntedMansion()
        {
            return DefaultTrap();
        }
        /// <summary>
        /// Same as DefaultBreakthroughSkill
        /// </summary>
        protected bool DefaultEffectVeiler()
        {
            ClientCard LastChainCard = Util.GetLastChainCard();
            if (LastChainCard != null && (LastChainCard.IsCode(CardId.GalaxySoldier) && Enemy.Hand.Count >= 3
                                    || LastChainCard.IsCode(CardId.EffectVeiler, CardId.InfiniteImpermanence)))
                return false;
            return DefaultBreakthroughSkill();
        }
        /// <summary>
        /// Chain common hand traps
        /// </summary>
        protected bool DefaultCalledByTheGrave()
        {
            int[] targetList =
            {
                CardId.MaxxC,
                CardId.LockBird,
                CardId.GhostOgreAndSnowRabbit,
                CardId.AshBlossom,
                CardId.GhostBelle,
                CardId.EffectVeiler,
                CardId.ArtifactLancea
            };
            if (Duel.LastChainPlayer == 1)
            {
                foreach (int id in targetList)
                {
                    if (Util.GetLastChainCard().IsCode(id))
                    {
                        AI.SelectCard(id);
                        return UniqueFaceupSpell();
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Default InfiniteImpermanence effect
        /// </summary>
        protected bool DefaultInfiniteImpermanence()
        {
            // TODO: disable s & t
            ClientCard LastChainCard = Util.GetLastChainCard();
            if (LastChainCard != null && (LastChainCard.IsCode(CardId.GalaxySoldier) && Enemy.Hand.Count >= 3
                                    || LastChainCard.IsCode(CardId.EffectVeiler, CardId.InfiniteImpermanence)))
                return false;
            return DefaultDisableMonster();
        }
        /// <summary>
        /// Chain the enemy monster, or disable monster like Rescue Rabbit.
        /// </summary>
        protected bool DefaultBreakthroughSkill()
        {
            if (!DefaultUniqueTrap())
                return false;
            return DefaultDisableMonster();
        }
        /// <summary>
        /// Chain the enemy monster, or disable monster like Rescue Rabbit.
        /// </summary>
        protected bool DefaultDisableMonster()
        {
            if (Duel.Player == 1)
            {
                ClientCard target = Enemy.MonsterZone.GetShouldBeDisabledBeforeItUseEffectMonster();
                if (target != null)
                {
                    AI.SelectCard(target);
                    return true;
                }
            }

            ClientCard LastChainCard = Util.GetLastChainCard();

            if (LastChainCard != null && LastChainCard.Controller == 1 && LastChainCard.Location == CardLocation.MonsterZone &&
                !LastChainCard.IsDisabled() && !LastChainCard.IsShouldNotBeTarget() && !LastChainCard.IsShouldNotBeSpellTrapTarget())
            {
                AI.SelectCard(LastChainCard);
                return true;
            }

            if (Bot.BattlingMonster != null && Enemy.BattlingMonster != null)
            {
                if (!Enemy.BattlingMonster.IsDisabled() && Enemy.BattlingMonster.IsCode(CardId.EaterOfMillions))
                {
                    AI.SelectCard(Enemy.BattlingMonster);
                    return true;
                }
            }

            if (Duel.Phase == DuelPhase.BattleStart && Duel.Player == 1 &&
                Enemy.HasInMonstersZone(CardId.NumberS39UtopiaTheLightning, true))
            {
                AI.SelectCard(CardId.NumberS39UtopiaTheLightning);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Activate only except this card is the target or we summon monsters.
        /// </summary>
        protected bool DefaultSolemnJudgment()
        {
            return !Util.IsChainTargetOnly(Card) && !(Duel.Player == 0 && Duel.LastChainPlayer == -1) && DefaultTrap();
        }

        /// <summary>
        /// Activate only except we summon monsters.
        /// </summary>
        protected bool DefaultSolemnWarning()
        {
            return (Bot.LifePoints > 2000) && !(Duel.Player == 0 && Duel.LastChainPlayer == -1) && DefaultTrap();
        }

        /// <summary>
        /// Activate only except we summon monsters.
        /// </summary>
        protected bool DefaultSolemnStrike()
        {
            return (Bot.LifePoints > 1500) && !(Duel.Player == 0 && Duel.LastChainPlayer == -1) && DefaultTrap();
        }

        /// <summary>
        /// Activate when all enemy monsters have better ATK.
        /// </summary>
        protected bool DefaultTorrentialTribute()
        {
            return !Util.HasChainedTrap(0) && Util.IsAllEnemyBetter(true);
        }

        /// <summary>
        /// Activate enemy have more S&T.
        /// </summary>
        protected bool DefaultHeavyStorm()
        {
            return Bot.GetSpellCount() < Enemy.GetSpellCount();
        }

        /// <summary>
        /// Activate before other winds, if enemy have more than 2 S&T.
        /// </summary>
        protected bool DefaultHarpiesFeatherDusterFirst()
        {
            return Enemy.GetSpellCount() >= 2;
        }

        /// <summary>
        /// Activate when one enemy monsters have better ATK.
        /// </summary>
        protected bool DefaultHammerShot()
        {
            return Util.IsOneEnemyBetter(true);
        }

        /// <summary>
        /// Activate when one enemy monsters have better ATK or DEF.
        /// </summary>
        protected bool DefaultDarkHole()
        {
            return Util.IsOneEnemyBetter();
        }

        /// <summary>
        /// Activate when one enemy monsters have better ATK or DEF.
        /// </summary>
        protected bool DefaultRaigeki()
        {
            return Util.IsOneEnemyBetter();
        }

        /// <summary>
        /// Activate when one enemy monsters have better ATK or DEF.
        /// </summary>
        protected bool DefaultSmashingGround()
        {
            return Util.IsOneEnemyBetter();
        }

        /// <summary>
        /// Activate when we have more than 15 cards in deck.
        /// </summary>
        protected bool DefaultPotOfDesires()
        {
            return Bot.Deck.Count > 15;
        }

        /// <summary>
        /// Set traps only and avoid block the activation of other cards.
        /// </summary>
        protected virtual bool DefaultSpellSet()
        {
            return (Card.IsTrap() || Card.HasType(CardType.QuickPlay) || DefaultSpellMustSetFirst()) && Bot.GetSpellCountWithoutField() < 4;
        }

        /// <summary>
        /// Summon with no tribute, or with tributes ATK lower.
        /// </summary>
        protected virtual bool DefaultMonsterSummon()
        {
            if (Card.Level <= 4)
                return true;

            if (!UniqueFaceupMonster())
                return false;
            int tributecount = (int)Math.Ceiling((Card.Level - 4.0d) / 2.0d);
            for (int j = 0; j < 7; ++j)
            {
                ClientCard tributeCard = Bot.MonsterZone[j];
                if (tributeCard == null) continue;
                if (tributeCard.GetDefensePower() < Card.Attack)
                    tributecount--;
            }
            return tributecount <= 0;
        }

        /// <summary>
        /// Activate when we have no field.
        /// </summary>
        protected bool DefaultField()
        {
            return Bot.SpellZone[5] == null;
        }

        /// <summary>
        /// Turn if all enemy is better.
        /// </summary>
        protected virtual bool DefaultMonsterRepos()
        {
            if (Card.IsMonsterInvincible())
                return Card.IsDefense();

            if (Card.Attack == 0)
            {
                if (Card.IsFaceup() && Card.IsAttack())
                    return true;
                if (Card.IsFaceup() && Card.IsDefense())
                    return false;
            }

            if (Enemy.HasInMonstersZone(CardId.BlueEyesChaosMAXDragon, true) &&
                Card.IsAttack() && (4000 - Card.Defense) * 2 > (4000 - Card.Attack))
                return false;
            if (Enemy.HasInMonstersZone(CardId.BlueEyesChaosMAXDragon, true) &&
                Card.IsDefense() && Card.IsFaceup() &&
                (4000 - Card.Defense) * 2 > (4000 - Card.Attack))
                return true;

            bool enemyBetter = Util.IsAllEnemyBetter();
            if (Card.IsAttack() && enemyBetter)
                return true;
            if (Card.IsDefense() && !enemyBetter && (Card.Attack >= Card.Defense || Card.Attack >= Util.GetBestPower(Enemy)))
                return true;

            return false;
        }

        /// <summary>
        /// If spell will be negated
        /// </summary>
        protected bool DefaultSpellWillBeNegated()
        {
            return (Bot.HasInSpellZone(CardId.ImperialOrder, true, true) || Enemy.HasInSpellZone(CardId.ImperialOrder, true)) && !Util.ChainContainsCard(CardId.ImperialOrder);
        }

        /// <summary>
        /// If trap will be negated
        /// </summary>
        protected bool DefaultTrapWillBeNegated()
        {
            return (Bot.HasInSpellZone(CardId.RoyalDecreel, true, true) || Enemy.HasInSpellZone(CardId.RoyalDecreel, true)) && !Util.ChainContainsCard(CardId.RoyalDecreel);
        }

        /// <summary>
        /// If spell must set first to activate
        /// </summary>
        protected bool DefaultSpellMustSetFirst()
        {
            return Bot.HasInSpellZone(CardId.AntiSpellFragrance, true, true) || Enemy.HasInSpellZone(CardId.AntiSpellFragrance, true);
        }

        /// <summary>
        /// if spell/trap is the target or enermy activate HarpiesFeatherDuster
        /// </summary>
        protected bool DefaultOnBecomeTarget()
        {
            if (Util.IsChainTarget(Card)) return true;
            int[] destroyAllList =
            {
                CardId.EvilswarmExcitonKnight,
                CardId.BlackRoseDragon,
                CardId.JudgmentDragon,
                CardId.TopologicTrisbaena,
                CardId.EvenlyMatched
            };
            int[] destroyAllOpponentList =
            {
                CardId.HarpiesFeatherDuster,
                CardId.DarkMagicAttack
            };

            if (Util.ChainContainsCard(destroyAllList)) return true;
            if (Enemy.HasInSpellZone(destroyAllOpponentList, true)) return true;
            // TODO: ChainContainsCard(id, player)
            return false;
        }
        /// <summary>
        /// Chain enemy activation or summon.
        /// </summary>
        protected bool DefaultTrap()
        {
            return (Duel.LastChainPlayer == -1 && Duel.LastSummonPlayer != 0) || Duel.LastChainPlayer == 1;
        }

        /// <summary>
        /// Activate when avail and no other our trap card in this chain or face-up.
        /// </summary>
        protected bool DefaultUniqueTrap()
        {
            if (Util.HasChainedTrap(0))
                return false;

            return UniqueFaceupSpell();
        }

        /// <summary>
        /// Check no other our spell or trap card with same name face-up.
        /// </summary>
        protected bool UniqueFaceupSpell()
        {
            return !Bot.GetSpells().Any(card => card.IsCode(Card.Id) && card.IsFaceup());
        }

        /// <summary>
        /// Check no other our monster card with same name face-up.
        /// </summary>
        protected bool UniqueFaceupMonster()
        {
            return !Bot.GetMonsters().Any(card => card.IsCode(Card.Id) && card.IsFaceup());
        }

        /// <summary>
        /// Dumb way to avoid the bot chain in mess.
        /// </summary>
        protected bool DefaultDontChainMyself()
        {
            if (Type != ExecutorType.Activate)
                return true;
            if (Executors.Any(exec => exec.Type == Type && exec.CardId == Card.Id))
                return false;
            return Duel.LastChainPlayer != 0;
        }

        /// <summary>
        /// Draw when we have lower LP, or destroy it. Can be overrided.
        /// </summary>
        protected bool DefaultChickenGame()
        {
            if (Executors.Count(exec => exec.Type == Type && exec.CardId == Card.Id) > 1)
                return false;
            if (Card.IsFacedown())
                return true;
            if (Bot.LifePoints <= 1000)
                return false;
            if (Bot.LifePoints <= Enemy.LifePoints && ActivateDescription == Util.GetStringId(CardId.ChickenGame, 0))
                return true;
            if (Bot.LifePoints > Enemy.LifePoints && ActivateDescription == Util.GetStringId(CardId.ChickenGame, 1))
                return true;
            return false;
        }

        /// <summary>
        /// Draw when we have Dark monster in hand,and banish random one. Can be overrided.
        /// </summary>
        protected bool DefaultAllureofDarkness()
        {
            ClientCard target = Bot.Hand.FirstOrDefault(card => card != null && card.HasAttribute(CardAttribute.Dark));
            return target != null;
        }

        /// <summary>
        /// Clever enough.
        /// </summary>
        protected bool DefaultDimensionalBarrier()
        {
            const int RITUAL = 0;
            const int FUSION = 1;
            const int SYNCHRO = 2;
            const int XYZ = 3;
            const int PENDULUM = 4;
            if (Duel.Player != 0)
            {
                List<ClientCard> monsters = Enemy.GetMonsters();
                int[] levels = new int[13];
                bool tuner = false;
                bool nontuner = false;
                foreach (ClientCard monster in monsters)
                {
                    if (monster.HasType(CardType.Tuner))
                        tuner = true;
                    else if (!monster.HasType(CardType.Xyz) && !monster.HasType(CardType.Link))
                    {
                        nontuner = true;
                        levels[monster.Level] = levels[monster.Level] + 1;
                    }

                    if (monster.IsOneForXyz())
                    {
                        AI.SelectOption(XYZ);
                        return true;
                    }
                }
                if (tuner && nontuner)
                {
                    AI.SelectOption(SYNCHRO);
                    return true;
                }
                for (int i=1; i<=12; i++)
                {
                    if (levels[i]>1)
                    {
                        AI.SelectOption(XYZ);
                        return true;
                    }
                }
                ClientCard l = Enemy.SpellZone[6];
                ClientCard r = Enemy.SpellZone[7];
                if (l != null && r != null && l.LScale != r.RScale)
                {
                    AI.SelectOption(PENDULUM);
                    return true;
                }
            }
            ClientCard lastchaincard = Util.GetLastChainCard();
            if (Duel.LastChainPlayer == 1 && lastchaincard != null && !lastchaincard.IsDisabled())
            {
                if (lastchaincard.HasType(CardType.Ritual))
                {
                    AI.SelectOption(RITUAL);
                    return true;
                }
                if (lastchaincard.HasType(CardType.Fusion))
                {
                    AI.SelectOption(FUSION);
                    return true;
                }
                if (lastchaincard.HasType(CardType.Synchro))
                {
                    AI.SelectOption(SYNCHRO);
                    return true;
                }
                if (lastchaincard.HasType(CardType.Xyz))
                {
                    AI.SelectOption(XYZ);
                    return true;
                }
                if (lastchaincard.IsFusionSpell())
                {
                    AI.SelectOption(FUSION);
                    return true;
                }
            }
            if (Util.IsChainTarget(Card))
            {
                AI.SelectOption(XYZ);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clever enough
        /// </summary>
        protected bool DefaultInterruptedKaijuSlumber()
        {
            if (Card.Location == CardLocation.Grave)
            {
                AI.SelectCard(
                    CardId.GamecieltheSeaTurtleKaiju,
                    CardId.KumongoustheStickyStringKaiju,
                    CardId.GadarlatheMysteryDustKaiju,
                    CardId.RadiantheMultidimensionalKaiju,
                    CardId.DogorantheMadFlameKaiju,
                    CardId.ThunderKingtheLightningstrikeKaiju,
                    CardId.JizukirutheStarDestroyingKaiju
                    );
                return true;
            }

            if (DefaultDarkHole())
            {
                AI.SelectCard(
                    CardId.JizukirutheStarDestroyingKaiju,
                    CardId.ThunderKingtheLightningstrikeKaiju,
                    CardId.DogorantheMadFlameKaiju,
                    CardId.RadiantheMultidimensionalKaiju,
                    CardId.GadarlatheMysteryDustKaiju,
                    CardId.KumongoustheStickyStringKaiju,
                    CardId.GamecieltheSeaTurtleKaiju
                    );
                AI.SelectNextCard(
                    CardId.SuperAntiKaijuWarMachineMechaDogoran,
                    CardId.GamecieltheSeaTurtleKaiju,
                    CardId.KumongoustheStickyStringKaiju,
                    CardId.GadarlatheMysteryDustKaiju,
                    CardId.RadiantheMultidimensionalKaiju,
                    CardId.DogorantheMadFlameKaiju,
                    CardId.ThunderKingtheLightningstrikeKaiju
                    );
                return true;
            }

            return false;
        }

        /// <summary>
        /// Clever enough.
        /// </summary>
        protected bool DefaultKaijuSpsummon()
        {
            IList<int> kaijus = new[] {
                CardId.JizukirutheStarDestroyingKaiju,
                CardId.GadarlatheMysteryDustKaiju,
                CardId.GamecieltheSeaTurtleKaiju,
                CardId.RadiantheMultidimensionalKaiju,
                CardId.KumongoustheStickyStringKaiju,
                CardId.ThunderKingtheLightningstrikeKaiju,
                CardId.DogorantheMadFlameKaiju,
                CardId.SuperAntiKaijuWarMachineMechaDogoran
            };
            foreach (ClientCard monster in Enemy.GetMonsters())
            {
                if (monster.IsCode(kaijus))
                    return Card.GetDefensePower() > monster.GetDefensePower();
            }
            ClientCard card = Enemy.MonsterZone.GetFloodgate();
            if (card != null)
            {
                AI.SelectCard(card);
                return true;
            }
            card = Enemy.MonsterZone.GetDangerousMonster();
            if (card != null)
            {
                AI.SelectCard(card);
                return true;
            }
            card = Util.GetOneEnemyBetterThanValue(Card.GetDefensePower());
            if (card != null)
            {
                AI.SelectCard(card);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Summon when enemy have card which we must solve.
        /// </summary>
        protected bool DefaultCastelTheSkyblasterMusketeerSummon()
        {
            return Util.GetProblematicEnemyCard() != null;
        }

        /// <summary>
        /// Bounce the problematic enemy card. Ignore the 1st effect.
        /// </summary>
        protected bool DefaultCastelTheSkyblasterMusketeerEffect()
        {
            if (ActivateDescription == Util.GetStringId(CardId.CastelTheSkyblasterMusketeer, 0))
                return false;
            ClientCard target = Util.GetProblematicEnemyCard();
            if (target != null)
            {
                AI.SelectCard(0);
                AI.SelectNextCard(target);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Summon when it should use effect, or when the attack of we is lower than enemy's, but not when enemy have monster higher than 3000.
        /// </summary>
        protected bool DefaultScarlightRedDragonArchfiendSummon()
        {
            int selfBestAttack = Util.GetBestAttack(Bot);
            int oppoBestAttack = Util.GetBestPower(Enemy);
            return (selfBestAttack <= oppoBestAttack && oppoBestAttack <= 3000) || DefaultScarlightRedDragonArchfiendEffect();
        }

        protected bool DefaultTimelordSummon()
        {
            return Bot.GetMonsterCount() == 0;
        }

        /// <summary>
        /// Activate when we have less monsters than enemy, or when enemy have more than 3 monsters.
        /// </summary>
        protected bool DefaultScarlightRedDragonArchfiendEffect()
        {
            int selfCount = Bot.GetMonsters().Count(monster => !monster.Equals(Card) && monster.IsSpecialSummoned && monster.HasType(CardType.Effect) && monster.Attack <= Card.Attack);
            int oppoCount = Enemy.GetMonsters().Count(monster => monster.IsSpecialSummoned && monster.HasType(CardType.Effect) && monster.Attack <= Card.Attack);
            return selfCount <= oppoCount && oppoCount > 0 || oppoCount >= 3;
        }

        /// <summary>
        /// Default Stardust Dragon effect
        /// </summary>
        protected bool DefaultStardustDragonEffect()
        {
            return DefaultNegateCard();
        }

        /// <summary>
        /// Default Number S39: Utopia the Lightning effect
        /// </summary>
        protected bool DefaultNumberS39UtopiaTheLightningEffect()
        {
            if (Card.Location != CardLocation.MonsterZone)
                return false;
            if (Duel.Phase != DuelPhase.BattleStep)
                return false;
            if (!Card.IsAttacking || Card.IsDisabled())
                return false;
            return true;
        }

        /// <summary>
        /// Default Number S39: Utopia the Lightning summon
        /// </summary>
        protected bool DefaultNumberS39UtopiaTheLightningSummon()
        {
            int bestPower = Util.GetBestPower(Bot);
            int oppoBestPower = Util.GetBestPower(Enemy);
            return oppoBestPower > bestPower || oppoBestPower >= 2500;
        }

        /// <summary>
        /// Default Evilswarm Exciton Knight effect
        /// </summary>
        protected bool DefaultEvilswarmExcitonKnightEffect()
        {
            return Bot.GetFieldCount() + 1 < Enemy.GetFieldCount();
        }

        /// <summary>
        /// Default Evilswarm Exciton Knight summon
        /// </summary>
        protected bool DefaultEvilswarmExcitonKnightSummon()
        {
            return Bot.GetFieldCount() + 1 < Enemy.GetFieldCount();
        }

        /// <summary>
        /// Default Stardust Dragon summon
        /// </summary>
        protected bool DefaultStardustDragonSummon()
        {
            return DefaultSynchroSummon();
        }

        /// <summary>
        /// Default method to negate cards
        /// </summary>
        protected bool DefaultNegateCard()
        {
            return (Duel.LastChainPlayer == 1 || Duel.LastChainPlayer == 0) && !DefaultOnBecomeTarget();
        }

        /// <summary>
        /// Default synchro summon logic
        /// </summary>
        protected bool DefaultSynchroSummon()
        {
            int bestPower = Util.GetBestPower(Bot);
            int oppoBestPower = Util.GetBestPower(Enemy);
            return oppoBestPower > bestPower || oppoBestPower >= 2500;
        }

        /// <summary>
        /// Default Honest effect
        /// </summary>
        protected bool DefaultHonestEffect()
        {
            if (Card.Location == CardLocation.Hand)
            {
                return Bot.BattlingMonster.IsAttack() &&
                    (((Bot.BattlingMonster.Attack < Enemy.BattlingMonster.Attack) || Bot.BattlingMonster.Attack >= Enemy.LifePoints)
                    || ((Bot.BattlingMonster.Attack < Enemy.BattlingMonster.Defense) && (Bot.BattlingMonster.Attack + Enemy.BattlingMonster.Attack > Enemy.BattlingMonster.Defense)));
            }

            return Util.IsTurn1OrMain2();
        }
    }
}
