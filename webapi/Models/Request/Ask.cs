using System.ComponentModel.DataAnnotations;
using WebApi.Attributes;

namespace WebApi.Models.Request;

public class Ask
{
    [Required, NotEmptyOrWhitespace]
    public string Input { get; set; } = string.Empty;
    public IEnumerable<KeyValuePair<string, string>> Variables { get; set; } = [];
}
