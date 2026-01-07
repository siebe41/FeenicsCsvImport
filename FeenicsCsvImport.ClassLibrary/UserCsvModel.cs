using System;

namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Model representing a user record from CSV file
    /// </summary>
    public class UserCsvModel
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public DateTime Birthday { get; set; }
    }
}
