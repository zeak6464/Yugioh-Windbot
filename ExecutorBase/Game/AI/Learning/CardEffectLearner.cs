using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using YGOSharp.OCGWrapper.Enums;

namespace WindBot.Game.AI.Learning
{
    public class GameState
    {
        public int LifePointsDifference { get; set; }
        public int CardsInHand { get; set; }
        public int MonsterCount { get; set; }
        public int SpellTrapCount { get; set; }
        public DuelPhase Phase { get; set; }
        public Dictionary<int, string> BoardState { get; set; } = new Dictionary<int, string>();

        public string GetStateHash()
        {
            return JsonConvert.SerializeObject(this);
        }

        public override bool Equals(object obj)
        {
            if (obj is GameState other)
            {
                return GetStateHash() == other.GetStateHash();
            }
            return false;
        }

        public override int GetHashCode()
        {
            return GetStateHash().GetHashCode();
        }
    }

    public class CardEffect
    {
        public int CardId { get; set; }
        public string CardName { get; set; }
        public Dictionary<string, List<GameStateChange>> ObservedEffects { get; set; }
        public double SuccessRate { get; set; }
        public int TimesUsed { get; set; }
        public List<int> CommonTargets { get; set; }
        public List<CardLocation> ValidLocations { get; set; }
        public List<string> Conditions { get; set; }

        public CardEffect()
        {
            ObservedEffects = new Dictionary<string, List<GameStateChange>>();
            CommonTargets = new List<int>();
            ValidLocations = new List<CardLocation>();
            Conditions = new List<string>();
        }
    }

    public class GameStateChange
    {
        public string Type { get; set; }
        public List<int> AffectedCards { get; set; }
        public Dictionary<string, int> StatChanges { get; set; }
        public double Reward { get; set; }

        public GameStateChange()
        {
            AffectedCards = new List<int>();
            StatChanges = new Dictionary<string, int>();
        }
    }

    public class CardEffectLearner : LearningBase
    {
        private Dictionary<int, CardEffect> _cardEffects;
        private const string EffectsFile = "card_effects.json";

        public CardEffectLearner() : base()
        {
            _cardEffects = LoadFromFile<Dictionary<int, CardEffect>>(EffectsFile);
        }

        public override GameState CaptureGameState(Duel duel, int player)
        {
            var state = new GameState
            {
                LifePointsDifference = duel.LifePoints[player] - duel.LifePoints[1 - player],
                CardsInHand = duel.Fields[player].Hand.Count,
                MonsterCount = duel.Fields[player].MonsterZone.Count(card => card != null),
                SpellTrapCount = duel.Fields[player].SpellZone.Count(card => card != null),
                Phase = duel.Phase
            };

            // Capture board state
            var boardState = new Dictionary<int, string>();
            foreach (var card in duel.Fields[player].GetCards())
            {
                if (card != null)
                {
                    boardState[card.Id] = $"{card.Name}_{card.Location}_{card.Position}";
                }
            }
            state.BoardState = boardState;

            return state;
        }

        public void ObserveEffect(ClientCard card, GameState before, GameState after, double reward)
        {
            if (!_cardEffects.ContainsKey(card.Id))
            {
                _cardEffects[card.Id] = new CardEffect
                {
                    CardId = card.Id,
                    CardName = card.Name
                };
            }

            var effect = _cardEffects[card.Id];
            var stateHash = before.GetStateHash();

            if (!effect.ObservedEffects.ContainsKey(stateHash))
            {
                effect.ObservedEffects[stateHash] = new List<GameStateChange>();
            }

            var changes = new GameStateChange
            {
                Type = DetermineEffectType(before, after),
                Reward = reward
            };

            foreach (var afterState in after.BoardState)
            {
                if (!before.BoardState.ContainsKey(afterState.Key) || 
                    before.BoardState[afterState.Key] != afterState.Value)
                {
                    var cardInfo = afterState.Value.Split(':');
                    changes.AffectedCards.Add(int.Parse(cardInfo[1]));
                }
            }

            effect.ObservedEffects[stateHash].Add(changes);
            effect.TimesUsed++;
            effect.SuccessRate = ((effect.SuccessRate * (effect.TimesUsed - 1)) + (reward > 0 ? 1 : 0)) / effect.TimesUsed;

            SaveToFile(EffectsFile, _cardEffects);
        }

        private string DetermineEffectType(GameState before, GameState after)
        {
            if (after.MonsterCount < before.MonsterCount)
                return "Destroy";
            if (after.CardsInHand > before.CardsInHand)
                return "Draw";
            if (after.MonsterCount > before.MonsterCount)
                return "Summon";
            if (after.LifePointsDifference != before.LifePointsDifference)
                return "LifePoints";
            return "Unknown";
        }

        public bool ShouldActivateEffect(ClientCard card, GameState currentState)
        {
            if (!_cardEffects.ContainsKey(card.Id))
                return true;

            var effect = _cardEffects[card.Id];
            
            if (effect.SuccessRate > 0.7 && effect.TimesUsed > 5)
                return true;

            if (Random.NextDouble() < 0.3)
                return true;

            var stateHash = currentState.GetStateHash();
            foreach (var state in effect.ObservedEffects.Keys)
            {
                if (AreStatesSimilar(JsonConvert.DeserializeObject<GameState>(state), currentState))
                {
                    var avgReward = effect.ObservedEffects[state].Average(x => x.Reward);
                    if (avgReward > 0)
                        return true;
                }
            }

            return false;
        }

        protected override bool AreStatesSimilar(GameState state1, GameState state2)
        {
            if (state1 == null || state2 == null)
                return false;

            // Compare basic metrics with some tolerance
            const int lifeDiffTolerance = 1000;
            const int cardCountTolerance = 1;

            bool basicMetricsSimilar = 
                Math.Abs(state1.LifePointsDifference - state2.LifePointsDifference) <= lifeDiffTolerance &&
                Math.Abs(state1.CardsInHand - state2.CardsInHand) <= cardCountTolerance &&
                Math.Abs(state1.MonsterCount - state2.MonsterCount) <= cardCountTolerance &&
                Math.Abs(state1.SpellTrapCount - state2.SpellTrapCount) <= cardCountTolerance;

            if (!basicMetricsSimilar)
                return false;

            // Compare board states
            var commonCards = state1.BoardState.Keys.Intersect(state2.BoardState.Keys);
            int commonCardCount = commonCards.Count();
            int totalCards = Math.Max(state1.BoardState.Count, state2.BoardState.Count);

            // Consider states similar if they share at least 70% of cards
            return commonCardCount >= totalCards * 0.7;
        }

        public List<ClientCard> GetPreferredTargets(ClientCard card, List<ClientCard> possibleTargets)
        {
            if (!_cardEffects.ContainsKey(card.Id))
                return possibleTargets;

            var effect = _cardEffects[card.Id];
            
            return possibleTargets.OrderByDescending(target => 
                effect.CommonTargets.Contains(target.Id) ? effect.SuccessRate : 0
            ).ToList();
        }
    }
}
