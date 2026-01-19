namespace SaveHere.Models
{
    /// <summary>
    /// Authentication types supported for HTTP downloads.
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// No authentication required.
        /// </summary>
        None = 0,

        /// <summary>
        /// HTTP Basic Authentication (username/password).
        /// </summary>
        BasicAuth = 1,

        /// <summary>
        /// Bearer token authentication (OAuth, JWT, etc.).
        /// </summary>
        BearerToken = 2,

        /// <summary>
        /// Cookie-based authentication.
        /// </summary>
        Cookie = 3,

        /// <summary>
        /// Custom HTTP headers for authentication.
        /// </summary>
        CustomHeaders = 4
    }
}
