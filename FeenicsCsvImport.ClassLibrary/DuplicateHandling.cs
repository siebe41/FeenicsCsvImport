namespace FeenicsCsvImport.ClassLibrary
{
    /// <summary>
    /// Defines how existing people (matched by name) are handled during import.
    /// </summary>
    public enum DuplicateHandling
    {
        /// <summary>
        /// Skip the CSV record if a person with the same name already exists.
        /// </summary>
        Skip,

        /// <summary>
        /// Update the existing person's data with the CSV record values.
        /// </summary>
        Update,

        /// <summary>
        /// Always create a new person, even if one with the same name already exists.
        /// This may result in duplicate entries.
        /// </summary>
        CreateNew
    }
}
