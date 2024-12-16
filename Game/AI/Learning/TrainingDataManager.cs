using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using WindBot.Game.AI.Enums;

namespace WindBot.Game.AI.Learning
{
    public class TrainingDataManager
    {
        private const string DATA_DIRECTORY = "TrainingData";
        private const string STATE_VALUES_FILE = "state_action_values.json";
        private const string EXPERIENCE_FILE = "experience_replay.json";

        public class ExperienceReplay
        {
            public string StateKey { get; set; }
            public string ActionKey { get; set; }
            public float Reward { get; set; }
            public string NextStateKey { get; set; }
        }

        private static string GetTrainingDataPath()
        {
            // Get the project root directory (3 levels up from the executing assembly location)
            string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            return Path.Combine(projectRoot, DATA_DIRECTORY);
        }

        private static void EnsureDirectoryExists()
        {
            string dirPath = GetTrainingDataPath();
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
        }

        public static void SaveStateActionValues(Dictionary<string, float> stateActionValues)
        {
            EnsureDirectoryExists();
            string filePath = Path.Combine(GetTrainingDataPath(), STATE_VALUES_FILE);
            string json = JsonConvert.SerializeObject(stateActionValues, Formatting.Indented);
            File.WriteAllText(filePath, json);
            Logger.WriteLine($"[TrainingData] Saved {stateActionValues.Count} state-action values to {STATE_VALUES_FILE}");
        }

        public static Dictionary<string, float> LoadStateActionValues()
        {
            string filePath = Path.Combine(GetTrainingDataPath(), STATE_VALUES_FILE);
            if (!File.Exists(filePath))
            {
                Logger.WriteLine("[TrainingData] No existing state-action values found. Starting fresh.");
                return new Dictionary<string, float>();
            }

            string json = File.ReadAllText(filePath);
            var values = JsonConvert.DeserializeObject<Dictionary<string, float>>(json) 
                   ?? new Dictionary<string, float>();
            Logger.WriteLine($"[TrainingData] Loaded {values.Count} state-action values from {STATE_VALUES_FILE}");
            return values;
        }

        public static void SaveExperience(ExperienceReplay experience)
        {
            EnsureDirectoryExists();
            string filePath = Path.Combine(GetTrainingDataPath(), EXPERIENCE_FILE);
            
            List<ExperienceReplay> experiences;
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                experiences = JsonConvert.DeserializeObject<List<ExperienceReplay>>(json) 
                             ?? new List<ExperienceReplay>();
            }
            else
            {
                experiences = new List<ExperienceReplay>();
            }

            experiences.Add(experience);

            if (experiences.Count > 10000)
            {
                Logger.WriteLine("[TrainingData] Experience buffer full. Removing oldest experiences.");
                experiences.RemoveRange(0, experiences.Count - 10000);
            }

            string updatedJson = JsonConvert.SerializeObject(experiences, Formatting.Indented);
            File.WriteAllText(filePath, updatedJson);
            Logger.WriteLine($"[TrainingData] Saved experience. Total experiences: {experiences.Count}");
        }

        public static List<ExperienceReplay> LoadExperiences()
        {
            string filePath = Path.Combine(GetTrainingDataPath(), EXPERIENCE_FILE);
            if (!File.Exists(filePath))
                return new List<ExperienceReplay>();

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<ExperienceReplay>>(json) 
                   ?? new List<ExperienceReplay>();
        }

        public static void BackupTrainingData()
        {
            EnsureDirectoryExists();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = Path.Combine(GetTrainingDataPath(), string.Format("backup_{0}", timestamp));
            
            Directory.CreateDirectory(backupDir);
            
            if (File.Exists(Path.Combine(GetTrainingDataPath(), STATE_VALUES_FILE)))
                File.Copy(
                    Path.Combine(GetTrainingDataPath(), STATE_VALUES_FILE),
                    Path.Combine(backupDir, STATE_VALUES_FILE)
                );
                
            if (File.Exists(Path.Combine(GetTrainingDataPath(), EXPERIENCE_FILE)))
                File.Copy(
                    Path.Combine(GetTrainingDataPath(), EXPERIENCE_FILE),
                    Path.Combine(backupDir, EXPERIENCE_FILE)
                );
        }
    }
}
