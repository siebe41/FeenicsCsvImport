using Feenics.Keep.WebApi.Model;
using Feenics.Keep.WebApi.Wrapper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FeenicsCsvImport.ClassLibrary
{
    public class DoorUsageEntry
    {
        public string PersonKey { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime LastUsedUtc { get; set; }
        public int EventCount { get; set; }
    }

    public class DoorUsageReportResult
    {
        public bool Success { get; set; }
        public List<DoorUsageEntry> People { get; set; } = new List<DoorUsageEntry>();
        public int EventsScanned { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Queries acre/Feenics Keep event history for a given door (reader/portal) and reports which
    /// people triggered access events there, with the email from their profile.
    ///
    /// The Feenics.Keep.WebApi.Wrapper NuGet SDK used elsewhere in this project does not have a
    /// publicly documented "get door events" method that could be verified from this environment
    /// (the docs site apidocs.feenics.com / apidocs.acresecurity.cloud and the API host itself are
    /// both unreachable from the sandbox this was written in), so this talks to the documented raw
    /// REST endpoints directly instead:
    ///   POST {apiUrl}/token                              (OAuth2 password grant)
    ///   POST {apiUrl}/api/f/{instanceKey}/aggregate/Events (MongoDB-style aggregation pipeline)
    /// Field names below (OccurredOn, ObjectLinks, MessageLong, EventData.Reader) come from public
    /// Feenics API documentation. They have NOT been verified against a live instance. Run with
    /// dumpSampleCount (DumpRecentEventsAsync) first to confirm the field names/date format your
    /// instance actually returns, and adjust BuildMatchStage / ExtractLinkedKey / ParseEventDate
    /// if they differ.
    /// </summary>
    public class DoorUsageReportService
    {
        private const string TokenClientId = "consoleApp";
        private const string TokenClientSecret = "consoleSecret";

        private readonly string _apiUrl;
        private readonly string _instance;
        private readonly string _username;
        private readonly string _password;
        private readonly Action<string> _logger;

        public DoorUsageReportService(string apiUrl, string instance, string username, string password, Action<string> logger = null)
        {
            if (string.IsNullOrWhiteSpace(apiUrl)) throw new ArgumentException("API URL is required.", nameof(apiUrl));
            if (string.IsNullOrWhiteSpace(instance)) throw new ArgumentException("Instance is required.", nameof(instance));
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required.", nameof(username));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.", nameof(password));

            _apiUrl = apiUrl.TrimEnd('/');
            _instance = instance;
            _username = username;
            _password = password;
            _logger = logger ?? (_ => { });
        }

        private void Log(string message) => _logger(message);

        /// <summary>
        /// Fetches and logs the N most recent raw events (within the last daysBack days) as JSON,
        /// to help identify the correct field names/shapes for your instance before trusting
        /// RunAsync's results. A date filter always leads the pipeline: on a live instance, sorting
        /// the full Events collection with no $match first hit a server-side MongoExecutionTimeoutException,
        /// so this narrows to a recent window before sorting, same as RunAsync does.
        /// </summary>
        public async Task DumpRecentEventsAsync(int count = 5, int daysBack = 30)
        {
            var instance = await GetInstanceAsync();
            var token = await GetAccessTokenAsync(_instance);

            DateTime sinceUtc = DateTime.UtcNow.AddDays(-daysBack);
            Log($"Looking at events since {sinceUtc:yyyy-MM-dd} (last {daysBack} day(s))...");

            var pipeline = new JArray
            {
                new JObject { ["$match"] = new JObject { ["OccurredOn"] = new JObject { ["$gte"] = sinceUtc.ToString("O", CultureInfo.InvariantCulture) } } },
                new JObject { ["$sort"] = new JObject { ["OccurredOn"] = -1 } },
                new JObject { ["$limit"] = count }
            };

            var events = await PostAggregateEventsAsync(token, instance.Key.ToString(), pipeline);
            Log($"--- {events.Count} most recent event(s) (raw JSON) ---");
            if (events.Count == 0)
            {
                Log($"No events found in the last {daysBack} day(s). Try a larger --dump-events-days value.");
            }
            foreach (var ev in events)
            {
                Log(ev.ToString(Formatting.Indented));
            }
        }

        /// <summary>
        /// Builds the report: distinct people whose access events at a door matching doorNameContains
        /// occurred within the last `months` months, with their profile email.
        /// </summary>
        public async Task<DoorUsageReportResult> RunAsync(string doorNameContains, int months = 9, bool includeDenied = false)
        {
            var result = new DoorUsageReportResult();
            if (string.IsNullOrWhiteSpace(doorNameContains))
            {
                result.Errors.Add("Door name is required.");
                return result;
            }

            try
            {
                var client = new Client(_apiUrl);
                var (success, error, msg) = await client.LoginAsync(_instance, _username, _password);
                if (!success)
                {
                    result.Errors.Add($"Login failed: {msg}");
                    return result;
                }

                var instance = await client.GetCurrentInstanceAsync();
                Log($"Connected to: {instance.CommonName}");

                var token = await GetAccessTokenAsync(_instance);
                DateTime sinceUtc = DateTime.UtcNow.AddMonths(-months);

                Log($"Querying event history for door matching '{doorNameContains}' since {sinceUtc:yyyy-MM-dd}...");
                var events = await QueryAllEventsAsync(token, instance.Key.ToString(), sinceUtc, doorNameContains, includeDenied);
                result.EventsScanned = events.Count;
                Log($"Matched {events.Count} event(s).");

                if (events.Count == 0)
                {
                    result.Success = true;
                    result.Warnings.Add("No matching events found. Run DumpRecentEventsAsync to inspect real field names, or double-check the door name spelling against your instance's reader/portal names.");
                    return result;
                }

                Log("Loading people directory to resolve names/emails...");
                var peopleByKey = await LoadAllPeopleByKeyAsync(client, instance);
                Log($"Loaded {peopleByKey.Count} people.");

                var involved = new Dictionary<string, DoorUsageEntry>();
                foreach (var ev in events)
                {
                    DateTime occurredOn = ParseEventDate(ev["OccurredOn"]) ?? DateTime.MinValue;
                    var links = ev["ObjectLinks"] as JArray;
                    if (links == null) continue;

                    foreach (var linkToken in links)
                    {
                        if (!(linkToken is JObject link)) continue;

                        string linkedKey = ExtractLinkedKey(link);
                        if (string.IsNullOrEmpty(linkedKey) || !peopleByKey.TryGetValue(linkedKey, out var person))
                            continue;

                        if (!involved.TryGetValue(linkedKey, out var entry))
                        {
                            entry = new DoorUsageEntry
                            {
                                PersonKey = linkedKey,
                                Name = person.CommonName,
                                Email = GetEmail(person),
                                LastUsedUtc = occurredOn
                            };
                            involved[linkedKey] = entry;
                        }

                        entry.EventCount++;
                        if (occurredOn > entry.LastUsedUtc)
                            entry.LastUsedUtc = occurredOn;
                    }
                }

                result.People = involved.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
                result.Success = true;
                Log($"{result.People.Count} distinct people used a door matching '{doorNameContains}' in the last {months} month(s).");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                Log($"Door usage report failed: {ex}");
            }

            return result;
        }

        private async Task<InstanceInfo> GetInstanceAsync()
        {
            var client = new Client(_apiUrl);
            var (success, error, msg) = await client.LoginAsync(_instance, _username, _password);
            if (!success)
                throw new Exception($"Login failed: {msg}");
            return await client.GetCurrentInstanceAsync();
        }

        private async Task<Dictionary<string, PersonInfo>> LoadAllPeopleByKeyAsync(Client client, InstanceInfo instance)
        {
            var byKey = new Dictionary<string, PersonInfo>();
            int page = 0;
            const int pageSize = 1000;
            while (true)
            {
                var peoplePage = await client.GetPeopleAsync(instance, page, pageSize);
                if (peoplePage == null || !peoplePage.Any())
                    break;

                foreach (var p in peoplePage)
                {
                    var key = p.Key?.ToString();
                    if (!string.IsNullOrEmpty(key))
                        byKey[key] = p;
                }

                if (peoplePage.Count() < pageSize)
                    break;
                page++;
            }
            return byKey;
        }

        private static string GetEmail(PersonInfo person)
        {
            var email = person.Addresses?.OfType<EmailAddressInfo>().FirstOrDefault();
            return email?.MailTo;
        }

        /// <summary>
        /// OAuth2 password-grant login against the raw token endpoint (not the wrapper), so we hold
        /// a bearer token usable for the raw aggregate/Events call. Client id/secret and username
        /// format ("instance\username") come from Feenics' published API quick-start docs.
        /// </summary>
        private async Task<string> GetAccessTokenAsync(string instanceName)
        {
            using (var http = new HttpClient())
            {
                var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = TokenClientId,
                    ["client_secret"] = TokenClientSecret,
                    ["username"] = $"{instanceName}\\{_username}",
                    ["password"] = _password,
                    ["instance"] = instanceName,
                    ["sendonetimepassword"] = "false"
                });

                var response = await http.PostAsync($"{_apiUrl}/token", form);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Token request failed ({(int)response.StatusCode}): {body}");

                var json = JObject.Parse(body);
                var token = json.Value<string>("access_token") ?? json.Value<string>("accessToken");
                if (string.IsNullOrEmpty(token))
                    throw new Exception($"Token response did not contain an access token: {body}");
                return token;
            }
        }

        private async Task<List<JObject>> QueryAllEventsAsync(string token, string instanceKey, DateTime sinceUtc, string doorNameContains, bool includeDenied)
        {
            var results = new List<JObject>();
            const int pageSize = 500;
            int skip = 0;

            while (true)
            {
                var pipeline = new JArray
                {
                    new JObject { ["$match"] = BuildMatchStage(sinceUtc, doorNameContains, includeDenied) },
                    new JObject { ["$sort"] = new JObject { ["OccurredOn"] = -1 } },
                    new JObject
                    {
                        ["$project"] = new JObject
                        {
                            ["OccurredOn"] = 1,
                            ["MessageLong"] = 1,
                            ["ObjectLinks"] = 1,
                            ["EventData.Reader"] = 1
                        }
                    },
                    new JObject { ["$skip"] = skip },
                    new JObject { ["$limit"] = pageSize }
                };

                var page = await PostAggregateEventsAsync(token, instanceKey, pipeline);
                results.AddRange(page);
                if (page.Count < pageSize)
                    break;
                skip += pageSize;
            }

            return results;
        }

        private static JObject BuildMatchStage(DateTime sinceUtc, string doorNameContains, bool includeDenied)
        {
            var doorRegex = Regex.Escape(doorNameContains);

            var conditions = new JArray
            {
                new JObject { ["OccurredOn"] = new JObject { ["$gte"] = sinceUtc.ToString("O", CultureInfo.InvariantCulture) } },
                new JObject
                {
                    ["$or"] = new JArray
                    {
                        new JObject { ["MessageLong"] = new JObject { ["$regex"] = doorRegex, ["$options"] = "i" } },
                        new JObject { ["EventData.Reader.CommonName"] = new JObject { ["$regex"] = doorRegex, ["$options"] = "i" } },
                        new JObject { ["EventData.Reader.Name"] = new JObject { ["$regex"] = doorRegex, ["$options"] = "i" } }
                    }
                }
            };

            if (!includeDenied)
            {
                conditions.Add(new JObject { ["MessageLong"] = new JObject { ["$regex"] = "grant", ["$options"] = "i" } });
            }

            return new JObject { ["$and"] = conditions };
        }

        private async Task<List<JObject>> PostAggregateEventsAsync(string token, string instanceKey, JArray pipeline)
        {
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var url = $"{_apiUrl}/api/f/{instanceKey}/aggregate/Events";

                // Confirmed against a live instance: the endpoint rejects an array of raw nested
                // stage objects ("unexpected character '{'"/"':'" parser errors). It wants each
                // pipeline stage individually JSON-encoded as a string inside the outer array.
                var stageStrings = new JArray(pipeline.Select(stage => (JToken)new JValue(stage.ToString(Formatting.None))));
                var content = new StringContent(stageStrings.ToString(Formatting.None), Encoding.UTF8, "application/json");

                var response = await http.PostAsync(url, content);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Event query failed ({(int)response.StatusCode}): {body}");

                var parsed = JToken.Parse(body);
                var array = parsed as JArray
                    ?? parsed["value"] as JArray
                    ?? parsed["results"] as JArray
                    ?? parsed["Results"] as JArray;

                var results = new List<JObject>();
                if (array != null)
                {
                    foreach (var item in array)
                        if (item is JObject jo) results.Add(jo);
                }
                return results;
            }
        }

        /// <summary>
        /// The link relating an event to a person could come back under a few different property
        /// names depending on how the instance's Events collection is shaped; try the ones the
        /// wrapper uses elsewhere in this project (see ObjectLinkItem.LinkedObjectKey in ImportService)
        /// plus common REST/Mongo variants.
        /// </summary>
        private static string ExtractLinkedKey(JObject link)
        {
            foreach (var candidate in new[] { "LinkedObjectKey", "LinkedObjectId", "ObjectId", "Key" })
            {
                var value = link.Value<string>(candidate);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }
            return null;
        }

        private static DateTime? ParseEventDate(JToken token)
        {
            if (token == null) return null;

            if (token.Type == JTokenType.Date)
                return (DateTime)token;

            if (token.Type == JTokenType.String &&
                DateTime.TryParse((string)token, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed;

            if (token.Type == JTokenType.Object && token["$date"] != null)
                return ParseEventDate(token["$date"]);

            return null;
        }
    }
}
