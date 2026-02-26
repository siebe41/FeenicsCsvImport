using System;
using System.Collections.Generic;
using System.Text.Json;

namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Defines an access level rule that maps an age range to a Feenics access level.
    /// </summary>
    public class AccessLevelRule
    {
        /// <summary>
        /// Display name / Feenics access level name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Minimum age (in years) when this access level becomes active.
        /// </summary>
        public int StartAge { get; set; }

        /// <summary>
        /// Maximum age (in years) when this access level expires.
        /// Null means the access level never expires (permanent from StartAge onward).
        /// </summary>
        public int? EndAge { get; set; }

        /// <summary>
        /// If true, the access level will be created in Feenics if it doesn't already exist.
        /// </summary>
        public bool CreateIfMissing { get; set; }

        /// <summary>
        /// Calculates the activation date for a given date of birth.
        /// </summary>
        public DateTime GetActiveOn(DateTime dob)
        {
            return dob.AddYears(StartAge);
        }

        /// <summary>
        /// Calculates the expiration date for a given date of birth.
        /// Returns null if this rule has no end age (permanent access).
        /// An EndAge of 0 is treated as no expiry.
        /// </summary>
        public DateTime? GetExpiresOn(DateTime dob)
        {
            return EndAge.HasValue && EndAge.Value > 0 ? dob.AddYears(EndAge.Value) : (DateTime?)null;
        }

        /// <summary>
        /// Returns a display string for this rule's age range.
        /// </summary>
        public string AgeRangeDisplay
        {
            get
            {
                if (EndAge.HasValue && EndAge.Value > 0)
                    return $"{StartAge}-{EndAge.Value}";
                return $"{StartAge}+";
            }
        }

        /// <summary>
        /// Parses a JSON string into a list of AccessLevelRules.
        /// Expected format: [{"Name":"Pool","StartAge":12,"EndAge":14}, ...]
        /// If EndAge is null, 0, or missing, it defaults to StartAge + 50.
        /// </summary>
        public static List<AccessLevelRule> ParseFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Access level rules JSON is required.", nameof(json));

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<List<JsonRuleEntry>>(json, options);
            if (parsed == null || parsed.Count == 0)
                throw new InvalidOperationException("Access level rules JSON contained no rules.");

            var rules = new List<AccessLevelRule>();
            foreach (var entry in parsed)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                    throw new InvalidOperationException("Each access level rule must have a Name.");

                int? endAge = entry.EndAge;
                if (!endAge.HasValue || endAge.Value <= 0)
                    endAge = entry.StartAge + 50;

                rules.Add(new AccessLevelRule
                {
                    Name = entry.Name,
                    StartAge = entry.StartAge,
                    EndAge = endAge,
                    CreateIfMissing = false
                });
            }

            return rules;
        }

        private class JsonRuleEntry
        {
            public string Name { get; set; }
            public int StartAge { get; set; }
            public int? EndAge { get; set; }
        }
    }
}
