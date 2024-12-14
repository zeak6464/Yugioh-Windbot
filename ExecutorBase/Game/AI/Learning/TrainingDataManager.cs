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

        public static void SaveStateActionValues(Dictionary<string, float> stateActionValues)
        {
            EnsureDirectoryExists();
            string filePath = Path.Combine(DATA_DIRECTORY, STATE_VALUES_FILE);
            string json = JsonConvert.SerializeObject(stateActionValues, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static Dictionary<string, float> LoadStateActionValues()
        {
            string filePath = Path.Combine(DATA_DIRECTORY, STATE_VALUES_FILE);
            if (!File.Exists(filePath))
                return new Dictionary<string, float>();

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, float>>(json) 
                   ?? new Dictionary<string, float>();
        }

        public static void SaveExperience(ExperienceReplay experience)
        {
            EnsureDirectoryExists();
            string filePath = Path.Combine(DATA_DIRECTORY, EXPERIENCE_FILE);
            
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
                experiences.RemoveRange(0, experiences.Count - 10000);

            string updatedJson = JsonConvert.SerializeObject(experiences, Formatting.Indented);
            File.WriteAllText(filePath, updatedJson);
        }

        public static List<ExperienceReplay> LoadExperiences()
        {
            string filePath = Path.Combine(DATA_DIRECTORY, EXPERIENCE_FILE);
            if (!File.Exists(filePath))
                return new List<ExperienceReplay>();

            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<List<ExperienceReplay>>(json) 
                   ?? new List<ExperienceReplay>();
        }

        private static void EnsureDirectoryExists()
        {
            if (!Directory.Exists(DATA_DIRECTORY))
                Directory.CreateDirectory(DATA_DIRECTORY);
        }

        public static void BackupTrainingData()
        {
            EnsureDirectoryExists();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = Path.Combine(DATA_DIRECTORY, $"backup_{timestamp}");
            
            Directory.CreateDirectory(backupDir);
            
            if (File.Exists(Path.Combine(DATA_DIRECTORY, STATE_VALUES_FILE)))
                File.Copy(
                    Path.Combine(DATA_DIRECTORY, STATE_VALUES_FILE),
                    Path.Combine(backupDir, STATE_VALUES_FILE)
                );
                
            if (File.Exists(Path.Combine(DATA_DIRECTORY, EXPERIENCE_FILE)))
                File.Copy(
                    Path.Combine(DATA_DIRECTORY, EXPERIENCE_FILE),
                    Path.Combine(backupDir, EXPERIENCE_FILE)
                );
        }
    }
}
