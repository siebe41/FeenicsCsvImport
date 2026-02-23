using System;
using System.Collections.Generic;

namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Represents the calculated status and dates for a single access level rule applied to a user.
    /// </summary>
    public class AccessLevelPreview
    {
        public string RuleName { get; set; }
        public string AgeRange { get; set; }
        public string Status { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    /// <summary>
    /// Model for previewing import data with calculated access levels
    /// </summary>
    public class ImportPreviewModel
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public DateTime Birthday { get; set; }

        /// <summary>
        /// Calculated access level previews, one per rule.
        /// </summary>
        public List<AccessLevelPreview> AccessLevels { get; set; } = new List<AccessLevelPreview>();

        /// <summary>
        /// Creates a preview model from a CSV record with calculated access dates based on the provided rules.
        /// </summary>
        public static ImportPreviewModel FromCsvRecord(UserCsvModel record, IList<AccessLevelRule> rules)
        {
            DateTime dob = record.Birthday;
            DateTime now = DateTime.UtcNow;

            var preview = new ImportPreviewModel
            {
                Name = record.Name,
                Email = record.Email,
                Phone = record.Phone,
                Address = record.Address,
                Birthday = record.Birthday
            };

            foreach (var rule in rules)
            {
                DateTime activeOn = rule.GetActiveOn(dob);
                DateTime? expiresOn = rule.GetExpiresOn(dob);

                string status;
                if (expiresOn.HasValue && expiresOn.Value <= now)
                {
                    status = "Expired";
                }
                else if (activeOn <= now)
                {
                    status = "Active";
                }
                else
                {
                    status = "Scheduled";
                }

                preview.AccessLevels.Add(new AccessLevelPreview
                {
                    RuleName = rule.Name,
                    AgeRange = rule.AgeRangeDisplay,
                    Status = status,
                    Start = activeOn,
                    End = expiresOn
                });
            }

            return preview;
        }
    }
}
