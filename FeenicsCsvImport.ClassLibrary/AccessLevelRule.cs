using System;

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
    }
}
