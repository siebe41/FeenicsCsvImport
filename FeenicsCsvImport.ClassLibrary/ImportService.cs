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
        /// Reads and parses a CSV file, returning preview models.
        /// Records missing Name or Address are excluded.
        /// </summary>
        public List<ImportPreviewModel> LoadCsvForPreview(string csvFilePath)
        {
            var records = ReadCsvFile(csvFilePath);
            return records
                .Where(r => !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Address))
                .Select(r => ImportPreviewModel.FromCsvRecord(r, _config.AccessLevelRules))
                .ToList();
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
                var allRecords = ReadCsvFile(csvFilePath);
                Log($"DEBUG: Read {allRecords.Count} records from CSV");

                // Validate records - skip rows missing Name or Address
                var records = new List<UserCsvModel>();
                int skippedEmpty = 0;
                for (int i = 0; i < allRecords.Count; i++)
                {
                    var r = allRecords[i];
                    if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Address))
                    {
                        skippedEmpty++;
                        var reason = string.IsNullOrWhiteSpace(r.Name) ? "missing Name" : "missing Address";
                        Log($"Skipping CSV row {i + 1}: {reason} (Name='{r.Name ?? ""}', Address='{r.Address ?? ""}')");
                        result.Warnings.Add($"Skipped row {i + 1}: {reason}");
                        continue;
                    }
                    records.Add(r);
                }

                if (skippedEmpty > 0)
                {
                    Log($"Skipped {skippedEmpty} record(s) with missing Name or Address.");
                }

                if (records.Count == 0)
                {
                    result.Errors.Add("No valid records found in CSV. Every row must have a Name and Address.");
                    return result;
                }

                Log($"DEBUG: {records.Count} valid records to process");

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
                    Log($"Updating {peopleToUpdate.Count} existing people (concurrency={_config.MaxConcurrency})...");
                    ReportProgress(progress, $"Updating {peopleToUpdate.Count} people...", 22);

                    int updatedCount = 0;
                    var updateSemaphore = new SemaphoreSlim(_config.MaxConcurrency);
                    var updateTasks = new List<Task>();

                    foreach (var (existing, record) in peopleToUpdate)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await updateSemaphore.WaitAsync(cancellationToken);

                        var capturedExisting = existing;
                        var capturedRecord = record;
                        updateTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var nameParts = capturedRecord.Name.Split(' ');
                                capturedExisting.GivenName = nameParts[0];
                                capturedExisting.Surname = nameParts.Length > 1 ? nameParts[1] : "";
                                capturedExisting.Addresses = new AddressInfo[]
                                {
                                    new PhoneInfo { Number = capturedRecord.Phone, Type = "Mobile" },
                                    new EmailAddressInfo { MailTo = capturedRecord.Email, Type = "Home" },
                                    ParseSingleStringAddress(capturedRecord.Address)
                                };

                                await ExecuteWithRetryAsync(
                                    () => client.UpdatePersonAsync(capturedExisting),
                                    $"Update '{capturedRecord.Name}'");
                                Interlocked.Increment(ref updatedCount);
                                Log($"Updated '{capturedRecord.Name}' (Key={capturedExisting.Key})");
                            }
                            catch (Exception ex)
                            {
                                var warning = $"Failed to update '{capturedRecord.Name}': {ex.Message}";
                                lock (result.Warnings) { result.Warnings.Add(warning); }
                                Log($"Warning: {warning}");
                            }
                            finally
                            {
                                updateSemaphore.Release();
                            }
                        }, cancellationToken));
                    }

                    await Task.WhenAll(updateTasks);
                    result.PeopleUpdated = updatedCount;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 5. Query for all people to match records for access level assignment.
                // Only needed for people that were just created or updated (not skipped).
                ReportProgress(progress, "Retrieving people for access level assignment...", 30);
                var matchedPeopleByName = new Dictionary<string, PersonInfo>();

                // For updates, we already have the PersonInfo objects
                foreach (var (existing, record) in peopleToUpdate)
                {
                    if (!matchedPeopleByName.ContainsKey(record.Name))
                        matchedPeopleByName[record.Name] = existing;
                }

                // For newly created people (and CreateNew duplicates), we need to query the API
                var namesToFind = new HashSet<string>();
                foreach (var record in records)
                {
                    if (skippedNames.Contains(record.Name))
                        continue;
                    if (matchedPeopleByName.ContainsKey(record.Name))
                        continue;
                    namesToFind.Add(record.Name);
                }

                if (namesToFind.Count > 0)
                {
                    Log($"Querying API for {namesToFind.Count} newly created people...");
                    page = 0;
                    while (true)
                    {
                        var peoplePage = await client.GetPeopleAsync(instance, page, pageSize);
                        if (peoplePage == null || !peoplePage.Any())
                            break;

                        foreach (var p in peoplePage)
                        {
                            if (namesToFind.Contains(p.CommonName) && !matchedPeopleByName.ContainsKey(p.CommonName))
                            {
                                matchedPeopleByName[p.CommonName] = p;
                                namesToFind.Remove(p.CommonName);
                            }
                        }

                        // Stop early if we've found everyone we need
                        if (namesToFind.Count == 0)
                            break;

                        if (peoplePage.Count() < pageSize)
                            break;

                        page++;
                        await Task.Delay(_config.ApiCallDelayMs, cancellationToken);
                    }
                }
                Log($"DEBUG: Matched {matchedPeopleByName.Count} people for access level assignment");

                // 6. Build batched access assignments — one API call per person with all their link items
                var perPersonAssignments = new List<(PersonInfo Person, List<ObjectLinkItem> LinkItems, string Description)>();
                DateTime now = DateTime.UtcNow;
                Log($"DEBUG: Current UTC time = {now:O}");

                foreach (var record in records)
                {
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

                    DateTime dob = record.Birthday;
                    var linkItems = new List<ObjectLinkItem>();
                    var ruleDescs = new List<string>();

                    foreach (var rule in _config.AccessLevelRules)
                    {
                        var accessLevel = resolvedAccessLevels[rule];
                        DateTime activeOn = rule.GetActiveOn(dob);
                        DateTime? expiresOn = rule.GetExpiresOn(dob);

                        Log($"DEBUG:   Rule '{rule.Name}' (Age {rule.AgeRangeDisplay}): ActiveOn={activeOn:yyyy-MM-dd}, ExpiresOn={expiresOn?.ToString("yyyy-MM-dd") ?? "none"}");

                        if (expiresOn.HasValue && expiresOn.Value <= now)
                        {
                            Log($"DEBUG:   Skipping '{rule.Name}' (expired: {expiresOn.Value:yyyy-MM-dd} <= now)");
                            continue;
                        }

                        var effectiveExpiresOn = expiresOn ?? DateTime.UtcNow.AddYears(50);
                        var metaDoc = new BsonDocument
                        {
                            { "ActiveOn", new BsonDateTime(activeOn) },
                            { "ExpiresOn", new BsonDateTime(effectiveExpiresOn) }
                        };

                        linkItems.Add(new ObjectLinkItem
                        {
                            LinkedObjectKey = accessLevel.Key,
                            Relation = "ScheduledAccessLevel",
                            MetaDataBson = metaDoc.ToBson()
                        });

                        ruleDescs.Add(expiresOn.HasValue
                            ? $"{rule.Name} ({activeOn:yyyy-MM-dd} to {expiresOn.Value:yyyy-MM-dd})"
                            : $"{rule.Name} ({activeOn:yyyy-MM-dd}+)");
                    }

                    if (linkItems.Count > 0)
                    {
                        var desc = $"{record.Name} [{linkItems.Count} rule(s): {string.Join(", ", ruleDescs)}]";
                        perPersonAssignments.Add((person, linkItems, desc));
                    }
                }

                // 7. Execute batched access assignments in parallel
                int totalPersons = perPersonAssignments.Count;
                int totalRuleCount = perPersonAssignments.Sum(a => a.LinkItems.Count);
                Log($"Assigning {totalRuleCount} scheduled access levels across {totalPersons} people (concurrency={_config.MaxConcurrency})...");

                int completedPersons = 0;
                int assignedCount = 0;
                var assignSemaphore = new SemaphoreSlim(_config.MaxConcurrency);
                var assignTasks = new List<Task>();

                foreach (var (person, linkItems, description) in perPersonAssignments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await assignSemaphore.WaitAsync(cancellationToken);

                    var capturedPerson = person;
                    var capturedLinks = linkItems;
                    var capturedDesc = description;
                    var capturedCount = linkItems.Count;
                    assignTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ExecuteWithRetryAsync(
                                () => client.AddScheduledAccessLevelBatchForPersonAsync(capturedPerson, capturedLinks),
                                capturedDesc);
                            Interlocked.Add(ref assignedCount, capturedCount);
                            Log($"   -> SUCCESS: {capturedDesc}");
                        }
                        catch (Exception ex)
                        {
                            var details = FormatExceptionDetails(ex);
                            var warning = $"Failed to assign {capturedDesc}";
                            lock (result.Warnings) { result.Warnings.Add($"{warning}: {ex.Message}"); }
                            Log($"   -> FAILED: {warning}");
                            Log($"   -> Details: {details}");
                        }
                        finally
                        {
                            var done = Interlocked.Increment(ref completedPersons);
                            int progressPercent = 30 + (int)((done / (double)totalPersons) * 70);
                            ReportProgress(progress, $"Assigning access levels... ({done}/{totalPersons} people)", progressPercent);
                            assignSemaphore.Release();
                        }
                    }, cancellationToken));
                }

                await Task.WhenAll(assignTasks);
                result.AccessLevelsAssigned = assignedCount;

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
            var zipRegex = new Regex(@"(?:\d{5}(?:-\d{4})?)|(?:[A-Za-z]\d[A-ZaZ][ -]?\d[A-Za-z]\d)$");
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

        /// <summary>
        /// Deletes all people from the connected Feenics instance.
        /// This is a destructive operation and cannot be undone.
        /// </summary>
        public async Task<(int Deleted, int Failed, List<string> Errors)> DeleteAllPeopleAsync(
            IProgress<ImportProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            int deleted = 0;
            int failed = 0;
            var errors = new List<string>();

            var client = new Client(_config.ApiUrl);
            Log($"Connecting to API for delete operation...");

            var (success, error, msg) = await client.LoginAsync(_config.Instance, _config.Username, _config.Password);
            if (!success)
            {
                errors.Add($"Login failed: {msg}");
                return (deleted, failed, errors);
            }

            var instance = await client.GetCurrentInstanceAsync();
            Log($"Connected to: {instance.CommonName}");

            // Collect all people
            ReportProgress(progress, "Retrieving all people...", 5);
            var allPeople = new List<PersonInfo>();
            int page = 0;
            const int pageSize = 1000;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var peoplePage = await client.GetPeopleAsync(instance, page, pageSize);
                if (peoplePage == null || !peoplePage.Any())
                    break;

                allPeople.AddRange(peoplePage);

                if (peoplePage.Count() < pageSize)
                    break;

                page++;
                await Task.Delay(_config.ApiCallDelayMs, cancellationToken);
            }

            Log($"Found {allPeople.Count} people to delete.");

            if (allPeople.Count == 0)
            {
                ReportProgress(progress, "No people found.", 100);
                return (deleted, failed, errors);
            }

            // Delete people in parallel with throttle
            int completedDeletes = 0;
            var deleteSemaphore = new SemaphoreSlim(_config.MaxConcurrency);
            var deleteTasks = new List<Task>();
            Log($"Deleting {allPeople.Count} people (concurrency={_config.MaxConcurrency})...");

            foreach (var person in allPeople)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await deleteSemaphore.WaitAsync(cancellationToken);

                var capturedPerson = person;
                deleteTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ExecuteWithRetryAsync(
                            () => client.DeletePersonAsync(capturedPerson),
                            $"Delete '{capturedPerson.CommonName}'");
                        Interlocked.Increment(ref deleted);
                        Log($"Deleted '{capturedPerson.CommonName}' (Key={capturedPerson.Key})");
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        var errMsg = $"Failed to delete '{capturedPerson.CommonName}': {ex.Message}";
                        lock (errors) { errors.Add(errMsg); }
                        Log($"ERROR: {errMsg}");
                    }
                    finally
                    {
                        var done = Interlocked.Increment(ref completedDeletes);
                        int progressPercent = 10 + (int)((done / (double)allPeople.Count) * 90);
                        ReportProgress(progress, $"Deleting {done}/{allPeople.Count}...", progressPercent);
                        deleteSemaphore.Release();
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(deleteTasks);

            ReportProgress(progress, "Delete complete.", 100);
            Log($"Delete complete. Deleted: {deleted}, Failed: {failed}");
            return (deleted, failed, errors);
        }
    }
}
