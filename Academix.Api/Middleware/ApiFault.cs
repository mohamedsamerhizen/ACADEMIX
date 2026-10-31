namespace Academix.Api.Middleware;

public sealed class ApiFault(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public static ApiFault Bad(string message) => new(400, message);
    public static ApiFault Unauthorized() => new(401, "Authentication required or session invalid.");
    public static ApiFault Forbidden() => new(403, "You do not have permission for this resource.");
    public static ApiFault NotFound() => new(404, "The requested resource was not found.");
    public static ApiFault Conflict(string message) => new(409, message);
}
