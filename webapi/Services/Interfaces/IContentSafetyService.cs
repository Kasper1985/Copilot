using WebApi.Models.Response;

namespace WebApi.Services.Interfaces;

/// <summary>
/// Defines a service that performs content safety analysis.
/// </summary>
public interface IContentSafetyService : IDisposable
{
    /// <summary>
    /// Invokes an API to perform harmful content analysis on an image.
    /// </summary>
    /// <param name="formFile">Image content file.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains the image analysis response.</returns>
    Task<ImageAnalysisResponse> ImageAnalysis(IFormFile formFile, CancellationToken cancellationToken);
    
    /// <summary>
    /// Parse the analysis result and return the violated categories.
    /// </summary>
    /// <param name="imageAnalysisResponse">The content analysis result.</param>
    /// <param name="threshold">Optional violation threshold.</param>
    /// <returns>The list of violated category names. Will return an empty list if there is no violation.</returns>
    List<string> ParseViolatedCategories(ImageAnalysisResponse imageAnalysisResponse, short threshold);
}
