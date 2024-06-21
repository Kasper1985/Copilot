namespace WebApi.Options;

/// <summary>
/// Configuration options for Azure speech recognition and synthesis.
/// </summary>
public class AzureSpeechOptions
{
    public const string SectionName = "AzureSpeech";
    
    /// <summary>
    /// SubscriptionKey to access the Azure speech service.
    /// </summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>
    /// Location of the Azure speech service to use (e.g. "South Central US")
    /// </summary>
    public string Region { get; set; } = string.Empty;
    /// <summary>
    /// Name of the voice to use for speech synthesis.
    /// </summary>
    public string VoiceName { get; set; } = string.Empty;
    /// <summary>
    /// Language to use for speech synthesis.
    /// </summary>
    public string Language { get; set; } = string.Empty;
}
