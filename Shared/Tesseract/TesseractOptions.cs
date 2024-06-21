using System.ComponentModel.DataAnnotations;

namespace Shared.Tesseract;

/// <summary>
/// Configuration options for Tesseract OCR support.
/// </summary>
public class TesseractOptions
{
    public const string SectionName = "Tesseract";
    
    /// <summary>
    /// The file path where the Tesseract language file is stored (e.g. "./data")
    /// </summary>
    [Required] public string? FilePath { get; set; } = string.Empty;
    /// <summary>
    /// The language file prefix name (e.g. "eng")
    /// </summary>
    [Required] public string? Language { get; set; } = string.Empty;
}
