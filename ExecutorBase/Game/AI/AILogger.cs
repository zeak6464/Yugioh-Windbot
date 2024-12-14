using System;

namespace WindBot.Game.AI
{
    public static class AILogger
    {
        public static void WriteLine(string message)
        {
            Console.WriteLine(string.Format("[{0}] {1}", DateTime.Now.ToString("yy-MM-dd HH:mm:ss"), message));
        }

        public static void DebugWriteLine(string message)
        {
#if DEBUG
            Console.WriteLine(string.Format("[{0}] {1}", DateTime.Now.ToString("yy-MM-dd HH:mm:ss"), message));
#endif
        }

        public static void WriteErrorLine(string message)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Error.WriteLine(string.Format("[{0}] {1}", DateTime.Now.ToString("yy-MM-dd HH:mm:ss"), message));
            Console.ResetColor();
        }
    }
}
