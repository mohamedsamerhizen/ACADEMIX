using Academix.Api.DTOs.Courses;

namespace Academix.Api.Services.Courses;

public interface ICourseQueries
{
    Task<IReadOnlyList<CourseDto>> GetActiveAsync(CancellationToken cancellationToken);
}
