using System.Text.Json;
namespace Academix.Api.Services.Infrastructure;
public static class StoredJson
{
    public static bool Same<T>(string? stored,string? request)where T:class=>stored is null?request is null:
        request is not null&&EqualityComparer<T>.Default.Equals(JsonSerializer.Deserialize<T>(stored),JsonSerializer.Deserialize<T>(request));
}
