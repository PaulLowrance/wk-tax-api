namespace TaxApiSample.Configuration;

/// <summary>
/// Base URLs and behavior toggles for the CCH Axcess Tax APIs.
/// Bound from the "CchTaxApi" section of appsettings.json.
/// </summary>
public sealed class CchApiOptions
{
    public const string SectionName = "CchTaxApi";

    public string AuthBaseUrl { get; set; } = string.Empty;

    public string TaxServiceBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// When true, the "login" command calls AzureAuthenticate instead of Authenticate.
    /// </summary>
    public bool UseAzureAuthenticate { get; set; }
}
