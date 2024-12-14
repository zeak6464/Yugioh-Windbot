using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Web;
using System.Linq;
using WindBot.Game;
using WindBot.Game.AI;
using WindBot.Game.AI.Decks;
using YGOSharp.OCGWrapper;
using System.Runtime.Serialization.Json;
using WindBot.Configuration;

namespace WindBot
{
    public class Program
    {
        public static string AssetPath { get; set; }
        public static Random Rand = new Random();

        public static void Main(string[] args)
        {
            Logger.WriteLine("WindBot starting...");

            // Set working directory to where the executable is
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(exePath);

            // Initialize configuration
            AssetPath = exePath;
            var config = WindBotConfig.Instance;

            // Override deck from command line arguments if provided
            string deckArg = args.FirstOrDefault(arg => arg.StartsWith("Deck="));
            string nameArg = args.FirstOrDefault(arg => arg.StartsWith("Name="));
            
            string deckName = deckArg?.Split('=')[1] ?? config.Deck;
            string botName = nameArg?.Split('=')[1] ?? config.Name;

            // Handle deck names with and without AI_ prefix
            if (deckName.StartsWith("AI_"))
            {
                deckName = deckName.Substring(3); // Remove AI_ prefix
            }

            // Ensure paths are absolute
            string dbPath = Path.IsPathRooted(config.Database.DefaultPath) 
                ? config.Database.DefaultPath 
                : Path.Combine(exePath, config.Database.DefaultPath);

            string decksPath = Path.IsPathRooted(config.Game.DefaultDeckPath)
                ? config.Game.DefaultDeckPath
                : Path.Combine(exePath, "Decks");

            InitDatas(dbPath, decksPath);

            // Log the deck being used
            Logger.WriteLine($"Using deck: {deckName}");

            bool serverMode = config.ServerMode;

            if (serverMode)
            {
                RunAsServer(config.Server.Port);
            }
            else if (config.Train)
            {
                string replayDir = config.ReplayDir;
                RunTrainingMode(replayDir, deckName);
            }
            else
            {
                if (args.Length == 0)
                {
                    Logger.WriteErrorLine("=== WARN ===");
                    Logger.WriteLine($"No input found, trying to connect to {config.Game.DefaultHost} YGOPro host.");
                    Logger.WriteLine("Usage: WindBot.exe ServerMode=true");
                    Logger.WriteLine("Usage: WindBot.exe ServerMode=false [Username=WindBot] [Deck=ABC] [Dialog=default] [Port=7911] [HostInfo=localhost:7922]");
                    Logger.WriteLine("Usage: WindBot.exe Train=true ReplayPath=Replay.yrp Deck=ABC");
                    Logger.WriteLine("=============");
                }

                string host = args.FirstOrDefault(arg => arg.StartsWith("Host="))?.Split('=')[1] ?? config.Game.DefaultHost;
                int port = int.Parse(args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? config.Game.DefaultPort.ToString());
                string dialog = args.FirstOrDefault(arg => arg.StartsWith("Dialog="))?.Split('=')[1] ?? config.Dialog;

                Run(host, port, botName, deckName, dialog);
            }
        }

        public static void InitDatas(string databasePath, string databasePaths)
        {
            DecksManager.Init();

            string[] dbPaths = null;
            try
            {
                if (databasePath == null && databasePaths != null)
                {
                    MemoryStream json = new MemoryStream(Convert.FromBase64String(databasePaths));
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(string[]));
                    dbPaths = serializer.ReadObject(json) as string[];
                }
            }
            catch (Exception)
            {
            }

            if (dbPaths == null)
            {
                if (databasePath == null)
                    databasePath = "cards.cdb";
                //If databasePath is an absolute path like "‪C:/ProjectIgnis/expansions/cards.cdb",
                //then Path.GetFullPath("../‪C:/ProjectIgnis/expansions/cards.cdb" would give an error,
                //due to containing a colon that's not part of a volume identifier.
                if (Path.IsPathRooted(databasePath)) dbPaths = new string[] { databasePath };
                else dbPaths = new string[]{
                Path.GetFullPath(databasePath),
                Path.GetFullPath("../" + databasePath),
                Path.GetFullPath("../expansions/" + databasePath)
            };
            }

            bool loadedone = false;
            foreach (var absPath in dbPaths)
            {
                try
                {
                    if (File.Exists(absPath))
                    {
                        NamedCardsManager.LoadDatabase(absPath);
                        Logger.DebugWriteLine("Loaded database: " + absPath + ".");
                        loadedone = true;
                    }
                } catch (Exception ex)
                {
                    Logger.WriteErrorLine("Failed loading database: " + absPath + " error: " + ex);
                }
            }
            if (!loadedone)
            {
                Logger.WriteErrorLine("Can't find cards database file.");
                Logger.WriteErrorLine("Please place cards.cdb next to WindBot.exe or Bot.exe .");
            }
        }

        private static void RunFromArgs()
        {
            WindBotInfo Info = new WindBotInfo();
            Info.Name = WindBotConfig.Instance.Name;
            Info.Deck = WindBotConfig.Instance.Deck;
            Info.DeckFile = WindBotConfig.Instance.DeckFile;
            Info.Dialog = WindBotConfig.Instance.Dialog;
            Info.Host = WindBotConfig.Instance.Game.DefaultHost;
            Info.Port = WindBotConfig.Instance.Game.DefaultPort;
            Info.HostInfo = WindBotConfig.Instance.Game.DefaultHostInfo;
            Info.Version = WindBotConfig.Instance.Game.DefaultVersion;
            Info.Hand = WindBotConfig.Instance.Game.DefaultHand;
            Info.Debug = WindBotConfig.Instance.Debug;
            Info.Chat = WindBotConfig.Instance.Chat;
            Info.RoomId = WindBotConfig.Instance.Game.DefaultRoomId;

            // Load bot configuration
            var botConfig = BotConfig.Instance;
            if (string.IsNullOrEmpty(Info.Deck) && botConfig.Bots.Count > 0)
            {
                // Randomly select a bot if no deck is specified
                var random = new Random();
                var randomBot = botConfig.Bots[random.Next(botConfig.Bots.Count)];
                Info.Name = randomBot.Name;
                Info.Deck = randomBot.Deck;
            }

            string b64CreateGame = WindBotConfig.Instance.CreateGame;
            if (b64CreateGame != null)
            {
                try
                {
                    var ms = new MemoryStream(Convert.FromBase64String(b64CreateGame));
                    var ser = new DataContractJsonSerializer(typeof(CreateGameInfo));
                    Info.CreateGame = ser.ReadObject(ms) as CreateGameInfo;
                    // "Best of 0" is not allowed by the server, use that to check for validity.
                    if (Info.CreateGame.bestOf == 0) Info.CreateGame = null;
                }
                catch (Exception ex)
                {
                    Info.CreateGame = null;
                    Logger.DebugWriteLine("Error while parsing CreateGame json: " + ex);
                }
            }
            Run(Info);
        }

        private static void RunAsServer(int ServerPort)
        {
            using (HttpListener MainServer = new HttpListener())
            {
                MainServer.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                MainServer.Prefixes.Add("http://+:" + ServerPort + "/");
                MainServer.Start();
                Logger.WriteLine("WindBot server start successed.");
                Logger.WriteLine("HTTP GET http://127.0.0.1:" + ServerPort + "/?name=WindBot&host=127.0.0.1&port=7911 to call the bot.");
                while (true)
                {
#if !DEBUG
    try
    {
#endif
                    HttpListenerContext ctx = MainServer.GetContext();

                    WindBotInfo Info = new WindBotInfo();
                    string RawUrl = Path.GetFileName(ctx.Request.RawUrl);
                    Info.Name = HttpUtility.ParseQueryString(RawUrl).Get("name");
                    Info.Deck = HttpUtility.ParseQueryString(RawUrl).Get("deck");
                    Info.Host = HttpUtility.ParseQueryString(RawUrl).Get("host");
                    string port = HttpUtility.ParseQueryString(RawUrl).Get("port");
                    if (port != null)
                        Info.Port = Int32.Parse(port);
                    string deckfile = HttpUtility.ParseQueryString(RawUrl).Get("deckfile");
                    if (deckfile != null)
                        Info.DeckFile = deckfile;
                    string dialog = HttpUtility.ParseQueryString(RawUrl).Get("dialog");
                    if (dialog != null)
                        Info.Dialog = dialog;
                    string version = HttpUtility.ParseQueryString(RawUrl).Get("version");
                    if (version != null)
                        Info.Version = Int16.Parse(version);
                    string RoomId = HttpUtility.ParseQueryString(RawUrl).Get("roomid");
                    if (RoomId != null)
                        Info.RoomId = Int32.Parse(RoomId);
                    string password = HttpUtility.ParseQueryString(RawUrl).Get("password");
                    if (password != null)
                        Info.HostInfo = password;
                    string hand = HttpUtility.ParseQueryString(RawUrl).Get("hand");
                    if (hand != null)
                        Info.Hand = Int32.Parse(hand);
                    string debug = HttpUtility.ParseQueryString(RawUrl).Get("debug");
                    if (debug != null)
                        Info.Debug= bool.Parse(debug);
                    string chat = HttpUtility.ParseQueryString(RawUrl).Get("chat");
                    if (chat != null)
                        Info.Chat = bool.Parse(chat);

                    if (Info.Name == null || Info.Host == null || port == null)
                    {
                        ctx.Response.StatusCode = 400;
                        ctx.Response.Close();
                    }
                    else
                    {
#if !DEBUG
        try
        {
#endif
                        Thread workThread = new Thread(new ParameterizedThreadStart(Run));
                        workThread.Start(Info);
#if !DEBUG
        }
        catch (Exception ex)
        {
            Logger.WriteErrorLine("Start Thread Error: " + ex);
        }
#endif
                        ctx.Response.StatusCode = 200;
                        ctx.Response.Close();
                    }
#if !DEBUG
    }
    catch (Exception ex)
    {
        Logger.WriteErrorLine("Parse Http Request Error: " + ex);
    }
#endif
                }
            }
        }

        private static void Run(object o)
        {
#if !DEBUG
            try
            {
            //all errors will be catched instead of causing the program to crash.
#endif
            WindBotInfo Info = (WindBotInfo)o;
            GameClient client = new GameClient(Info);
            client.Start();
            Logger.DebugWriteLine(client.Username + " started.");
            while (client.Connection.IsConnected)
            {
#if !DEBUG
                try
                {
#endif
                    client.Tick();
                    Thread.Sleep(30);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLine("Tick Error: " + ex);
                    client.Chat("I crashed, check the crash.log file in the WindBot folder", true);
                    using (StreamWriter sw = File.AppendText(Path.Combine(AssetPath, "crash.log")))
                    {
                        sw.WriteLine("[" + DateTime.Now.ToString("yy-MM-dd HH:mm:ss") + "] Tick Error: " + ex);
                    }
                    break;
                }
#endif
            }
            Logger.DebugWriteLine(client.Username + " disconnected.");
#if !DEBUG
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLine("Run Error: " + ex);
            }
#endif
        }

        private static void RunTrainingMode(string replayDir, string deck)
        {
            Logger.WriteLine("Starting training mode with deck: " + deck);
            Logger.WriteLine("Loading replays from: " + replayDir);

            if (!Directory.Exists(replayDir))
            {
                Logger.WriteErrorLine("Replay directory not found: " + replayDir);
                return;
            }

            var analyzer = new Game.AI.Learning.ReplayAnalyzer(replayDir);
            analyzer.LoadExistingPatterns();
            
            string[] replayFiles = Directory.GetFiles(replayDir, "*.yrp*", SearchOption.AllDirectories);
            Logger.WriteLine("Found " + replayFiles.Length + " replay files");

            foreach (string replayFile in replayFiles)
            {
                try
                {
                    Logger.WriteLine("Analyzing replay: " + Path.GetFileName(replayFile));
                    analyzer.AnalyzeReplayFile(replayFile);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLine("Error analyzing replay " + replayFile + ": " + ex.Message);
                }
            }

            analyzer.SavePatterns();
            Logger.WriteLine("Training completed. Card patterns have been updated.");
        }

        private static void Run(string host, int port, string botName, string deckName, string dialog)
        {
            WindBotInfo Info = new WindBotInfo();
            Info.Name = botName;
            Info.Deck = deckName;
            Info.Host = host;
            Info.Port = port;
            Info.Dialog = dialog;

            Run(Info);
        }
    }
}
