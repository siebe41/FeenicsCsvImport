using System;
using System.Collections.Generic;

namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Configuration settings for the import service
    /// </summary>
    public class ImportConfiguration
    {
        /// <summary>
        /// API endpoint URL
        /// </summary>
        public string ApiUrl { get; set; } = "https://api.us.acresecurity.cloud";

        /// <summary>
        /// Instance name for login
        /// </summary>
        public string Instance { get; set; }

        /// <summary>
        /// Username for login
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Password for login
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Access level rules defining which access levels to assign based on age.
        /// Each rule maps an age range to a named Feenics access level.
        /// </summary>
        public List<AccessLevelRule> AccessLevelRules { get; set; } = new List<AccessLevelRule>();

        /// <summary>
        /// Controls how existing people (matched by name) are handled during import.
        /// </summary>
        public DuplicateHandling DuplicateHandling { get; set; } = DuplicateHandling.Skip;

        /// <summary>
        /// Delay between API calls in milliseconds
        /// </summary>
        public int ApiCallDelayMs { get; set; } = 100;

        /// <summary>
        /// Maximum retry attempts for rate-limited requests
        /// </summary>
        public int MaxRetries { get; set; } = 5;

        /// <summary>
        /// Initial retry delay in milliseconds
        /// </summary>
        public int InitialRetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum retry delay in milliseconds
        /// </summary>
        public int MaxRetryDelayMs { get; set; } = 30000;

        /// <summary>
        /// Creates a default configuration with the original hardcoded access level rules.
        /// </summary>
        public static ImportConfiguration CreateDefault()
        {
            return new ImportConfiguration
            {
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
