using CsvHelper;
using Feenics.Keep.WebApi.Model;
using Feenics.Keep.WebApi.Wrapper;
using MongoDB.Bson;
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
        public int PeopleUpdated { get; set; }
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
        /// Connects to the Feenics API and retrieves the names of all access levels defined in the instance.
        /// </summary>
        public static async Task<List<string>> FetchAccessLevelNamesAsync(string apiUrl, string instance, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
                throw new ArgumentException("API URL is required.", nameof(apiUrl));
            if (string.IsNullOrWhiteSpace(instance))
                throw new ArgumentException("Instance is required.", nameof(instance));
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.", nameof(username));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password is required.", nameof(password));

            var client = new Client(apiUrl);
            var (success, error, msg) = await client.LoginAsync(instance, username, password);
            if (!success)
                throw new Exception($"Login failed: {msg}");

            var inst = await client.GetCurrentInstanceAsync();
            var accessLevels = await client.GetAccessLevelsAsync(inst);

            var names = new List<string>();
            if (accessLevels != null)
            {
                foreach (var al in accessLevels)
                {
                    names.Add(al.CommonName);
                }
            }
            return names;
        }

        /// <summary>
        /// Writes a CSV template file with headers and sample rows so users know the expected format.
        /// </summary>
        public static void WriteTemplateCsv(string filePath)
        {
            var sampleRecords = new List<UserCsvModel>
            {
                new UserCsvModel { Name = "John Smith", Address = "123 Main St, Springfield, IL 62701", Phone = "555-123-4567", Email = "john.smith@example.com", Birthday = new DateTime(2010, 3, 15) },
                new UserCsvModel { Name = "Jane Doe", Address = "456 Oak Ave, Columbus, OH 43215", Phone = "555-987-6543", Email = "jane.doe@example.com", Birthday = new DateTime(2008, 7, 22) }
            };

            using (var writer = new StreamWriter(filePath))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(sampleRecords);
            }
        }

        /// <summary>
        /// Reads and parses a CSV file, returning preview models
        /// </summary>
        public List<ImportPreviewModel> LoadCsvForPreview(string csvFilePath)
        {
            var records = ReadCsvFile(csvFilePath);
            return records.Select(r => ImportPreviewModel.FromCsvRecord(r, _config.AccessLevelRules)).ToList();
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
        /// Extracts detailed diagnostic information from an exception.
        /// Provides rich output for FailedOutcomeException from the Feenics API wrapper.
        /// </summary>
        public static string FormatExceptionDetails(Exception ex)
        {
            var details = new List<string>();
            details.Add($"Exception Type: {ex.GetType().FullName}");
            details.Add($"Message: {ex.Message}");

            if (ex is FailedOutcomeException foe)
            {
                if (!string.IsNullOrEmpty(foe.InFunction))
                    details.Add($"API Function: {foe.InFunction}");
                if (!string.IsNullOrEmpty(foe.Url))
                    details.Add($"URL: {foe.Url}");
                if (foe.HttpStatus.HasValue)
                    details.Add($"HTTP Status: {foe.HttpStatus.Value}");
                if (!string.IsNullOrEmpty(foe.ResponseString))
                    details.Add($"Response: {foe.ResponseString}");
            }

            if (ex.InnerException != null)
            {
                details.Add($"Inner Exception: {FormatExceptionDetails(ex.InnerException)}");
            }

            details.Add($"Stack Trace: {ex.StackTrace}");

            return string.Join(Environment.NewLine + "  ", details);
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

            // Validate configuration before attempting any API calls
            if (string.IsNullOrWhiteSpace(_config.ApiUrl))
            {
                result.Errors.Add("API URL is required.");
                return result;
            }
            if (string.IsNullOrWhiteSpace(_config.Instance))
            {
                result.Errors.Add("Instance is required.");
                return result;
            }
            if (string.IsNullOrWhiteSpace(_config.Username))
            {
                result.Errors.Add("Username is required.");
                return result;
            }
            if (string.IsNullOrWhiteSpace(_config.Password))
            {
                result.Errors.Add("Password is required.");
                return result;
            }
            if (string.IsNullOrWhiteSpace(csvFilePath))
            {
                result.Errors.Add("CSV file path is required.");
                return result;
            }
            if (_config.AccessLevelRules == null || _config.AccessLevelRules.Count == 0)
            {
                result.Errors.Add("At least one access level rule is required.");
                return result;
            }

            try
            {
                // 1. Authenticate
                ReportProgress(progress, "Connecting to API...", 0);
                Log("Connecting to API...");

                var client = new Client(_config.ApiUrl);
                Log($"DEBUG: API URL = {_config.ApiUrl}");
                Log($"DEBUG: Instance = {_config.Instance}");

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
                Log($"DEBUG: Instance Key = {instance.Key}");
                Log($"DEBUG: Instance InFolderHref = {instance.InFolderHref}");
                ReportProgress(progress, $"Connected to: {instance.CommonName}", 5);

                //var folders = await client.GetChildFoldersAsync(instance);

                var existingAccessLevels = await client.GetAccessLevelsAsync(instance);
                Log($"DEBUG: Found {(existingAccessLevels != null ? existingAccessLevels.Count() : 0)} existing access levels");
                if (existingAccessLevels != null)
                {
                    foreach (var al in existingAccessLevels)
                    {
                        Log($"DEBUG:   Access Level: '{al.CommonName}' (Key={al.Key})");
                    }
                }

                // Resolve each rule to an AccessLevelInfo (create if needed)
                var resolvedAccessLevels = new Dictionary<AccessLevelRule, AccessLevelInfo>();
                FolderInfo rootFolder = null;

                foreach (var rule in _config.AccessLevelRules)
                {
                    var matched = existingAccessLevels.FirstOrDefault(al => al.CommonName.ToString() == rule.Name);

                    if (matched != null)
                    {
                        Log($"DEBUG: Rule '{rule.Name}' (Age {rule.AgeRangeDisplay}) matched Key={matched.Key}");
                        resolvedAccessLevels[rule] = matched;
                    }
                    else if (rule.CreateIfMissing)
                    {
                        // Only fetch the root folder once, and only when we actually need to create an access level
                        if (rootFolder == null)
                        {
                            Log("Fetching root folder for access level creation...");
                            rootFolder = await client.GetFolderAsync(instance.InFolderHref);
                        }

                        Log($"Creating access level '{rule.Name}' (marked as create-if-missing)...");
                        var newAl = new AccessLevelInfo { CommonName = rule.Name };
                        var created = await client.AddAccessLevelAsync(rootFolder, newAl);
                        Log($"DEBUG: Created access level '{rule.Name}' Key={created.Key}");
                        resolvedAccessLevels[rule] = created;
                    }
                    else
                    {
                        result.Success = false;
                        result.Errors.Add($"Access level not found: '{rule.Name}' (Age {rule.AgeRangeDisplay}). Enable 'Create' to auto-create it.");
                        Log($"ERROR: Access level not found: '{rule.Name}'");
                    }
                }

                if (result.Errors.Count > 0)
                {
                    return result;
                }

                // 2. Read CSV
                ReportProgress(progress, "Reading CSV file...", 10);
                Log("Reading CSV file...");
                var records = ReadCsvFile(csvFilePath);
                Log($"DEBUG: Read {records.Count} records from CSV");

                cancellationToken.ThrowIfCancellationRequested();

                // 3. Query existing people to check for duplicates
                ReportProgress(progress, "Checking for existing people...", 12);
                var existingByName = new Dictionary<string, PersonInfo>();
                int page = 0;
                const int pageSize = 1000;
                while (true)
                {
                    var peoplePage = await client.GetPeopleAsync(instance, page, pageSize);
                    if (peoplePage == null || !peoplePage.Any())
                        break;

                    foreach (var ep in peoplePage)
                    {
                        if (!existingByName.ContainsKey(ep.CommonName))
                            existingByName[ep.CommonName] = ep;
                    }

                    if (peoplePage.Count() < pageSize)
                        break;

                    page++;
                    await Task.Delay(_config.ApiCallDelayMs, cancellationToken);
                }
                Log($"DEBUG: Found {existingByName.Count} existing people in instance");

                // 4. Create or update people
                ReportProgress(progress, "Preparing person records...", 15);
                var peopleToCreate = new List<PersonInfo>();
                var peopleToUpdate = new List<(PersonInfo Existing, UserCsvModel Record)>();
                var skippedDuplicates = new List<string>();
                var skippedNames = new HashSet<string>();

                foreach (var record in records)
                {
                    var nameParts = record.Name.Split(' ');
                    if (existingByName.TryGetValue(record.Name, out var existingPerson))
                    {
                        switch (_config.DuplicateHandling)
                        {
                            case DuplicateHandling.Update:
                                peopleToUpdate.Add((existingPerson, record));
                                break;

                            case DuplicateHandling.CreateNew:
                                Log($"Creating new entry for '{record.Name}' even though one already exists (Key={existingPerson.Key}).");
                                var duplicatePerson = new PersonInfo
                                {
                                    GivenName = nameParts[0],
                                    Surname = nameParts.Length > 1 ? nameParts[1] : "",
                                    CommonName = record.Name,
                                    Addresses = new AddressInfo[]
                                    {
                                        new PhoneInfo { Number = record.Phone, Type = "Mobile" },
                                        new EmailAddressInfo { MailTo = record.Email, Type = "Home" },
                                        ParseSingleStringAddress(record.Address)
                                    },
                                };
                                peopleToCreate.Add(duplicatePerson);
                                result.Warnings.Add($"Duplicate created: '{record.Name}' (existing Key={existingPerson.Key})");
                                break;

                            case DuplicateHandling.Skip:
                            default:
                                skippedNames.Add(record.Name);
                                skippedDuplicates.Add(record.Name);
                                Log($"Skipping '{record.Name}' - already exists (Key={existingPerson.Key}).");
                                break;
                        }
                    }
                    else
                    {
                        var newPerson = new PersonInfo
                        {
                            GivenName = nameParts[0],
                            Surname = nameParts.Length > 1 ? nameParts[1] : "",
                            CommonName = record.Name,
                            Addresses = new AddressInfo[]
                            {
                                new PhoneInfo { Number = record.Phone, Type = "Mobile" },
                                new EmailAddressInfo { MailTo = record.Email, Type = "Home" },
                                ParseSingleStringAddress(record.Address)
                            },
                        };
                        peopleToCreate.Add(newPerson);
                    }
                }

                foreach (var name in skippedDuplicates)
                {
                    result.Warnings.Add($"Skipped duplicate: {name}");
                }

                // Create new people
                if (peopleToCreate.Count > 0)
                {
                    Log($"Adding {peopleToCreate.Count} new people in batch...");
                    ReportProgress(progress, $"Creating {peopleToCreate.Count} people...", 18);
                    int createdCount = await client.AddPeopleBatchAsync(instance, peopleToCreate.ToArray());
                    result.PeopleCreated = createdCount;
                    Log($"Successfully created {createdCount} people.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Update existing people
                if (peopleToUpdate.Count > 0)
                {
                    Log($"Updating {peopleToUpdate.Count} existing people...");
                    ReportProgress(progress, $"Updating {peopleToUpdate.Count} people...", 22);
                    foreach (var (existing, record) in peopleToUpdate)
                    {
                        var nameParts = record.Name.Split(' ');
                        existing.GivenName = nameParts[0];
                        existing.Surname = nameParts.Length > 1 ? nameParts[1] : "";
                        existing.Addresses = new AddressInfo[]
                        {
                            new PhoneInfo { Number = record.Phone, Type = "Mobile" },
                            new EmailAddressInfo { MailTo = record.Email, Type = "Home" },
                            ParseSingleStringAddress(record.Address)
                        };

                        try
                        {
                            await client.UpdatePersonAsync(existing);
                            result.PeopleUpdated++;
                            Log($"Updated '{record.Name}' (Key={existing.Key})");
                        }
                        catch (Exception ex)
                        {
                            var warning = $"Failed to update '{record.Name}': {ex.Message}";
                            result.Warnings.Add(warning);
                            Log($"Warning: {warning}");
                        }

                        await Task.Delay(_config.ApiCallDelayMs, cancellationToken);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 5. Query for all people to match records
                ReportProgress(progress, "Retrieving people...", 30);
                var matchedPeopleByName = new Dictionary<string, PersonInfo>();
                var recordNames = new HashSet<string>(records.Select(r => r.Name));
                page = 0;
                while (true)
                {
                    var peoplePage = await client.GetPeopleAsync(instance, page, pageSize);
                    if (peoplePage == null || !peoplePage.Any())
                        break;

                    foreach (var p in peoplePage)
                    {
                        if (recordNames.Contains(p.CommonName) && !matchedPeopleByName.ContainsKey(p.CommonName))
                            matchedPeopleByName[p.CommonName] = p;
                    }

                    if (peoplePage.Count() < pageSize)
                        break;

                    page++;
                    await Task.Delay(_config.ApiCallDelayMs, cancellationToken);
                }
                Log($"DEBUG: Retrieved and matched {matchedPeopleByName.Count} people by name");

                // 6. Build access assignment tasks from rules
                var accessAssignmentTasks = new List<(Func<Task> Action, string Description)>();
                DateTime now = DateTime.UtcNow;
                Log($"DEBUG: Current UTC time = {now:O}");

                foreach (var record in records)
                {
                    // Skip access level assignments for users that were skipped during import
                    if (skippedNames.Contains(record.Name))
                    {
                        Log($"Skipping access level assignments for '{record.Name}' (user was skipped).");
                        continue;
                    }

                    if (!matchedPeopleByName.TryGetValue(record.Name, out var person))
                    {
                        var warning = $"Could not find created person: {record.Name}";
                        result.Warnings.Add(warning);
                        Log($"Warning: {warning}");
                        continue;
                    }

                    Log($"Queuing access assignments for {record.Name} (Key={person.Key})...");
                    DateTime dob = record.Birthday;

                    foreach (var rule in _config.AccessLevelRules)
                    {
                        var accessLevel = resolvedAccessLevels[rule];
                        DateTime activeOn = rule.GetActiveOn(dob);
                        DateTime? expiresOn = rule.GetExpiresOn(dob);

                        Log($"DEBUG:   Rule '{rule.Name}' (Age {rule.AgeRangeDisplay}): ActiveOn={activeOn:yyyy-MM-dd}, ExpiresOn={expiresOn?.ToString("yyyy-MM-dd") ?? "none"}");

                        // Skip rules where the end date has already passed
                        if (expiresOn.HasValue && expiresOn.Value <= now)
                        {
                            Log($"DEBUG:   Skipping '{rule.Name}' (expired: {expiresOn.Value:yyyy-MM-dd} <= now)");
                            continue;
                        }

                        var p = person;
                        var ao = activeOn;
                        var eo = expiresOn;
                        var al = accessLevel;
                        var desc = expiresOn.HasValue
                            ? $"{record.Name} - {rule.Name} (Start={activeOn:yyyy-MM-dd}, End={expiresOn.Value:yyyy-MM-dd})"
                            : $"{record.Name} - {rule.Name} (Start={activeOn:yyyy-MM-dd})";

                        accessAssignmentTasks.Add((
                            () => AssignScheduledAccessAsync(client, p, al, ao, eo),
                            desc
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
                        Log($"   -> SUCCESS: {description}");
                    }
                    catch (Exception ex)
                    {
                        var details = FormatExceptionDetails(ex);
                        var warning = $"Failed to assign {description}";
                        result.Warnings.Add($"{warning}: {ex.Message}");
                        Log($"   -> FAILED: {warning}");
                        Log($"   -> Details: {details}");
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
            catch (FailedOutcomeException ex)
            {
                result.Success = false;
                var details = FormatExceptionDetails(ex);
                result.Errors.Add($"API error: {ex.Message}");
                Log($"Import failed with API error:");
                Log($"  {details}");
            }
            catch (Exception ex)
            {
                result.Success = false;
                var details = FormatExceptionDetails(ex);
                result.Errors.Add($"Import failed: {ex.Message}");
                Log($"Import failed:");
                Log($"  {details}");
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
            // For the scheduled access level API, a valid date range is always required.
            // If no expiry is provided, use a date 50 years from now to represent permanent access.
            var effectiveExpiresOn = expiresOn ?? DateTime.UtcNow.AddYears(50);

            Log($"DEBUG: AddScheduledAccessLevelBatchForPersonAsync call:");
            Log($"DEBUG:   Person: '{person.CommonName}' (Key={person.Key})");
            Log($"DEBUG:   AccessLevel: '{accessLevel.CommonName}' (Key={accessLevel.Key})");
            Log($"DEBUG:   ActiveOn: {activeOn:O}");
            Log($"DEBUG:   ExpiresOn: {effectiveExpiresOn:O}{(expiresOn.HasValue ? "" : " (no expiry provided, using 50 years from now)") }");

            var metaDoc = new BsonDocument
            {
                { "ActiveOn", new BsonDateTime(activeOn) },
                { "ExpiresOn", new BsonDateTime(effectiveExpiresOn) }
            };

            var linkItem = new ObjectLinkItem
            {
                LinkedObjectKey = accessLevel.Key,
                Relation = "ScheduledAccessLevel",
                MetaDataBson = metaDoc.ToBson()
            };

            Log($"DEBUG:   ObjectLinkItem: LinkedObjectKey={linkItem.LinkedObjectKey}, Relation={linkItem.Relation}, MetaDataBson.Length={linkItem.MetaDataBson?.Length}");

            try
            {
                await client.AddScheduledAccessLevelBatchForPersonAsync(person, new[] { linkItem });
                Log($"   -> Scheduled {accessLevel.CommonName}: {activeOn.ToShortDateString()} to {effectiveExpiresOn.ToShortDateString()}");
            }
            catch (FailedOutcomeException ex)
            {
                Log($"   -> API ERROR in AddScheduledAccessLevelBatchForPersonAsync:");
                Log($"      {FormatExceptionDetails(ex)}");
                throw;
            }
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
                catch (FailedOutcomeException ex) when (ex.HttpStatus.HasValue && (int)ex.HttpStatus.Value == 429)
                {
                    retryCount++;
                    if (retryCount > _config.MaxRetries)
                    {
                        Log($"   -> FAILED after {_config.MaxRetries} retries: {operationName}");
                        throw;
                    }

                    int jitter = _random.Next(0, currentDelayMs / 4);
                    int delayWithJitter = currentDelayMs + jitter;

                    Log($"   -> Rate limited (429). Retry {retryCount}/{_config.MaxRetries} after {delayWithJitter}ms: {operationName}");
                    await Task.Delay(delayWithJitter);
                    currentDelayMs = Math.Min(currentDelayMs * 2, _config.MaxRetryDelayMs);
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
            var zipRegex = new Regex(@"(?:\d{5}(?:-\d{4})?)|(?:[A-Za-z]\d[A-Za-z][ -]?\d[A-ZaZ]\d)$");
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
