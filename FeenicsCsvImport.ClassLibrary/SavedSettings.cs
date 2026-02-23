using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Persistable settings that are saved to a JSON config file.
    /// Does NOT include the password for security.
    /// </summary>
    public class SavedSettings
    {
        public string ApiUrl { get; set; } = "https://api.us.acresecurity.cloud";
        public string Instance { get; set; }
        public string Username { get; set; }
        public DuplicateHandling DuplicateHandling { get; set; } = DuplicateHandling.Skip;
        public List<AccessLevelRule> AccessLevelRules { get; set; } = new List<AccessLevelRule>();

        private static readonly string DefaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FeenicsCsvImport",
            "settings.json");

        /// <summary>
        /// Saves settings to the default config file.
        /// </summary>
        public void Save(string path = null)
        {
            path = path ?? DefaultPath;
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Loads settings from the default config file. Returns default settings if the file doesn't exist.
        /// </summary>
        public static SavedSettings Load(string path = null)
        {
            path = path ?? DefaultPath;
            if (!File.Exists(path))
                return CreateDefault();

            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<SavedSettings>(json) ?? CreateDefault();
            }
            catch
            {
                return CreateDefault();
            }
        }

        /// <summary>
        /// Creates default settings with the standard access level rules.
        /// </summary>
        public static SavedSettings CreateDefault()
        {
            return new SavedSettings
            {
                ApiUrl = "https://api.us.acresecurity.cloud",
                AccessLevelRules = new List<AccessLevelRule>
                {
                    new AccessLevelRule { Name = "PoolOnlyAccess-Age12", StartAge = 12, EndAge = 14, CreateIfMissing = false },
                    new AccessLevelRule { Name = "PoolAndGymAccess-Age14", StartAge = 14, EndAge = 18, CreateIfMissing = false },
                    new AccessLevelRule { Name = "PoolAndGymAfterHoursAccess-Age18", StartAge = 18, EndAge = null, CreateIfMissing = false }
                }
            };
        }
    }
}
