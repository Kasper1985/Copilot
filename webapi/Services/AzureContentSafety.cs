using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using WebApi.Models.Request;
using WebApi.Models.Response;
using WebApi.Services.Interfaces;
using ImageContent = WebApi.Models.Response.ImageContent;

namespace WebApi.Services;

/// <summary>
/// Moderator service to handle content safety.
/// </summary>
public sealed class AzureContentSafety : IContentSafetyService
{
    private const string HttpUserAgent = "Chat Copilot";

    private readonly string _endpoint;
    private readonly HttpClient _httpClient;
    private readonly HttpClientHandler? _httpClientHandler;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureContentSafety"/> class.
    /// </summary>
    /// <param name="endpoint">Endpoint for service API call.</param>
    /// <param name="apiKey">The API key.</param>
    /// <param name="httpClientHandler">Instance of <see cref="HttpClientHandler"/> to setup specific scenarios.</param>
    public AzureContentSafety(string endpoint, string apiKey, HttpClientHandler httpClientHandler)
    {
        _endpoint = endpoint;
        _httpClient = new HttpClient(httpClientHandler);

        _httpClient.DefaultRequestHeaders.Add("User-Agent", HttpUserAgent);

        // Subscription Key header required to authenticate requests to Azure API Management (APIM) service
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AzureContentSafety"/> class.
    /// </summary>
    /// <param name="endpoint">Endpoint for service API call.</param>
    /// <param name="apiKey">The API key.</param>
    public AzureContentSafety(string endpoint, string apiKey)
    {
        _endpoint = endpoint;

        _httpClientHandler = new HttpClientHandler { CheckCertificateRevocationList = true };
        _httpClient = new HttpClient(_httpClientHandler);

        _httpClient.DefaultRequestHeaders.Add("User-Agent", HttpUserAgent);

        // Subscription Key header required to authenticate requests to Azure API Management (APIM) service
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
    }
    
    /// <inheritdoc/>
    public List<string> ParseViolatedCategories(ImageAnalysisResponse imageAnalysisResponse, short threshold)
    {
        var violatedCategories = new List<string>();

        foreach (var property in typeof(ImageAnalysisResponse).GetProperties())
            if (property.GetValue(imageAnalysisResponse) is AnalysisResult analysisResult && analysisResult.Severity >= threshold)
                violatedCategories.Add($"{analysisResult.Category} ({analysisResult.Severity})");
        
        return violatedCategories;
    }
    
    /// <inheritdoc/>
    public async Task<ImageAnalysisResponse> ImageAnalysis(IFormFile formFile, CancellationToken cancellationToken)
    {
        // Convert the form file to a base64 string
        var base64Image = await ConvertFormFileToBase64(formFile);
        var image = base64Image
            .Replace("data:image/png;base64,", "", StringComparison.InvariantCultureIgnoreCase)
            .Replace("data:image/jpeg;base64,", "", StringComparison.InvariantCultureIgnoreCase);

        var content = new ImageContent(image);
        var requestBody = new ImageAnalysisRequest(content);

        using var httpRequestMessage = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{_endpoint}/contentsafety/image:analyze?api-version=2023-04-30-preview"),
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        
        var response = await _httpClient.SendAsync(httpRequestMessage, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || body is null)
            throw new KernelException($"[ContentSafety] Image analysis failed: {response.StatusCode} - {body}");
        
        var result = JsonSerializer.Deserialize<ImageAnalysisResponse>(body);
        if (result is null)
            throw new KernelException($"[ContentSafety] Image analysis failed: {body}");

        return result;
    }
    
    /// <inheritdoc/>
    public void Dispose()
    {
        _httpClientHandler?.Dispose();
        _httpClient.Dispose();
    }
    
    
    /// <summary>
    /// Helper method to convert a form file to a base64 string.
    /// </summary>
    /// <param name="formFile">An <see cref="IFormFile"/> object.</param>
    /// <returns>A base64 string of the content of the <see cref="IFormFile"/>'s object.</returns>
    private static async Task<string> ConvertFormFileToBase64(IFormFile formFile)
    {
        using var memoryStream = new MemoryStream();
        await formFile.CopyToAsync(memoryStream);
        return Convert.ToBase64String(memoryStream.ToArray());
    }
}
