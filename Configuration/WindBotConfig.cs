using System;
using System.IO;
using Newtonsoft.Json;

namespace WindBot.Configuration
{
    public class WindBotConfig
    {
        public ServerConfig Server { get; set; }
        public DatabaseConfig Database { get; set; }
        public GameConfig Game { get; set; }
        public LoggingConfig Logging { get; set; }
        public AIConfig AI { get; set; }

        // Legacy configuration properties
        public string AssetPath { get; set; }
        public bool ServerMode { get; set; }
        public bool Train { get; set; }
        public string ReplayDir { get; set; }
        public string Deck { get; set; }
        public string Name { get; set; }
        public string DeckFile { get; set; }
        public string Dialog { get; set; } = "default"; 
        public bool Debug { get; set; }
        public bool Chat { get; set; }
        public string CreateGame { get; set; }

        private static WindBotConfig _instance;
        private static readonly object _lock = new object();

        public static WindBotConfig Instance
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

        private static WindBotConfig Load()
        {
            try
            {
                string configPath = Path.Combine(Directory.GetCurrentDirectory(), "Configuration", "appsettings.json");
                if (!File.Exists(configPath))
                {
                    Logger.WriteErrorLine($"Configuration file not found at: {configPath}");
                    return CreateDefault();
                }

                string jsonString = File.ReadAllText(configPath);
                var rootConfig = JsonConvert.DeserializeObject<RootConfig>(jsonString);
                return rootConfig?.WindBot ?? CreateDefault();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLine($"Error loading configuration: {ex.Message}");
                return CreateDefault();
            }
        }

        private static WindBotConfig CreateDefault()
        {
            return new WindBotConfig
            {
                Server = new ServerConfig(),
                Database = new DatabaseConfig(),
                Game = new GameConfig(),
                Logging = new LoggingConfig(),
                AI = new AIConfig(),
                AssetPath = "",
                ServerMode = false,
                Train = false,
                ReplayDir = "Replays",
                Dialog = "default", 
                Debug = false,
                Chat = true
            };
        }
    }

    public class ServerConfig
    {
        public int Port { get; set; } = 2399;
        public bool EnableHttps { get; set; }
        public int MaxConcurrentBots { get; set; } = 10;
        public int RequestTimeout { get; set; } = 30000;
    }

    public class DatabaseConfig
    {
        public string DefaultPath { get; set; } = "cards.cdb";
        public string[] AlternativePaths { get; set; } = new[] { "../cards.cdb", "../expansions/cards.cdb" };
    }

    public class GameConfig
    {
        public string DefaultDeckPath { get; set; } = "Decks";
        public string DefaultName { get; set; } = "WindBot";
        public int DefaultPort { get; set; } = 7911;
        public string DefaultHost { get; set; } = "127.0.0.1";
        public int MessageTimeout { get; set; } = 3000;
        public int HandshakeTimeout { get; set; } = 10000;
        public string DefaultHostInfo { get; set; } = "";
        public int DefaultVersion { get; set; } = 4946;
        public int DefaultHand { get; set; }
        public int DefaultRoomId { get; set; }
    }

    public class LoggingConfig
    {
        public string LogLevel { get; set; } = "Info";
        public bool EnableDebug { get; set; }
        public string LogFile { get; set; } = "windbot.log";
        public int MaxLogSize { get; set; } = 10485760;
        public int MaxLogFiles { get; set; } = 5;
    }

    public class AIConfig
    {
        public int ResponseDelay { get; set; } = 100;
        public int ChainDelay { get; set; } = 200;
        public string DefaultBehavior { get; set; } = "Smart";
    }

    public class RootConfig
    {
        public WindBotConfig WindBot { get; set; }
    }
}
