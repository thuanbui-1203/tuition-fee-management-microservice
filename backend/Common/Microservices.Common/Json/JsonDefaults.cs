using System.Text.Json;

namespace Microservices.Common.Json;

/// <summary>Shared JSON serializer options (web defaults: camelCase, case-insensitive).</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
