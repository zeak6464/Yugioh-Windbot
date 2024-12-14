using System;
using System.IO;
using Newtonsoft.Json;

namespace WindBot.Configuration
{
    public class AIRewardConfig
    {
        public RewardsConfig Rewards { get; set; }
        public LearningConfig Learning { get; set; }
        public DecisionMakingConfig DecisionMaking { get; set; }
        public MemoryManagementConfig MemoryManagement { get; set; }

        private static AIRewardConfig _instance;
        private static readonly object _lock = new object();

        public static AIRewardConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = Load();
                        }
                    }
                }
                return _instance;
            }
        }

        private static AIRewardConfig Load()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", "ai_config.json");
                if (!File.Exists(configPath))
                {
                    Logger.WriteErrorLine($"AI configuration file not found at: {configPath}");
                    return CreateDefault();
                }

                string jsonString = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<AIRewardConfig>(jsonString);
                return config ?? CreateDefault();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLine($"Error loading AI configuration: {ex.Message}");
                return CreateDefault();
            }
        }

        private static AIRewardConfig CreateDefault()
        {
            return new AIRewardConfig
            {
                Rewards = new RewardsConfig(),
                Learning = new LearningConfig(),
                DecisionMaking = new DecisionMakingConfig(),
                MemoryManagement = new MemoryManagementConfig()
            };
        }
    }

    public class RewardsConfig
    {
        public GeneralRewards General { get; set; } = new GeneralRewards();
        public FieldStateRewards FieldState { get; set; } = new FieldStateRewards();
        public LifePointRewards LifePoints { get; set; } = new LifePointRewards();
        public CardAdvantageRewards CardAdvantage { get; set; } = new CardAdvantageRewards();
        public ResourceManagementRewards ResourceManagement { get; set; } = new ResourceManagementRewards();
        public ComboRewards Combos { get; set; } = new ComboRewards();
    }

    public class GeneralRewards
    {
        public double DefaultSummon { get; set; } = 0.8;
        public double DefaultActivateSpell { get; set; } = 0.75;
        public double DefaultSetTrap { get; set; } = 0.7;
        public double DefaultAttack { get; set; } = 0.8;
        public double DefaultChain { get; set; } = 0.75;
    }

    public class FieldStateRewards
    {
        public double EmptyFieldSummon { get; set; } = 0.9;
        public double NoMonstersActivateSpell { get; set; } = 0.85;
        public double WinningPositionAttack { get; set; } = 0.9;
        public double LosingPositionActivate { get; set; } = 0.8;
    }

    public class LifePointRewards
    {
        public double GainLifePoints { get; set; } = 0.6;
        public double LoseLifePoints { get; set; } = -0.7;
        public double DealDamage { get; set; } = 0.8;
        public double LethalDamage { get; set; } = 1.0;
    }

    public class CardAdvantageRewards
    {
        public double DrawCard { get; set; } = 0.5;
        public double SearchDeck { get; set; } = 0.7;
        public double SpecialSummon { get; set; } = 0.8;
        public double DestroyOpponentCard { get; set; } = 0.9;
        public double NegateEffect { get; set; } = 0.85;
    }

    public class ResourceManagementRewards
    {
        public double UseNormalSummon { get; set; } = -0.2;
        public double UseSpecialSummon { get; set; } = -0.1;
        public double DiscardCost { get; set; } = -0.3;
        public double BanishCost { get; set; } = -0.4;
        public double LifePointCost { get; set; } = -0.01;
    }

    public class ComboRewards
    {
        public double TwoCardCombo { get; set; } = 0.9;
        public double ThreeCardCombo { get; set; } = 1.0;
        public double ArchetypeSynergy { get; set; } = 0.8;
        public double TypeSynergy { get; set; } = 0.7;
        public double AttributeSynergy { get; set; } = 0.6;
    }

    public class LearningConfig
    {
        public double ExplorationRate { get; set; } = 0.3;
        public double ExplorationDecay { get; set; } = 0.995;
        public double MinExploration { get; set; } = 0.05;
        public double LearningRate { get; set; } = 0.1;
        public double DiscountFactor { get; set; } = 0.9;
    }

    public class DecisionMakingConfig
    {
        public int MaxThinkingTime { get; set; } = 5000;
        public double MinConfidence { get; set; } = 0.6;
        public double ChainThreshold { get; set; } = 0.7;
        public double AggressivenessLevel { get; set; } = 0.8;
    }

    public class MemoryManagementConfig
    {
        public int MaxStoredGames { get; set; } = 1000;
        public int MaxStoredStates { get; set; } = 10000;
        public int SaveInterval { get; set; } = 100;
        public double PruneThreshold { get; set; } = 0.3;
    }
}
