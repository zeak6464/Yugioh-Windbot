using System;
using System.Threading;
using WindBot.Game;
using WindBot.Game.AI;
using YGOSharp.OCGWrapper;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.IO;
using System.Globalization;

namespace WindBot
{
    public class WindBot
    {
        public WindBotInfo Info { get; private set; }
        public GameClient Game { get; private set; }

        public static void Initialize(string assetPath)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Program.Rand = new Random();
            Program.AssetPath = assetPath;
        }

        public static void AddDatabase(string databasePath)
        {
            YGOSharp.OCGWrapper.CardsManager.Init(databasePath);
        }

        public WindBot(WindBotInfo info)
        {
            Info = info;
            if (Info.IsFirst)
                Logger.WriteLine("AI: Will be first to play.");

            Game = new GameClient(Info);
            Game.OnChatReceived += OnChat;
        }

        private void OnChat(string msg)
        {
            Logger.WriteLine("Chat: " + msg);
        }
    }
}
