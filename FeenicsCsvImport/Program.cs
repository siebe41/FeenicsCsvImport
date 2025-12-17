using CsvHelper;
using Feenics.Keep.WebApi.Model;
using Feenics.Keep.WebApi.Wrapper;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FeenicsCsvImport
{
    class Program
    {
        // Replace with your actual Feenics Access Level Common Names
        static string PoolAccessId = "PoolOnlyAccess-Age12";
        static string PoolAndGymId = "PoolAndGymAccess-Age14";
        static string AllAccessId = "PoolAndGymAfterHoursAccess-Age18";

        // Delay between API calls in milliseconds to avoid rate limiting
        static int ApiCallDelayMs = 100;

        // Retry configuration for rate limiting (429)
        static int MaxRetries = 5;
        static int InitialRetryDelayMs = 1000; // 1 second initial delay
        static int MaxRetryDelayMs = 30000;    // 30 seconds max delay

        static async Task Main(string[] args)
        {
            AccessLevelInfo PoolAccess;
            AccessLevelInfo PoolAndGymAccess;
            AccessLevelInfo AllAccess;

            // 1. Authenticate
            var client = new Client("https://api.us.acresecurity.cloud");
            var (success, error, msg) = await client.LoginAsync("YOUR_INSTANCE", "YOUR_USER", "YOUR_PASS");

            if (!success)
            {
                Console.WriteLine($"Login Failed: {msg}");
                return;
            }

            var instance = await client.GetCurrentInstanceAsync();
            Console.WriteLine($"Connected to: {instance.CommonName}");

            var rootFolder = await client.GetFolderAsync(instance.InFolderHref);
            var accessLevelsFolder = await client.GetFolderAsync($"{instance.InFolderHref}/ACCESS_LEVELS");
            var accessLevels = await client.GetAccessLevelsAsync(accessLevelsFolder);

            PoolAccess = accessLevels.First(al => al.CommonName.ToString() == PoolAccessId);
            PoolAndGymAccess = accessLevels.First(al => al.CommonName.ToString() == PoolAndGymId);
            AllAccess = accessLevels.First(al => al.CommonName.ToString() == AllAccessId);

            // 2. Read CSV and collect all records
            List<UserCsvModel> records;
            using (var reader = new StreamReader("users.csv"))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                records = csv.GetRecords<UserCsvModel>().ToList();
            }

            // 3. Build PersonInfo objects for batch creation
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

            // 4. Batch add all people at once
            Console.WriteLine($"Adding {personInfoList.Count} people in batch...");
            int createdCount = await client.AddPeopleBatchAsync(rootFolder, personInfoList.ToArray());
            Console.WriteLine($"Successfully created {createdCount} people.");

            // 5. Query for all people in folder to get the server-assigned objects
            var allPeople = await client.GetPeopleAsync(rootFolder, records.Count);
            var createdPeopleByName = allPeople
                .Where(p => records.Any(r => r.Name == p.CommonName))
                .ToDictionary(p => p.CommonName, p => p);

            // 6. Build list of access assignment tasks
            var accessAssignmentTasks = new List<Func<Task>>();

            foreach (var record in records)
            {
                if (!createdPeopleByName.TryGetValue(record.Name, out var person))
                {
                    Console.WriteLine($"Warning: Could not find created person {record.Name}");
                    continue;
                }

                Console.WriteLine($"Queuing access assignments for {record.Name}...");

                // Calculate Dates
                DateTime dob = record.Birthday;
                DateTime age12 = dob.AddYears(12);
                DateTime age14 = dob.AddYears(14);
                DateTime age18 = dob.AddYears(18);
                DateTime now = DateTime.UtcNow;

                // Segment 1: Pool (12-14)
                if (age14 > now)
                {
                    var p = person;
                    var activeOn = age12;
                    var expiresOn = age14;
                    accessAssignmentTasks.Add(() => AssignScheduledAccess(client, p, PoolAccess, activeOn, expiresOn));
                }

                // Segment 2: Pool + Gym (14-18)
                if (age18 > now)
                {
                    var p = person;
                    var activeOn = age14;
                    var expiresOn = age18;
                    accessAssignmentTasks.Add(() => AssignScheduledAccess(client, p, PoolAndGymAccess, activeOn, expiresOn));
                }

                // Segment 3: All Access (18+)
                {
                    var p = person;
                    var activeOn = age18;
                    accessAssignmentTasks.Add(() => AssignScheduledAccess(client, p, AllAccess, activeOn, null));
                }
            }

            // 7. Execute all access assignment calls sequentially with delay
            Console.WriteLine($"Assigning {accessAssignmentTasks.Count} scheduled access levels...");
            for (int i = 0; i < accessAssignmentTasks.Count; i++)
            {
                await ExecuteWithRetryAsync(accessAssignmentTasks[i], $"Access assignment {i + 1}/{accessAssignmentTasks.Count}");

                // Add delay between calls to avoid overloading the API
                if (i < accessAssignmentTasks.Count - 1)
                {
                    await Task.Delay(ApiCallDelayMs);
                }
            }

            Console.WriteLine("Import complete.");
        }

        /// <summary>
        /// Executes an async action with retry logic for 429 (Too Many Requests) responses.
        /// Uses exponential backoff with jitter.
        /// </summary>
        static async Task ExecuteWithRetryAsync(Func<Task> action, string operationName)
        {
            int retryCount = 0;
            int currentDelayMs = InitialRetryDelayMs;
            var random = new Random();

            while (true)
            {
                try
                {
                    await action();
                    return; // Success, exit the retry loop
                }
                catch (Exception ex) when (Is429Exception(ex))
                {
                    retryCount++;
                    if (retryCount > MaxRetries)
                    {
                        Console.WriteLine($"   -> FAILED after {MaxRetries} retries: {operationName}");
                        throw;
                    }

                    // Add jitter: random delay between 0-25% of current delay
                    int jitter = random.Next(0, currentDelayMs / 4);
                    int delayWithJitter = currentDelayMs + jitter;

                    Console.WriteLine($"   -> Rate limited (429). Retry {retryCount}/{MaxRetries} after {delayWithJitter}ms: {operationName}");
                    await Task.Delay(delayWithJitter);

                    // Exponential backoff: double the delay for next retry, up to max
                    currentDelayMs = Math.Min(currentDelayMs * 2, MaxRetryDelayMs);
                }
            }
        }

        /// <summary>
        /// Checks if an exception represents a 429 Too Many Requests response.
        /// </summary>
        static bool Is429Exception(Exception ex)
        {
            // Check for HttpRequestException with 429 status
            if (ex is HttpRequestException httpEx)
            {
                // .NET Framework 4.8 HttpRequestException doesn't have StatusCode property,
                // so we check the message
                if (httpEx.Message.Contains("429") || httpEx.Message.Contains("Too Many Requests"))
                {
                    return true;
                }
            }

            // Check for WebException with 429 status
            if (ex is WebException webEx && webEx.Response is HttpWebResponse response)
            {
                if ((int)response.StatusCode == 429)
                {
                    return true;
                }
            }

            // Check inner exceptions
            if (ex.InnerException != null)
            {
                return Is429Exception(ex.InnerException);
            }

            // Also check if the exception message contains 429 or TooManyRequests
            if (ex.Message.Contains("429") || ex.Message.Contains("TooManyRequests") || ex.Message.Contains("Too Many Requests"))
            {
                return true;
            }

            return false;
        }

        static async Task AssignScheduledAccess(Client client, PersonInfo person, AccessLevelInfo accessLevel, DateTime activeOn, DateTime? expiresOn)
        {
            // Define the schedule metadata
            var metadata = new
            {
                ActiveOn = activeOn,
                ExpiresOn = expiresOn
            };

            // The "ScheduledAccessLevel" relation tells the background service to watch these dates
            await client.AssignConnectedObjectAsync(person, accessLevel, "ScheduledAccessLevel", false, metadata);

            Console.WriteLine($"   -> Scheduled {accessLevel.CommonName}: Start {activeOn.ToShortDateString()}");
        }

        public static MailingAddressInfo ParseSingleStringAddress(string rawAddress)
        {
            // Initialize with defaults
            var addressInfo = new MailingAddressInfo { Type = "Home", Country = "US" };

            if (string.IsNullOrWhiteSpace(rawAddress)) return addressInfo;

            // Normalize: remove extra spaces
            rawAddress = rawAddress.Trim();

            // 1. Try to find the Postal Code/Zip at the very end
            // Supports US (5 or 9 digits) and Canada (A1A 1A1)
            var zipRegex = new Regex(@"(?:\d{5}(?:-\d{4})?)|(?:[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d)$");
            var zipMatch = zipRegex.Match(rawAddress);

            if (zipMatch.Success)
            {
                addressInfo.PostalCode = zipMatch.Value;
                // Remove the zip from the string to make parsing the rest easier
                rawAddress = rawAddress.Substring(0, zipMatch.Index).Trim(',', ' ');
            }

            // 2. Try to find the State/Province (Assumes 2-letter abbreviation like ON, NY, CA) at the end
            var stateRegex = new Regex(@"\b([A-Za-z]{2})\b$");
            var stateMatch = stateRegex.Match(rawAddress);

            if (stateMatch.Success)
            {
                addressInfo.Province = stateMatch.Value.ToUpper();
                // Remove the state from the string
                rawAddress = rawAddress.Substring(0, stateMatch.Index).Trim(',', ' ');
            }

            // 3. Try to find the City (This is tricky, we assume it's the last part remaining after a comma)
            // Example: "101 Champagne Ave, Ottawa" -> Split by last comma
            int lastCommaIndex = rawAddress.LastIndexOf(',');
            if (lastCommaIndex > 0)
            {
                addressInfo.City = rawAddress.Substring(lastCommaIndex + 1).Trim();
                addressInfo.Street = rawAddress.Substring(0, lastCommaIndex).Trim();
            }
            else
            {
                // Fallback: If no commas, we can't reliably separate City from Street without an API
                // We put everything remaining into Street to avoid data loss.
                addressInfo.Street = rawAddress;
            }

            return addressInfo;
        }
    }

    public class UserCsvModel
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public DateTime Birthday { get; set; }
    }
}
