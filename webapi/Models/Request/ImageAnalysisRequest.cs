using System.Text.Json.Serialization;
using WebApi.Models.Response;

namespace WebApi.Models.Request;

public record ImageAnalysisRequest([property: JsonPropertyName("image")] ImageContent Image);
