namespace IKJ.Client;

public static class Configuration
{
    /// <summary>
    ///     URL to IKJ's test environment.
    /// </summary>
    public const string ApiBaseUrl = "https://api.ikj.test.nhn.no/v1";

    /// <summary>
    ///     Example code system for KPR (i.e. IPLOS) codes.
    /// </summary>
    public const string KprCodeSystem = "https://komsat.samples.example.com/ikj/kpr-codes";

    /// <summary>
    ///     Example code system for managingOrganization area codes.
    /// </summary>
    public const string AreaCodeSystem = "https://komsat.samples.example.com/some-epj/area-codes";

    /// <summary>
    ///     Organization number for the organization you are acting on behalf of; i.e. the municipality responsible
    ///     for the Electronic Healthcare Records.
    ///     The number specified is used when requesting HelseID access tokens. The HelseID Test client provided in
    ///     this sample has been delegated access to organizations 315945062 and 313004600.
    /// </summary>
    public const string ParentOrgNumber = "315945062";

    /// <summary>
    ///     Max. number of times to poll a Task before giving up.
    /// </summary>
    public const int PollTaskStatusMaxAttempts = 3;

    /// <summary>
    ///     Number of resources returned per page
    /// </summary>
    public const int PageSize = 3;

    /// <summary>
    ///     Number of pages read
    /// </summary>
    public const int PageMaxCount = 3;
}
