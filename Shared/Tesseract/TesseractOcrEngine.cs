using Microsoft.KernelMemory.DataFormats;
using Tesseract;

namespace Shared.Tesseract;

/// <summary>
/// Wrapper for the TesseractEngine within the Tesseract OCR library.
/// </summary>
/// <param name="tesseractOptions"></param>
public class TesseractOcrEngine (TesseractOptions tesseractOptions) : IOcrEngine
{
    private readonly TesseractEngine _engine = new (tesseractOptions.FilePath, tesseractOptions.Language);
    
    public async Task<string> ExtractTextFromImageAsync(Stream imageContent, CancellationToken cancellationToken = default)
    {
        await using var imgStream = new MemoryStream();
        await imageContent.CopyToAsync(imgStream, cancellationToken);
        imgStream.Position = 0;

        using var img = Pix.LoadFromMemory(imgStream.ToArray());

        using var page = this._engine.Process(img);
        return page.GetText();
    }
}
