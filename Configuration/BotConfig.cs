using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace WindBot.Configuration
{
    public class BotInfo
    {
        public string Name { get; set; }
        public string Deck { get; set; }
        public int Difficulty { get; set; }
        public List<int> MasterRules { get; set; }
    }

    public class BotConfig
    {
        private static BotConfig _instance;
        private static readonly object _lock = new object();

        public List<BotInfo> Bots { get; set; }

        public static BotConfig Instance
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

        private static BotConfig Load()
        {
            try
            {
                string configPath = Path.Combine("Configuration", "bots.json");
                if (!File.Exists(configPath))
                {
                    Logger.WriteErrorLine($"Bot configuration file not found at: {configPath}");
                    return CreateDefault();
                }

                string jsonString = File.ReadAllText(configPath);
                var bots = JsonConvert.DeserializeObject<List<BotInfo>>(jsonString);
                return new BotConfig { Bots = bots ?? new List<BotInfo>() };
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLine($"Error loading bot configuration: {ex.Message}");
                return CreateDefault();
            }
        }

        private static BotConfig CreateDefault()
        {
            return new BotConfig
            {
                Bots = new List<BotInfo>
                {
                    new BotInfo
                    {
                        Name = "Simple",
                        Deck = "Simple",
                        Difficulty = 1,
                        MasterRules = new List<int> { 3, 4, 5 }
                    }
                }
            };
        }
    }
}
