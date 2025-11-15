namespace BusinessObject.Enums
{
    /// <summary>
    /// Provider application type based on Tax ID
    /// </summary>
    public enum ProviderApplicationType
    {
        /// <summary>
        /// Individual provider (12-digit Tax ID / Personal ID)
        /// </summary>
        Individual,

        /// <summary>
        /// Business provider (10-digit Tax ID / Business registration number)
        /// </summary>
        Business
    }
}
