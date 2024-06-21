using System.ComponentModel.DataAnnotations;
using WebApi.Attributes;

namespace WebApi.Options;

/// <summary>
/// File system storage configuration
/// </summary>
public class FileSystemOptions
{
    /// <summary>
    /// Gets or inits the file path for persistent file system storage
    /// </summary>
    [Required, NotEmptyOrWhitespace]
    public string FilePath { get; set; } = string.Empty;
}
