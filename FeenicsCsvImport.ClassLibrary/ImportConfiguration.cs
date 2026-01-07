using System;

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
        /// Access level name for Pool access (ages 12-14)
        /// </summary>
        public string PoolAccessLevelName { get; set; } = "PoolOnlyAccess-Age12";

        /// <summary>
        /// Access level name for Pool + Gym access (ages 14-18)
        /// </summary>
        public string PoolGymAccessLevelName { get; set; } = "PoolAndGymAccess-Age14";

        /// <summary>
        /// Access level name for All access (ages 18+)
        /// </summary>
        public string AllAccessLevelName { get; set; } = "PoolAndGymAfterHoursAccess-Age18";

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
    }
}
