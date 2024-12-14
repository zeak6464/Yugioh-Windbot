using System;
using System.IO;

namespace WindBot.Game.AI.Learning
{
    public class DeckTrainer
    {
        private readonly ReplayAnalyzer _analyzer;
        private readonly string _replayFolder;

        public DeckTrainer(string replayFolder)
        {
            _replayFolder = replayFolder;
            _analyzer = new ReplayAnalyzer(replayFolder);
        }

        public void TrainFromReplays()
        {
            Console.WriteLine("Starting deck training from replays...");
            
            string[] replayFiles = Directory.GetFiles(_replayFolder, "*.yrp");
            int processedCount = 0;

            foreach (string replayFile in replayFiles)
            {
                try
                {
                    byte[] replayData = File.ReadAllBytes(replayFile);
                    _analyzer.AnalyzeReplay(replayData);
                    processedCount++;
                    
                    Console.WriteLine($"Processed replay: {Path.GetFileName(replayFile)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing replay {replayFile}: {ex.Message}");
                }
            }

            Console.WriteLine($"\nTraining complete! Processed {processedCount} replays.");
        }

        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: DeckTrainer.exe <replay_folder_path>");
                return;
            }

            string replayFolder = args[0];
            if (!Directory.Exists(replayFolder))
            {
                Console.WriteLine($"Error: Replay folder '{replayFolder}' does not exist.");
                return;
            }

            var trainer = new DeckTrainer(replayFolder);
            trainer.TrainFromReplays();
        }
    }
}
