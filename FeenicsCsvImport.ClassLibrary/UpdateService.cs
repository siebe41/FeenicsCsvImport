using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FeenicsCsvImport.ClassLibrary
{
    public class UpdateInfo
    {
        public Version LatestVersion { get; set; }
        public string ReleaseUrl { get; set; }
        public string ReleaseNotes { get; set; }
        public bool IsUpdateAvailable { get; set; }
    }

    public class UpdateService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _repoOwner;
        private readonly string _repoName;
        private readonly Version _currentVersion;
        private Timer _timer;

        // Apps will subscribe to this event to be notified in the background
        public event EventHandler<UpdateInfo> UpdateAvailable;

        /// <summary>
        /// Initializes the update checker. Pass in the executing assembly's version so it knows what to compare against.
        /// </summary>
        public UpdateService(string repoOwner, string repoName, Version currentAppVersion)
        {
            _repoOwner = repoOwner;
            _repoName = repoName;
            _currentVersion = currentAppVersion;

            _httpClient = new HttpClient();
            // GitHub API strictly requires a User-Agent header
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FeenicsTools", _currentVersion.ToString()));
        }

        /// <summary>
        /// Checks GitHub immediately and returns the result (Great for manual button clicks).
        /// </summary>
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                string url = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        string latestTag = doc.RootElement.GetProperty("tag_name").GetString();
                        string releaseUrl = doc.RootElement.GetProperty("html_url").GetString();

                        // Clean the "v" off the tag (e.g., "v1.2.3" -> "1.2.3")
                        if (!string.IsNullOrEmpty(latestTag))
                        {
                            string cleanTag = latestTag.TrimStart('v', 'V');

                            if (Version.TryParse(cleanTag, out Version latestVersion))
                            {
                                var info = new UpdateInfo
                                {
                                    LatestVersion = latestVersion,
                                    ReleaseUrl = releaseUrl,
                                    // If latest version is strictly greater than current, an update is available
                                    IsUpdateAvailable = latestVersion > _currentVersion
                                };

                                // Fire the background event if there's an update
                                if (info.IsUpdateAvailable)
                                {
                                    UpdateAvailable?.Invoke(this, info);
                                }

                                return info;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fail silently. We don't want to crash the app if the user loses internet connection.
            }

            return new UpdateInfo { IsUpdateAvailable = false };
        }

        /// <summary>
        /// Starts a background timer that periodically checks for updates.
        /// </summary>
        public void StartPeriodicCheck(TimeSpan interval)
        {
            // Clean up existing timer if one is running
            _timer?.Dispose();

            // Start the timer. It will wait the 'interval' before firing the first time.
            _timer = new Timer(async _ => await CheckForUpdatesAsync(), null, interval, interval);
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}