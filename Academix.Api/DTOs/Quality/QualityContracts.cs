namespace Academix.Api.DTOs.Quality;

public sealed record QualityOutcomeInput(string Code, string Description, decimal TargetPassingRate);
public sealed record QualityCourseMapInput(Guid CourseId);
public sealed record QualityActionInput(Guid? OutcomeId, string Description, DateOnly? DueOn);
public sealed record QualityActionDecision(string Status, string? ResolutionNote);

/// <summary>Proxy indicator; denominator is published gradebook result rows, NOT unique students or direct outcome-assessment observations.</summary>
public sealed record QualityOutcomeEvidence(Guid OutcomeId, string Code, decimal TargetPassingRate,
    int MappedCourses, int PublishedGradeRecords, int PassingGradeRecords, decimal? PassingRate,
    string AssessmentStatus, string EvidenceKind);
public sealed record QualityReviewPreview(Guid ReviewId, Guid ProgramId, Guid TermId, string Status,
    IReadOnlyList<QualityOutcomeEvidence> Evidence, DateTime GeneratedAtUtc);
public sealed record QualityDashboard(Guid InstitutionId, int Outcomes, int CourseMappings, int Reviews,
    int PublishedReviews, int OpenImprovementActions, DateTime GeneratedAtUtc);


/// <summary>Direct graded rubric criterion observations; group submission-level, not per-student mastery.</summary>
public sealed record QualityDirectEvidence(Guid OutcomeId, string Code, int MappedCriteria,
    int GradedCriterionObservations, int ProficientObservations, decimal? ProficientRate,
    decimal TargetRate, string AssessmentStatus, string EvidenceKind);
public sealed record QualityCriterionMapInput(Guid SectionId, Guid CriterionId, decimal ProficiencyThresholdPercent);
public sealed record QualityPackInput(string Title);
public sealed record QualityPackItemInput(string EvidenceKind, Guid SourceId, string Label);
public sealed record QualityProgramTrend(Guid ProgramId, Guid TermId, Guid ReviewId, DateTime PublishedAtUtc,
    int ProxyMappedOutcomes, int ProxyMeetingTargets, int DirectMappedOutcomes, int DirectMeetingTargets,
    int DirectObservations, int OpenActions, int OverdueActions);
