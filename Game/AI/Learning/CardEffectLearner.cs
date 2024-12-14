using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;
using WindBot.Game;
using Newtonsoft.Json;

namespace WindBot.Game.AI.Learning
{
    public class CardEffect
    {
        public string Name { get; set; }
        public string EffectType { get; set; }
        public Dictionary<string, List<GameStateChange>> ObservedEffects { get; set; }
        public List<int> PreferredTargets { get; set; }
        public int TimesUsed { get; set; }
        public double LastReward { get; set; }
        public Dictionary<string, double> EffectOutcomes { get; set; }  // Track specific outcomes
        public List<string> KnownEffects { get; set; }  // List of effects we've learned
        public DateTime LastUsed { get; set; }

        public CardEffect()
        {
            ObservedEffects = new Dictionary<string, List<GameStateChange>>();
            PreferredTargets = new List<int>();
            TimesUsed = 0;
            LastReward = 0;
            EffectOutcomes = new Dictionary<string, double>();
            KnownEffects = new List<string>();
            LastUsed = DateTime.Now;
        }
    }

    public class GameStateChange
    {
        public DuelGameState Before { get; set; }
        public DuelGameState After { get; set; }
        public double Reward { get; set; }

        public GameStateChange()
        {
            Before = new DuelGameState();
            After = new DuelGameState();
            Reward = 0;
        }
    }

    public class CardEffectLearner : LearningBase
    {
        private readonly string EffectsFile = "card_effects.json";
        private Dictionary<int, CardEffect> _cardEffects;
        private const double EXPLORATION_BONUS = 0.3;
        private const int EXPLORATION_THRESHOLD = 5;
        private const int SAVE_INTERVAL = 10; // Save every 10 effects
        private int _effectsObservedSinceLastSave = 0;

        public CardEffectLearner() : base()
        {
            LoadEffects();
        }

        private void LoadEffects()
        {
            _cardEffects = LoadFromFile<Dictionary<int, CardEffect>>(EffectsFile);
            if (_cardEffects == null)
            {
                _cardEffects = new Dictionary<int, CardEffect>();
                Logger.WriteLine(string.Format("Created new card effects dictionary"));
            }
            else
            {
                Logger.WriteLine(string.Format("Loaded knowledge of {0} cards", _cardEffects.Count));
            }
        }

        private void SaveEffects()
        {
            if (_cardEffects != null)
            {
                SaveToFile(EffectsFile, _cardEffects);
                _effectsObservedSinceLastSave = 0;
                Logger.WriteLine(string.Format("Saved knowledge of {0} cards", _cardEffects.Count));
            }
        }

        public bool ShouldActivateEffect(ClientCard card, DuelGameState currentState)
        {
            if (card == null) return false;

            // Always try to use a card at least once
            if (!_cardEffects.ContainsKey(card.Id))
            {
                Logger.WriteLine(string.Format("New card {0} ({1}) - attempting first use", card.Id, card.Name));
                return true;
            }

            var effect = _cardEffects[card.Id];

            // Add exploration bonus for less-used cards
            double explorationBonus = 0;
            if (effect.TimesUsed < EXPLORATION_THRESHOLD)
            {
                explorationBonus = EXPLORATION_BONUS * (1.0 - (double)effect.TimesUsed / EXPLORATION_THRESHOLD);
                Logger.WriteLine(string.Format("Adding exploration bonus of {0} for {1}", explorationBonus, card.Name));
            }

            // If the card was successful last time, be more likely to use it again
            double lastUseBonus = effect.LastReward > 0 ? 0.2 : 0;

            // Calculate base activation chance from state values
            double baseChance = 0.5;  // Default 50% chance
            string stateKey = GetStateKey(currentState);
            if (effect.ObservedEffects.ContainsKey(stateKey))
            {
                var outcomes = effect.ObservedEffects[stateKey];
                if (outcomes.Any())
                {
                    baseChance = outcomes.Average(o => o.Reward);
                }
            }

            double totalChance = baseChance + explorationBonus + lastUseBonus;
            Logger.WriteLine(string.Format("Card {0} activation chance: {1} (base: {2}, exploration: {3}, last use: {4})", 
                card.Name, totalChance, baseChance, explorationBonus, lastUseBonus));

            return totalChance >= 0.4;  // Lower threshold to encourage more activation
        }

        private void LearnEffect(ClientCard card, DuelGameState before, DuelGameState after)
        {
            if (card == null) return;

            var effect = GetOrCreateCardEffect(card);
            
            // Learn about monster effects
            if (card.HasType(CardType.Monster))
            {
                if (after.MonsterCount != before.MonsterCount)
                    AddKnownEffect(effect, "Changes monster count on field");
                if (after.LifePointsDifference != before.LifePointsDifference)
                    AddKnownEffect(effect, "Affects life points");
            }

            // Learn about spell effects
            if (card.HasType(CardType.Spell))
            {
                if (after.CardsInHand != before.CardsInHand)
                    AddKnownEffect(effect, "Affects hand size");
                if (after.SpellTrapCount != before.SpellTrapCount)
                    AddKnownEffect(effect, "Affects spells/traps on field");
            }

            // Learn about trap effects
            if (card.HasType(CardType.Trap))
            {
                if (after.MonsterCount != before.MonsterCount)
                    AddKnownEffect(effect, "Affects monsters on field");
                if (after.Phase != before.Phase)
                    AddKnownEffect(effect, "Can change game phase");
            }

            effect.LastUsed = DateTime.Now;
        }

        private void AddKnownEffect(CardEffect effect, string newEffect)
        {
            if (!effect.KnownEffects.Contains(newEffect))
            {
                effect.KnownEffects.Add(newEffect);
                Logger.WriteLine(string.Format("Learned new effect for {0}: {1}", effect.Name, newEffect));
            }
        }

        private CardEffect GetOrCreateCardEffect(ClientCard card)
        {
            if (!_cardEffects.ContainsKey(card.Id))
            {
                _cardEffects[card.Id] = new CardEffect
                {
                    Name = card.Name,
                    EffectType = GetEffectType(card)
                };
            }
            return _cardEffects[card.Id];
        }

        public void UpdateCardKnowledge(ClientCard card, DuelGameState before, DuelGameState after, double reward)
        {
            if (card == null) return;

            var effect = GetOrCreateCardEffect(card);
            effect.TimesUsed++;
            effect.LastReward = reward;

            // Learn what the effect does
            LearnEffect(card, before, after);

            // Track the outcome in the current game state
            string stateKey = GetStateKey(before);
            if (!effect.ObservedEffects.ContainsKey(stateKey))
            {
                effect.ObservedEffects[stateKey] = new List<GameStateChange>();
            }

            effect.ObservedEffects[stateKey].Add(new GameStateChange
            {
                Before = before,
                After = after,
                Reward = reward
            });

            // Update success rate for this type of situation
            string situationKey = GetSituationKey(before);
            if (!effect.EffectOutcomes.ContainsKey(situationKey))
            {
                effect.EffectOutcomes[situationKey] = 0;
            }
            effect.EffectOutcomes[situationKey] = (effect.EffectOutcomes[situationKey] * 0.9) + (reward * 0.1);

            _effectsObservedSinceLastSave++;
            if (_effectsObservedSinceLastSave >= SAVE_INTERVAL)
            {
                SaveEffects();
            }

            Logger.WriteLine(string.Format("Updated knowledge of {0}: {1} known effects, used {2} times", card.Name, effect.KnownEffects.Count, effect.TimesUsed));
        }

        private string GetSituationKey(DuelGameState state)
        {
            return string.Format("Phase_{0}_OppMon_{1}_OppST_{2}", state.Phase, state.EnemyMonsterCount, state.EnemySpellTrapCount);
        }

        private string GetStateKey(DuelGameState state)
        {
            return JsonConvert.SerializeObject(state);
        }

        private string GetEffectType(ClientCard card)
        {
            if (card.HasType(CardType.Monster)) return "Monster";
            if (card.HasType(CardType.Spell)) return "Spell";
            if (card.HasType(CardType.Trap)) return "Trap";
            return "Unknown";
        }

        public override DuelGameState CaptureGameState(Duel duel, int player)
        {
            var state = new DuelGameState
            {
                Phase = duel.Phase,
                Turn = duel.Turn,
                Player = player,
                MonsterCount = duel.Fields[player].MonsterZone.Count(card => card != null),
                SpellTrapCount = duel.Fields[player].SpellZone.Count(card => card != null),
                CardsInHand = duel.Fields[player].Hand.Count,
                LifePointsDifference = duel.Fields[player].LifePoints - duel.Fields[1-player].LifePoints,
                EnemyMonsterCount = duel.Fields[1-player].MonsterZone.Count(card => card != null),
                EnemySpellTrapCount = duel.Fields[1-player].SpellZone.Count(card => card != null),
                DeckCount = duel.Fields[player].Deck.Count,
                EnemyDeckCount = duel.Fields[1-player].Deck.Count,
                GraveyardCount = duel.Fields[player].Graveyard.Count,
                EnemyGraveyardCount = duel.Fields[1-player].Graveyard.Count
            };

            return state;
        }
    }
}
