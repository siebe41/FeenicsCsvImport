using CsvHelper;
using Feenics.Keep.WebApi.Model;
using Feenics.Keep.WebApi.Wrapper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Progress information for import operations
    /// </summary>
    public class ImportProgress
    {
        public int CurrentStep { get; set; }
        public int TotalSteps { get; set; }
        public string Message { get; set; }
        public bool IsError { get; set; }
    }

    /// <summary>
    /// Result of an import operation
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }
        public int PeopleCreated { get; set; }
        public int AccessLevelsAssigned { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Service for importing users from CSV and assigning access levels
    /// </summary>
    public class ImportService
    {
        private readonly ImportConfiguration _config;
        private readonly Random _random = new Random();
        private readonly Action<string> _logger;

        public ImportService(ImportConfiguration config, Action<string> logger = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? (msg => { }); // Default to no-op if no logger provided
        }

        private void Log(string message)
        {
            _logger(message);
        }

        /// <summary>
        /// Reads and parses a CSV file, returning preview models
        /// </summary>
        public List<ImportPreviewModel> LoadCsvForPreview(string csvFilePath)
        {
            var records = ReadCsvFile(csvFilePath);
            return records.Select(ImportPreviewModel.FromCsvRecord).ToList();
        }

        /// <summary>
        /// Reads CSV records from a file
        /// </summary>
        public List<UserCsvModel> ReadCsvFile(string csvFilePath)
        {
            using (var reader = new StreamReader(csvFilePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                return csv.GetRecords<UserCsvModel>().ToList();
            }
        }

        /// <summary>
        /// Executes the full import process
        /// </summary>
        public async Task<ImportResult> ExecuteImportAsync(
            string csvFilePath,
            IProgress<ImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new ImportResult();

            try
            {
                // 1. Authenticate
                ReportProgress(progress, "Connecting to API...", 0);
                Log("Connecting to API...");

                var client = new Client(_config.ApiUrl);
                var (success, error, msg) = await client.LoginAsync(_config.Instance, _config.Username, _config.Password);

                if (!success)
                {
                    result.Success = false;
                    result.Errors.Add($"Login failed: {msg}");
                    Log($"Login Failed: {msg}");
                    return result;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var instance = await client.GetCurrentInstanceAsync();
                Log($"Connected to: {instance.CommonName}");
                ReportProgress(progress, $"Connected to: {instance.CommonName}", 5);

                var rootFolder = await client.GetFolderAsync(instance.InFolderHref);
                var accessLevelsFolder = await client.GetFolderAsync($"{instance.InFolderHref}/ACCESS_LEVELS");
                var accessLevels = await client.GetAccessLevelsAsync(accessLevelsFolder);

                var poolAccess = accessLevels.FirstOrDefault(al => al.CommonName.ToString() == _config.PoolAccessLevelName);
                var poolGymAccess = accessLevels.FirstOrDefault(al => al.CommonName.ToString() == _config.PoolGymAccessLevelName);
                var allAccess = accessLevels.FirstOrDefault(al => al.CommonName.ToString() == _config.AllAccessLevelName);

                if (poolAccess == null || poolGymAccess == null || allAccess == null)
                {
                    result.Success = false;
                    if (poolAccess == null) result.Errors.Add($"Access level not found: {_config.PoolAccessLevelName}");
                    if (poolGymAccess == null) result.Errors.Add($"Access level not found: {_config.PoolGymAccessLevelName}");
                    if (allAccess == null) result.Errors.Add($"Access level not found: {_config.AllAccessLevelName}");
                    return result;
                }

                // 2. Read CSV
                ReportProgress(progress, "Reading CSV file...", 10);
                Log("Reading CSV file...");
                var records = ReadCsvFile(csvFilePath);

                cancellationToken.ThrowIfCancellationRequested();

                // 3. Build PersonInfo objects
                ReportProgress(progress, "Preparing person records...", 15);
                var personInfoList = new List<PersonInfo>();
                foreach (var record in records)
                {
                    var newPerson = new PersonInfo
                    {
                        CommonName = record.Name,
                        Addresses = new AddressInfo[]
                        {
                            new PhoneInfo { Number = record.Phone, Type = "Mobile" },
                            new EmailAddressInfo { MailTo = record.Email, Type = "Home" },
                            ParseSingleStringAddress(record.Address)
                        },
                    };
                    personInfoList.Add(newPerson);
                }

                // 4. Batch add all people
                Log($"Adding {personInfoList.Count} people in batch...");
                ReportProgress(progress, $"Creating {personInfoList.Count} people...", 20);
                int createdCount = await client.AddPeopleBatchAsync(rootFolder, personInfoList.ToArray());
                result.PeopleCreated = createdCount;
                Log($"Successfully created {createdCount} people.");

                cancellationToken.ThrowIfCancellationRequested();

                // 5. Query for created people
                ReportProgress(progress, "Retrieving created people...", 30);
                var allPeople = await client.GetPeopleAsync(rootFolder, records.Count);
                var createdPeopleByName = allPeople
                    .Where(p => records.Any(r => r.Name == p.CommonName))
                    .ToDictionary(p => p.CommonName, p => p);

                // 6. Build access assignment tasks
                var accessAssignmentTasks = new List<(Func<Task> Action, string Description)>();
                DateTime now = DateTime.UtcNow;

                foreach (var record in records)
                {
                    if (!createdPeopleByName.TryGetValue(record.Name, out var person))
                    {
                        var warning = $"Could not find created person: {record.Name}";
                        result.Warnings.Add(warning);
                        Log($"Warning: {warning}");
                        continue;
                    }

                    Log($"Queuing access assignments for {record.Name}...");

                    DateTime dob = record.Birthday;
                    DateTime age12 = dob.AddYears(12);
                    DateTime age14 = dob.AddYears(14);
                    DateTime age18 = dob.AddYears(18);

                    // Pool Access (12-14)
                    if (age14 > now)
                    {
                        var p = person;
                        var activeOn = age12;
                        var expiresOn = age14;
                        accessAssignmentTasks.Add((
                            () => AssignScheduledAccessAsync(client, p, poolAccess, activeOn, expiresOn),
                            $"{record.Name} - Pool Access"
                        ));
                    }

                    // Pool + Gym Access (14-18)
                    if (age18 > now)
                    {
                        var p = person;
                        var activeOn = age14;
                        var expiresOn = age18;
                        accessAssignmentTasks.Add((
                            () => AssignScheduledAccessAsync(client, p, poolGymAccess, activeOn, expiresOn),
                            $"{record.Name} - Pool+Gym Access"
                        ));
                    }

                    // All Access (18+)
                    {
                        var p = person;
                        var activeOn = age18;
                        accessAssignmentTasks.Add((
                            () => AssignScheduledAccessAsync(client, p, allAccess, activeOn, null),
                            $"{record.Name} - All Access"
                        ));
                    }
                }

                // 7. Execute access assignments with progress
                int totalTasks = accessAssignmentTasks.Count;
                Log($"Assigning {totalTasks} scheduled access levels...");

                for (int i = 0; i < accessAssignmentTasks.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var (action, description) = accessAssignmentTasks[i];
                    int progressPercent = 30 + (int)((i / (double)totalTasks) * 70);

                    ReportProgress(progress, $"Assigning: {description}", progressPercent);

                    try
                    {
                        await ExecuteWithRetryAsync(action, description);
                        result.AccessLevelsAssigned++;
                    }
                    catch (Exception ex)
                    {
                        var warning = $"Failed to assign {description}: {ex.Message}";
                        result.Warnings.Add(warning);
                        Log($"   -> FAILED: {warning}");
                    }

                    if (i < accessAssignmentTasks.Count - 1)
                    {
                        await Task.Delay(_config.ApiCallDelayMs, cancellationToken);
                    }
                }

                ReportProgress(progress, "Import complete!", 100);
                Log("Import complete.");
                result.Success = true;
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.Errors.Add("Import was cancelled.");
                Log("Import was cancelled.");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Import failed: {ex.Message}");
                Log($"Import failed: {ex.Message}");
            }

            return result;
        }

        private void ReportProgress(IProgress<ImportProgress> progress, string message, int step)
        {
            progress?.Report(new ImportProgress
            {
                Message = message,
                CurrentStep = step,
                TotalSteps = 100
            });
        }

        private async Task AssignScheduledAccessAsync(Client client, PersonInfo person, AccessLevelInfo accessLevel, DateTime activeOn, DateTime? expiresOn)
        {
            var metadata = new
            {
                ActiveOn = activeOn,
                ExpiresOn = expiresOn
            };

            await client.AssignConnectedObjectAsync(person, accessLevel, "ScheduledAccessLevel", false, metadata);
            Log($"   -> Scheduled {accessLevel.CommonName}: Start {activeOn.ToShortDateString()}");
        }

        private async Task ExecuteWithRetryAsync(Func<Task> action, string operationName)
        {
            int retryCount = 0;
            int currentDelayMs = _config.InitialRetryDelayMs;

            while (true)
            {
                try
                {
                    await action();
                    return;
                }
                catch (Exception ex) when (Is429Exception(ex))
                {
                    retryCount++;
                    if (retryCount > _config.MaxRetries)
                    {
                        Log($"   -> FAILED after {_config.MaxRetries} retries: {operationName}");
                        throw new Exception($"Failed after {_config.MaxRetries} retries: {operationName}", ex);
                    }

                    int jitter = _random.Next(0, currentDelayMs / 4);
                    int delayWithJitter = currentDelayMs + jitter;

                    Log($"   -> Rate limited (429). Retry {retryCount}/{_config.MaxRetries} after {delayWithJitter}ms: {operationName}");
                    await Task.Delay(delayWithJitter);
                    currentDelayMs = Math.Min(currentDelayMs * 2, _config.MaxRetryDelayMs);
                }
            }
        }

        /// <summary>
        /// Checks if an exception represents a 429 Too Many Requests response.
        /// </summary>
        public static bool Is429Exception(Exception ex)
        {
            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.Message.Contains("429") || httpEx.Message.Contains("Too Many Requests"))
                {
                    return true;
                }
            }

            if (ex is WebException webEx && webEx.Response is HttpWebResponse response)
            {
                if ((int)response.StatusCode == 429)
                {
                    return true;
                }
            }

            if (ex.InnerException != null)
            {
                return Is429Exception(ex.InnerException);
            }

            if (ex.Message.Contains("429") || ex.Message.Contains("TooManyRequests") || ex.Message.Contains("Too Many Requests"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses a single-line address string into structured address components
        /// </summary>
        public static MailingAddressInfo ParseSingleStringAddress(string rawAddress)
        {
            var addressInfo = new MailingAddressInfo { Type = "Home", Country = "US" };

            if (string.IsNullOrWhiteSpace(rawAddress)) return addressInfo;

            rawAddress = rawAddress.Trim();

            // Postal Code
            var zipRegex = new Regex(@"(?:\d{5}(?:-\d{4})?)|(?:[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d)$");
            var zipMatch = zipRegex.Match(rawAddress);

            if (zipMatch.Success)
            {
                addressInfo.PostalCode = zipMatch.Value;
                rawAddress = rawAddress.Substring(0, zipMatch.Index).Trim(',', ' ');
            }

            // State/Province
            var stateRegex = new Regex(@"\b([A-Za-z]{2})\b$");
            var stateMatch = stateRegex.Match(rawAddress);

            if (stateMatch.Success)
            {
                addressInfo.Province = stateMatch.Value.ToUpper();
                rawAddress = rawAddress.Substring(0, stateMatch.Index).Trim(',', ' ');
            }

            // City and Street
            int lastCommaIndex = rawAddress.LastIndexOf(',');
            if (lastCommaIndex > 0)
            {
                addressInfo.City = rawAddress.Substring(lastCommaIndex + 1).Trim();
                addressInfo.Street = rawAddress.Substring(0, lastCommaIndex).Trim();
            }
            else
            {
                addressInfo.Street = rawAddress;
            }

            return addressInfo;
        }
    }
}
