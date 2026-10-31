using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Services.AcademicOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

internal static class NoDatabaseAuthorizationProbes
{
    private sealed class SuppressConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken=default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }
    private sealed class ResourceQueryReached(string sql) : Exception(sql);
    private sealed class ObserveCommand : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken=default)
            => throw new ResourceQueryReached(command.CommandText);
    }
    internal static async Task Run()
    {
        var options=new DbContextOptionsBuilder<AcademixDbContext>().UseNpgsql("Host=localhost;Database=audit_no_connection;Username=audit;Password=not-used-placeholder")
            .AddInterceptors(new SuppressConnection(),new ObserveCommand()).Options;
        await using var db=new AcademixDbContext(options);
        // Deliberately absent authorization collaborators: a guarded operation must invoke
        // security before reaching any resource query. All database I/O is intercepted.
        var service=new CampusFacilitiesService(db,null!,null!);var principal=new ClaimsPrincipal();
        var institution=Guid.NewGuid();var room=Guid.NewGuid();var from=DateTime.UtcNow;var until=from.AddHours(1);
        foreach(var operation in new[]{"RoomOccupancy","InstitutionCalendar","AvailableAt"})
        {
            try
            {
                if(operation=="RoomOccupancy")await service.RoomOccupancy(principal,institution,room,from,until,default);
                else if(operation=="InstitutionCalendar")await service.InstitutionCalendar(principal,institution,from,until,default);
                else await service.AvailableAt(principal,institution,from,until,1,default);
                throw new InvalidOperationException("Expected intercepted database command.");
            }
            catch(ResourceQueryReached reached)
            {
                Console.WriteLine("AUDIT_RESULT "+JsonSerializer.Serialize(new{Probe="CampusReadMissingAuthority",Operation=operation,UnauthenticatedPrincipal=true,AuthorizationServiceAbsent=true,ResourceSqlReached=true,DatabaseExecuted=false,Sql=reached.Message}));
            }
        }
    }
}