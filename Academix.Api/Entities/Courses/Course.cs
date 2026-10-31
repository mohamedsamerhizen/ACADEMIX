using System.Text.RegularExpressions;
using Academix.Api.Domain.Academic;

namespace Academix.Api.Domain.Courses;

public sealed class Course
{
    private static readonly Regex CodePattern = new("^[A-Z]{2,8}[0-9]{2,4}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private Course() { }
    public Guid Id { get; private set; }
    public Guid InstitutionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public long Version { get; private set; } = 1;

    public static Course Create(Guid id, string code, string title) => Create(id, AcademicIds.InitialInstitution, code, title);
    public static Course Create(Guid id, Guid institutionId, string code, string title)
    {
        if (id == Guid.Empty || institutionId == Guid.Empty) throw new ArgumentException("Identifiers must be nonempty.");
        if (string.IsNullOrWhiteSpace(code) || !CodePattern.IsMatch(code.Trim().ToUpperInvariant()))
            throw new ArgumentException("Course code must have 2-8 letters and 2-4 digits.", nameof(code));
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Course title must contain 1-200 characters.", nameof(title));
        return new Course { Id = id, InstitutionId = institutionId, Code = code.Trim().ToUpperInvariant(), Title = title.Trim(), IsActive = true };
    }
    public void Change(string title, bool active, long expectedVersion)
    {
        if (Version != expectedVersion) throw new InvalidOperationException("Stale course version.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) throw new ArgumentException("Invalid course title.");
        Title = title.Trim(); IsActive = active; Version++;
    }
}
