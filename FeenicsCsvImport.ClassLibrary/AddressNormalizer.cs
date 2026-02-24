using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Provides fuzzy street address matching by normalizing street portions of addresses.
    /// Strips city, state, and zip — only the street number and name are used for comparison.
    /// Expands common abbreviations (St ? Street, Ave ? Avenue, etc.) so that
    /// "123 Main St" matches "123 Main Street".
    /// </summary>
    public static class AddressNormalizer
    {
        private static readonly Dictionary<string, string> Abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Street type abbreviations
            { "st", "street" },
            { "str", "street" },
            { "ave", "avenue" },
            { "av", "avenue" },
            { "blvd", "boulevard" },
            { "bvd", "boulevard" },
            { "cir", "circle" },
            { "ct", "court" },
            { "crt", "court" },
            { "dr", "drive" },
            { "drv", "drive" },
            { "hwy", "highway" },
            { "hw", "highway" },
            { "ln", "lane" },
            { "la", "lane" },
            { "pkwy", "parkway" },
            { "pky", "parkway" },
            { "pk", "parkway" },
            { "pl", "place" },
            { "plz", "plaza" },
            { "rd", "road" },
            { "sq", "square" },
            { "ter", "terrace" },
            { "terr", "terrace" },
            { "trl", "trail" },
            { "tr", "trail" },
            { "wy", "way" },
            // Directional abbreviations
            { "n", "north" },
            { "s", "south" },
            { "e", "east" },
            { "w", "west" },
            { "ne", "northeast" },
            { "nw", "northwest" },
            { "se", "southeast" },
            { "sw", "southwest" },
            // Unit/apartment abbreviations
            { "apt", "apartment" },
            { "ste", "suite" },
            { "fl", "floor" },
            { "bldg", "building" },
        };

        /// <summary>
        /// Extracts just the street portion from a raw single-line address (strips city, state, zip)
        /// and normalizes it for fuzzy comparison.
        /// </summary>
        public static string NormalizeStreet(string rawAddress)
        {
            if (string.IsNullOrWhiteSpace(rawAddress))
                return "";

            var street = ExtractStreetPortion(rawAddress.Trim());
            return NormalizeStreetValue(street);
        }

        /// <summary>
        /// Normalizes a street value that has already been extracted (e.g. from MailingAddressInfo.Street).
        /// </summary>
        public static string NormalizeStreetValue(string street)
        {
            if (string.IsNullOrWhiteSpace(street))
                return "";

            var normalized = street.Trim().ToLowerInvariant();

            // Remove punctuation (periods, commas, hashes)
            normalized = Regex.Replace(normalized, @"[.,#]", "");

            // Collapse multiple spaces
            normalized = Regex.Replace(normalized, @"\s+", " ");

            // Expand abbreviations word-by-word
            var words = normalized.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (Abbreviations.TryGetValue(words[i], out var expanded))
                {
                    words[i] = expanded;
                }
            }

            return string.Join(" ", words);
        }

        /// <summary>
        /// Checks whether two raw single-line addresses match on the street portion,
        /// ignoring city, state, zip, and common abbreviation differences.
        /// </summary>
        public static bool StreetsMatch(string address1, string address2)
        {
            var n1 = NormalizeStreet(address1);
            var n2 = NormalizeStreet(address2);
            return !string.IsNullOrEmpty(n1) && n1 == n2;
        }

        /// <summary>
        /// Checks whether a normalized CSV street matches a MailingAddressInfo.Street value.
        /// </summary>
        public static bool NormalizedStreetMatchesPersonStreet(string normalizedCsvStreet, string personStreet)
        {
            if (string.IsNullOrEmpty(normalizedCsvStreet))
                return false;
            return normalizedCsvStreet == NormalizeStreetValue(personStreet);
        }

        /// <summary>
        /// Extracts the street portion from a single-line address by stripping
        /// the trailing zip, state, and city components (same logic as ParseSingleStringAddress).
        /// </summary>
        private static string ExtractStreetPortion(string rawAddress)
        {
            // Strip trailing postal code
            var zipRegex = new Regex(@"(?:\d{5}(?:-\d{4})?)|(?:[A-Za-z]\d[A-Za-z][ -]?\d[A-Za-z]\d)$");
            var zipMatch = zipRegex.Match(rawAddress);
            if (zipMatch.Success)
                rawAddress = rawAddress.Substring(0, zipMatch.Index).Trim(',', ' ');

            // Strip trailing state (2-letter code)
            var stateRegex = new Regex(@"\b([A-Za-z]{2})\b$");
            var stateMatch = stateRegex.Match(rawAddress);
            if (stateMatch.Success)
                rawAddress = rawAddress.Substring(0, stateMatch.Index).Trim(',', ' ');

            // Strip trailing city (everything after the last comma is the city)
            int lastComma = rawAddress.LastIndexOf(',');
            if (lastComma > 0)
                rawAddress = rawAddress.Substring(0, lastComma).Trim();

            return rawAddress;
        }
    }
}
