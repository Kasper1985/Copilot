using Microsoft.KernelMemory.Pipeline;

namespace WebApi.Services;

/// <summary>
/// Defines a service that performs content safety analysis on images.
/// </summary>
/// <param name="allowImageOcr">Flag indicating if image OCR is supported.</param>
public class DocumentTypeProvider(bool allowImageOcr = false)
{
    private readonly Dictionary<string, bool> _supportedTypes = new (StringComparer.OrdinalIgnoreCase)
    {
        { FileExtensions.MarkDown, false },
        { FileExtensions.MsWord, false },
        { FileExtensions.MsWordX, false },
        { FileExtensions.Pdf, false },
        { FileExtensions.PlainText, false },
        { FileExtensions.ImageBmp, allowImageOcr },
        { FileExtensions.ImageGif, allowImageOcr },
        { FileExtensions.ImagePng, allowImageOcr },
        { FileExtensions.ImageJpg, allowImageOcr },
        { FileExtensions.ImageJpeg, allowImageOcr },
        { FileExtensions.ImageTiff, allowImageOcr },
    };
    
    /// <summary>
    /// Returns true if the extension is supported for import.
    /// </summary>
    /// <param name="extension">The file extension.</param>
    /// <param name="isSafetyTarget">Is the document a target for content safety, if enabled?</param>
    public bool IsSupported(string extension, out bool isSafetyTarget) => _supportedTypes.TryGetValue(extension, out isSafetyTarget);
}
