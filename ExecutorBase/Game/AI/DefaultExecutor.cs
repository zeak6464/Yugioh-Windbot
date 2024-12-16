using System;
using System.Collections.Generic;
using System.Linq;
using WindBot.Game.AI.Enums;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game.AI;
using WindBot.Game.AI.Learning;
using Newtonsoft.Json;
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
        protected DuelLearningAgent LearningAgent { get; private set; }
        private DuelGameState _lastState;
        private ExecutorAction _lastAction;
        protected new ChainSequenceLearner ChainLearner { get; set; }

        public DefaultExecutor(GameAI ai, Duel duel) : base(ai, duel)
        {
            AI = ai;
            Duel = duel;
            Bot = Duel.Fields[0];
            Enemy = Duel.Fields[1];
            StateAnalyzer = new DuelStateAnalyzer(Duel);
            ChainLearner = new ChainSequenceLearner();
            LearningAgent = new DuelLearningAgent();
            _lastState = null;
            _lastAction = null;
            
            AI.Log(LogLevel.Info, "Initializing DefaultExecutor with learning capabilities...");
            
            // Add default executors with always-true condition
            AddExecutor(ExecutorType.Activate, -1, DefaultActivateCheck); // Try activation first
            AddExecutor(ExecutorType.SpellSet, -1, DefaultSpellSetCheck); // Then set spells/traps
            AddExecutor(ExecutorType.MonsterSet, -1, DefaultMonsterSetCheck); // Then set monsters
            AddExecutor(ExecutorType.Summon, -1, DefaultSummonCheck);  // Then summon
            AddExecutor(ExecutorType.SpSummon, -1, DefaultSpSummonCheck); // Then special summon
            AddExecutor(ExecutorType.Repos, -1, DefaultReposCheck);    // Then reposition
            
            AI.Log(LogLevel.Info, "Added default executors");
        }

        protected virtual bool DefaultActivateCheck()
        {
            // Get current game state
            var currentState = GetCurrentGameState();
            
            // Check each card in hand, monster zone, and spell/trap zone
            var allCards = new List<ClientCard>();
            allCards.AddRange(Bot.Hand.Where(card => card != null));
            allCards.AddRange(Bot.MonsterZone.Where(card => card != null));
            allCards.AddRange(Bot.SpellZone.Where(card => card != null));

            foreach (var card in allCards)
            {
                // Log card being considered
                string cardName = card.Name ?? "???";
                AI.Log(LogLevel.Info, $"Considering activation of {cardName} (ID: {card.Id})");

                // Create action for this card
                var action = new ExecutorAction(ExecutorType.Activate, card.Id, () => true);

                // Get action value from learning agent
                if (LearningAgent != null)
                {
                    double actionValue = LearningAgent.GetActionValue(currentState, action);
                    AI.Log(LogLevel.Debug, $"Action value for {cardName}: {actionValue}");

                    // Update learning with previous state-action pair if exists
                    if (_lastState != null && _lastAction != null)
                    {
                        double reward = GetLearningReward();
                        LearningAgent.UpdateLearning(_lastState, _lastAction, reward, currentState);
                    }

                    // Store current state and action
                    _lastState = currentState;
                    _lastAction = action;

                    // Use action value to decide
                    if (actionValue > 0.5) // Threshold can be adjusted
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected DuelGameState GetCurrentGameState()
        {
            return new DuelGameState
            {
                BotLifePoints = Bot.LifePoints,
                EnemyLifePoints = Enemy.LifePoints,
                BotMonsterCount = Bot.MonsterZone.Count(card => card != null),
                EnemyMonsterCount = Enemy.MonsterZone.Count(card => card != null),
                CardsInHand = Bot.Hand.Count(card => card != null),
                EnemyCardsInHand = Enemy.Hand.Count,
                SpellTrapCount = Bot.SpellZone.Count(card => card != null),
                EnemySpellTrapCount = Enemy.SpellZone.Count(card => card != null),
                Turn = Duel.Turn,
                Phase = Duel.Phase
            };
        }

        protected virtual double GetLearningReward()
        {
            double reward = 0;

            // Life points difference (normalized)
            double lifePointsDiff = (Bot.LifePoints - Enemy.LifePoints) / 8000.0;
            reward += lifePointsDiff;

            // Field advantage
            int fieldAdvantage = Bot.MonsterZone.Count(card => card != null) - Enemy.MonsterZone.Count(card => card != null);
            reward += fieldAdvantage * 0.2;

            // Hand advantage
            int handAdvantage = Bot.Hand.Count(card => card != null) - Enemy.Hand.Count;
            reward += handAdvantage * 0.1;

            // Additional rewards for specific game states
            if (Bot.LifePoints > 0 && Enemy.LifePoints <= 0)
                reward += 1.0; // Win condition
            else if (Bot.LifePoints <= 0)
                reward -= 1.0; // Loss condition

            // Card quality in hand (basic approximation)
            reward += Bot.Hand.Count(card => card != null && card.Attack >= 2000) * 0.1;
            
            // Spell/Trap advantage
            int spellTrapAdvantage = Bot.SpellZone.Count(card => card != null) - Enemy.SpellZone.Count(card => card != null);
            reward += spellTrapAdvantage * 0.1;

            return reward;
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

        public override void OnChaining(ClientCard card, int player)
        {
            base.OnChaining(card, player);

            if (player == 0) // If it's our activation
            {
                var gameState = DuelGameState.FromDuel(Duel);
                ChainLearner.StartChain(gameState);
            }
        }

        public override void OnChainEnd()
        {
            var currentState = StateAnalyzer.CaptureGameState(Duel, 0);
            double reward = StateAnalyzer.CalculateReward(Bot, Enemy, Bot, Enemy);
            _lastState = currentState;
            base.OnChainEnd();
        }

        public override void OnNewPhase()
        {
            var currentState = StateAnalyzer.CaptureGameState(Duel, 0);
            if (_lastState != null)
            {
                double reward = StateAnalyzer.CalculateReward(Bot, Enemy, Bot, Enemy);
                _lastState = currentState;
            }
            _lastState = currentState;
            base.OnNewPhase();
        }

        public override void OnSelectChain(IList<ClientCard> cards)
        {
            var currentState = StateAnalyzer.CaptureGameState(Duel, 0);
            var chainableCards = cards;
            
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
                var currentState = StateAnalyzer.CaptureGameState(Duel, 0);
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
            var currentState = StateAnalyzer.CaptureGameState(Duel, 0);
            var lastCard = Duel.CurrentChain.LastOrDefault();
            
            if (lastCard != null && lastCard.Card != null)
            {
                var clientLastCard = new ClientCard(lastCard.Card.Id, CardLocation.Hand, 0, 0);
                return true;
            }
            
            return base.OnSelectYesNo(desc);
        }

        public override bool OnPreActivate(ClientCard card)
        {
            if (card != null)
            {
                var currentState = StateAnalyzer.CaptureGameState(Duel, 0);
                return true;
            }
            return base.OnPreActivate(card);
        }

        public override CardPosition OnSelectPosition(int cardId, IList<CardPosition> positions)
        {
            return positions.FirstOrDefault();
        }

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

        public override bool OnPreBattleBetween(ClientCard attacker, ClientCard defender)
        {
            if (attacker != null && defender != null)
            {
                var currentState = StateAnalyzer.CaptureGameState(Duel, 0);
                double reward = StateAnalyzer.CalculateReward(Bot, Enemy, Bot, Enemy);
                _lastState = currentState;
            }
            return base.OnPreBattleBetween(attacker, defender);
        }

        protected virtual bool DefaultMysticalSpaceTyphoon()
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

        protected virtual bool DefaultCosmicCyclone()
        {
            foreach (ClientCard card in Duel.CurrentChain)
                if (card.IsCode(CardId.CosmicCyclone))
                    return false;
            return (Bot.LifePoints > 1000) && DefaultMysticalSpaceTyphoon();
        }

        protected virtual bool DefaultGalaxyCyclone()
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

        protected virtual bool DefaultBookOfMoon()
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

        protected virtual bool DefaultCompulsoryEvacuationDevice()
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

        protected virtual bool DefaultCallOfTheHaunted()
        {
            if (!Util.IsAllEnemyBetter(true))
                return false;
            ClientCard selected = Bot.Graveyard.GetMatchingCards(card => card.IsCanRevive()).OrderByDescending(card => card.Attack).FirstOrDefault();
            AI.SelectCard(selected);
            return true;
        }

        protected virtual bool DefaultScapegoat()
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
        protected virtual bool DefaultMaxxC()
        {
            return Duel.Player == 1;
        }
        protected virtual bool DefaultAshBlossomAndJoyousSpring()
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
        protected virtual bool DefaultGhostOgreAndSnowRabbit()
        {
            if (Util.GetLastChainCard() != null && Util.GetLastChainCard().IsDisabled())
                return false;
            return DefaultTrap();
        }
        protected virtual bool DefaultGhostBelleAndHauntedMansion()
        {
            return DefaultTrap();
        }
        protected virtual bool DefaultEffectVeiler()
        {
            ClientCard LastChainCard = Util.GetLastChainCard();
            if (LastChainCard != null && (LastChainCard.IsCode(CardId.GalaxySoldier) && Enemy.Hand.Count >= 3
                                    || LastChainCard.IsCode(CardId.EffectVeiler, CardId.InfiniteImpermanence)))
                return false;
            return DefaultBreakthroughSkill();
        }
        protected virtual bool DefaultCalledByTheGrave()
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
        protected virtual bool DefaultInfiniteImpermanence()
        {
            // TODO: disable s & t
            ClientCard LastChainCard = Util.GetLastChainCard();
            if (LastChainCard != null && (LastChainCard.IsCode(CardId.GalaxySoldier) && Enemy.Hand.Count >= 3
                                    || LastChainCard.IsCode(CardId.EffectVeiler, CardId.InfiniteImpermanence)))
                return false;
            return DefaultDisableMonster();
        }
        protected virtual bool DefaultBreakthroughSkill()
        {
            if (!DefaultUniqueTrap())
                return false;
            return DefaultDisableMonster();
        }
        protected virtual bool DefaultDisableMonster()
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

        protected virtual bool DefaultSolemnJudgment()
        {
            return !Util.IsChainTargetOnly(Card) && !(Duel.Player == 0 && Duel.LastChainPlayer == -1) && DefaultTrap();
        }

        protected virtual bool DefaultSolemnWarning()
        {
            return (Bot.LifePoints > 2000) && !(Duel.Player == 0 && Duel.LastChainPlayer == -1) && DefaultTrap();
        }

        protected virtual bool DefaultSolemnStrike()
        {
            return (Bot.LifePoints > 1500) && !(Duel.Player == 0 && Duel.LastChainPlayer == -1) && DefaultTrap();
        }

        protected virtual bool DefaultTorrentialTribute()
        {
            return !Util.HasChainedTrap(0) && Util.IsAllEnemyBetter(true);
        }

        protected virtual bool DefaultHeavyStorm()
        {
            return Bot.GetSpellCount() < Enemy.GetSpellCount();
        }

        protected virtual bool DefaultHarpiesFeatherDusterFirst()
        {
            return Enemy.GetSpellCount() >= 2;
        }

        protected virtual bool DefaultHammerShot()
        {
            return Util.IsOneEnemyBetter(true);
        }

        protected virtual bool DefaultDarkHole()
        {
            return Util.IsOneEnemyBetter();
        }

        protected virtual bool DefaultRaigeki()
        {
            return Util.IsOneEnemyBetter();
        }

        protected virtual bool DefaultSmashingGround()
        {
            return Util.IsOneEnemyBetter();
        }

        protected virtual bool DefaultPotOfDesires()
        {
            return Bot.Deck.Count > 15;
        }

        protected virtual bool DefaultSpellSet()
        {
            return (Card.IsTrap() || Card.HasType(CardType.QuickPlay) || DefaultSpellMustSetFirst()) && Bot.GetSpellCountWithoutField() < 4;
        }

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

        protected virtual bool DefaultField()
        {
            return Bot.SpellZone[5] == null;
        }

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

        protected bool DefaultSpellWillBeNegated()
        {
            return (Bot.HasInSpellZone(CardId.ImperialOrder, true, true) || Enemy.HasInSpellZone(CardId.ImperialOrder, true)) && !Util.ChainContainsCard(CardId.ImperialOrder);
        }

        protected bool DefaultTrapWillBeNegated()
        {
            return (Bot.HasInSpellZone(CardId.RoyalDecreel, true, true) || Enemy.HasInSpellZone(CardId.RoyalDecreel, true)) && !Util.ChainContainsCard(CardId.RoyalDecreel);
        }

        protected bool DefaultSpellMustSetFirst()
        {
            return Bot.HasInSpellZone(CardId.AntiSpellFragrance, true, true) || Enemy.HasInSpellZone(CardId.AntiSpellFragrance, true);
        }

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
        protected bool DefaultTrap()
        {
            return (Duel.LastChainPlayer == -1 && Duel.LastSummonPlayer != 0) || Duel.LastChainPlayer == 1;
        }

        protected bool DefaultUniqueTrap()
        {
            if (Util.HasChainedTrap(0))
                return false;

            return UniqueFaceupSpell();
        }

        protected bool UniqueFaceupSpell()
        {
            return !Bot.GetSpells().Any(card => card.IsCode(Card.Id) && card.IsFaceup());
        }

        protected bool UniqueFaceupMonster()
        {
            return !Bot.GetMonsters().Any(card => card.IsCode(Card.Id) && card.IsFaceup());
        }

        protected bool DefaultDontChainMyself()
        {
            if (Type != ExecutorType.Activate)
                return true;
            if (Executors.Any(exec => exec.Type == Type && exec.CardId == Card.Id))
                return false;
            return Duel.LastChainPlayer != 0;
        }

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

        protected bool DefaultAllureofDarkness()
        {
            ClientCard target = Bot.Hand.FirstOrDefault(card => card != null && card.HasAttribute(CardAttribute.Dark));
            return target != null;
        }

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

        protected bool DefaultCastelTheSkyblasterMusketeerSummon()
        {
            return Util.GetProblematicEnemyCard() != null;
        }

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

        protected bool DefaultScarlightRedDragonArchfiendEffect()
        {
            int selfCount = Bot.GetMonsters().Count(monster => !monster.Equals(Card) && monster.IsSpecialSummoned && monster.HasType(CardType.Effect) && monster.Attack <= Card.Attack);
            int oppoCount = Enemy.GetMonsters().Count(monster => monster.IsSpecialSummoned && monster.HasType(CardType.Effect) && monster.Attack <= Card.Attack);
            return selfCount <= oppoCount && oppoCount > 0 || oppoCount >= 3;
        }

        protected bool DefaultStardustDragonEffect()
        {
            return DefaultNegateCard();
        }

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

        protected bool DefaultNumberS39UtopiaTheLightningSummon()
        {
            int bestPower = Util.GetBestPower(Bot);
            int oppoBestPower = Util.GetBestPower(Enemy);
            return oppoBestPower > bestPower || oppoBestPower >= 2500;
        }

        protected bool DefaultEvilswarmExcitonKnightEffect()
        {
            return Bot.GetFieldCount() + 1 < Enemy.GetFieldCount();
        }

        protected bool DefaultEvilswarmExcitonKnightSummon()
        {
            return Bot.GetFieldCount() + 1 < Enemy.GetFieldCount();
        }

        protected bool DefaultStardustDragonSummon()
        {
            return DefaultSynchroSummon();
        }

        protected bool DefaultNegateCard()
        {
            return (Duel.LastChainPlayer == 1 || Duel.LastChainPlayer == 0) && !DefaultOnBecomeTarget();
        }

        protected bool DefaultSynchroSummon()
        {
            int bestPower = Util.GetBestPower(Bot);
            int oppoBestPower = Util.GetBestPower(Enemy);
            return oppoBestPower > bestPower || oppoBestPower >= 2500;
        }

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

        protected bool DefaultShouldChain()
        {
            // First check basic conditions
            if (Util.IsChainTarget(Card)) return true;

            // Get current game state
            var gameState = DuelGameState.FromDuel(Duel);

            // Check with chain learner if we have positive experience
            bool shouldRespond = ChainLearner.ShouldRespond(gameState, Card.Id);
            
            if (shouldRespond)
            {
                AI.Log(LogLevel.Info, $"Chain learner suggests responding to {Card.Name} based on past experience");
            }

            return shouldRespond;
        }

        protected virtual void UpdateLearning(ExecutorType actionType, int cardId)
        {
            if (LearningAgent == null) return;

            string currentState = LearningAgent.GetStateKey(Bot, Enemy);
            var action = new ExecutorAction(actionType, cardId, () => true);
            float reward = (float)GetLearningReward();
            string nextState = LearningAgent.GetStateKey(Bot, Enemy);
            
            LearningAgent.UpdateValue(currentState, action, reward, nextState);
        }

        public virtual void OnActionExecuted(ExecutorType type, int cardId)
        {
            UpdateLearning(type, cardId);
        }

        public virtual void OnDuelEnd()
        {
            // Base implementation does nothing
        }
    }
}
