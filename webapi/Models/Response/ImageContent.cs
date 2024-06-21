using System.Text.Json.Serialization;

namespace WebApi.Models.Response;

public record ImageContent([property: JsonPropertyName("content")] string Content);
