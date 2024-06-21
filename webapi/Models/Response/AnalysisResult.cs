using System.Text.Json.Serialization;

namespace WebApi.Models.Response;

public record AnalysisResult([property: JsonPropertyName("category")] string Category, [property: JsonPropertyName("severity")] short Severity);
