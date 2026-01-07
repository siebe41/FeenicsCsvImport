using System;

namespace FeenicsCsvImport.ClassLibrary
{
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

        // Pool Access (Age 12-14)
        public string PoolAccessStatus { get; set; }
        public DateTime? PoolAccessStart { get; set; }
        public DateTime? PoolAccessEnd { get; set; }

        // Pool + Gym Access (Age 14-18)
        public string PoolGymAccessStatus { get; set; }
        public DateTime? PoolGymAccessStart { get; set; }
        public DateTime? PoolGymAccessEnd { get; set; }

        // All Access (Age 18+)
        public string AllAccessStatus { get; set; }
        public DateTime? AllAccessStart { get; set; }

        /// <summary>
        /// Creates a preview model from a CSV record with calculated access dates
        /// </summary>
        public static ImportPreviewModel FromCsvRecord(UserCsvModel record)
        {
            DateTime dob = record.Birthday;
            DateTime age12 = dob.AddYears(12);
            DateTime age14 = dob.AddYears(14);
            DateTime age18 = dob.AddYears(18);
            DateTime now = DateTime.UtcNow;

            var preview = new ImportPreviewModel
            {
                Name = record.Name,
                Email = record.Email,
                Phone = record.Phone,
                Address = record.Address,
                Birthday = record.Birthday
            };

            // Pool Access (12-14)
            if (age14 <= now)
            {
                preview.PoolAccessStatus = "Expired";
            }
            else if (age12 <= now)
            {
                preview.PoolAccessStatus = "Active";
            }
            else
            {
                preview.PoolAccessStatus = "Scheduled";
            }
            preview.PoolAccessStart = age12;
            preview.PoolAccessEnd = age14;

            // Pool + Gym Access (14-18)
            if (age18 <= now)
            {
                preview.PoolGymAccessStatus = "Expired";
            }
            else if (age14 <= now)
            {
                preview.PoolGymAccessStatus = "Active";
            }
            else
            {
                preview.PoolGymAccessStatus = "Scheduled";
            }
            preview.PoolGymAccessStart = age14;
            preview.PoolGymAccessEnd = age18;

            // All Access (18+)
            if (age18 <= now)
            {
                preview.AllAccessStatus = "Active";
            }
            else
            {
                preview.AllAccessStatus = "Scheduled";
            }
            preview.AllAccessStart = age18;

            return preview;
        }
    }
}
