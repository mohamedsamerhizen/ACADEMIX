namespace Academix.Api.Domain.Learning;

public sealed class LearningUnit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public string Title { get; set; } = "";
    public int Position { get; set; }
    public bool IsPublished { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class LearningLesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid UnitId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int Position { get; set; }
    public bool IsPublished { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class LearningAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LearningAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public string Title { get; set; } = "";
    public string Instructions { get; set; } = "";
    public DateTime DueAtUtc { get; set; }
    public decimal MaxScore { get; set; } = 100;
    public bool IsPublished { get; set; }
    public long Version { get; set; } = 1;
}

public sealed class LearningSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public string Content { get; set; } = "";
    public Guid? AttachmentId { get; set; }
    public string Status { get; set; } = "Draft"; // Draft | Submitted | Graded
    public int Revision { get; set; } = 1;
    public DateTime? SubmittedAtUtc { get; set; }
    public decimal? Score { get; set; }
    public string? Feedback { get; set; }
    public Guid? GradedBy { get; set; }
    public DateTime? GradedAtUtc { get; set; }
}

public sealed class LessonCompletion
{
    public Guid LessonId { get; set; }
    public Guid StudentId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LearningFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid UploadedBy { get; set; }
    public bool IsStudentUpload { get; set; }
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
