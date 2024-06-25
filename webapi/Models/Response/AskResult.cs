namespace WebApi.Models.Response;

public class AskResult
{
    public string Value { get; set; } = string.Empty;
    public IEnumerable<KeyValuePair<string, object?>>? Variables { get; set; } = [];
}
