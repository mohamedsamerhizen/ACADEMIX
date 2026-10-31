using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Curriculum;

public sealed record CreditRecognition(Guid TargetCourseId,string Source);
public sealed record DegreeException(Guid TargetCourseId,Guid? SourceCourseId,string Kind,string Status);
public sealed record DegreePrerequisite(Guid CourseId,Guid RequiredCourseId,decimal MinimumPercentage);
public sealed record DegreeAuditData(Guid StudentId,Guid PlanId,int CatalogYear,int Revision,
    decimal MinimumCredits,decimal MinimumElectives,decimal MinimumGpa,
    IReadOnlyList<CurriculumCourseView> Courses,IReadOnlyList<CurriculumBandInput> Bands,
    IReadOnlyList<TranscriptAttempt> Attempts,IReadOnlyList<CreditRecognition> Recognitions,
    IReadOnlyList<DegreeException> Exceptions,IReadOnlySet<Guid> InProgress,
    IReadOnlyList<ElectiveGroupInput> Groups,IReadOnlyList<DegreePrerequisite> Prerequisites,
    IReadOnlyList<AuditRequirement> Conditions,int RepeatedFailureThreshold,decimal MinimumProgressCredits);

/// <summary>Pure evaluation of server-loaded evidence. No client totals or persisted calculated progress.</summary>
public static class DegreeAuditEngine
{
    public static void ValidateGroups(IReadOnlyList<ElectiveGroupInput> groups,IReadOnlyList<CurriculumCourseView> courses)
    {
        if(groups is null||groups.Count>50)throw ApiFault.Bad("At most 50 elective groups are supported.");
        if(groups.Any(x=>x is null||string.IsNullOrWhiteSpace(x.Name)))throw ApiFault.Bad("Elective group names are required.");
        if(groups.Select(x=>x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=groups.Count)
            throw ApiFault.Bad("Elective group names must be distinct.");
        var eligible=courses.Where(x=>x.RequirementKind=="Elective").ToDictionary(x=>x.CourseId);
        var used=new HashSet<Guid>();
        foreach(var group in groups)
        {
            if(string.IsNullOrWhiteSpace(group.Name)||group.Name.Trim().Length>120||group.CourseIds is null||group.CourseIds.Count==0||
                group.MinimumCredits<=0||group.MinimumCredits>500||decimal.Round(group.MinimumCredits,2)!=group.MinimumCredits)
                throw ApiFault.Bad("A named elective group requires courses and a positive credit requirement.");
            foreach(var id in group.CourseIds)
                if(!eligible.ContainsKey(id)||!used.Add(id))throw ApiFault.Bad("Each elective course can belong to one requirement group per curriculum; overlapping groups cannot double-count credit.");
            if(group.CourseIds.Sum(x=>eligible[x].CreditHours)<group.MinimumCredits)throw ApiFault.Bad("Elective group cannot satisfy its minimum credits.");
        }
    }

    public static DegreeAuditView Evaluate(DegreeAuditData data)
    {
        ValidateGroups(data.Groups,data.Courses);
        var best=CurriculumPolicy.BestAttempts(data.Attempts).ToDictionary(x=>x.CourseId);
        var completed=new HashSet<Guid>();var waived=new HashSet<Guid>();var consumed=new HashSet<string>(StringComparer.Ordinal);
        var evidence=new Dictionary<Guid,string>();
        // Reserve actual achievements first. Substitution never steals credit already
        // counted for its native curriculum course, including electives.
        foreach(var c in data.Courses.OrderBy(x=>x.CourseId))
            if(best.TryGetValue(c.CourseId,out var attempt)&&attempt.Passed)
            {completed.Add(c.CourseId);consumed.Add("course:"+c.CourseId);evidence[c.CourseId]="PublishedTranscript:"+attempt.TermId;}
        foreach(var c in data.Courses.OrderBy(x=>x.RequirementKind=="Required"?0:1).ThenBy(x=>x.CourseId))
        {
            if(completed.Contains(c.CourseId))continue;
            foreach(var r in data.Recognitions.Where(x=>x.TargetCourseId==c.CourseId).OrderBy(x=>x.Source,StringComparer.Ordinal))
            {
                // Local transition credit cannot outlive the qualifying achievement.
                // External transfer provenance is qualified by the shared loader.
                if(r.Source.StartsWith("course:",StringComparison.Ordinal)&&
                    (!Guid.TryParse(r.Source.AsSpan(7),out var sourceCourse)||!best.TryGetValue(sourceCourse,out var original)||!original.Passed))continue;
                if(consumed.Add(r.Source)){completed.Add(c.CourseId);evidence[c.CourseId]="Recognized:"+r.Source;break;}
            }
            if(completed.Contains(c.CourseId))continue;
            var exception=data.Exceptions.SingleOrDefault(x=>x.TargetCourseId==c.CourseId&&x.Status=="Approved");
            if(exception?.Kind=="Waiver")
            {waived.Add(c.CourseId);evidence[c.CourseId]="ApprovedWaiver: requirement satisfied; no earned credits or GPA fabricated.";}
            if(exception?.Kind=="Substitution"&&exception.SourceCourseId is Guid source&&best.TryGetValue(source,out var sourceGrade)&&sourceGrade.Passed&&consumed.Add("course:"+source))
            {completed.Add(c.CourseId);evidence[c.CourseId]="ApprovedSubstitution:"+source;}
        }
        var pending=data.InProgress.Where(x=>!completed.Contains(x)&&!waived.Contains(x)).ToHashSet();
        var results=data.Courses.Select(c=>
        {
            var missing=data.Prerequisites.Where(x=>x.CourseId==c.CourseId&&!completed.Contains(x.RequiredCourseId)&&!waived.Contains(x.RequiredCourseId)&&
                (!best.TryGetValue(x.RequiredCourseId,out var a)||!a.Passed||a.Percentage<x.MinimumPercentage)).Select(x=>x.RequiredCourseId).Distinct().Order().ToArray();
            // A passed prerequisite must also meet the configured minimum percentage.
            missing=missing.Concat(data.Prerequisites.Where(x=>x.CourseId==c.CourseId&&best.TryGetValue(x.RequiredCourseId,out var a)&&a.Passed&&a.Percentage<x.MinimumPercentage)
                .Select(x=>x.RequiredCourseId)).Distinct().Order().ToArray();
            return new CourseProgress(c.CourseId,c.CourseCode,c.RequirementKind,c.CreditHours,
                completed.Contains(c.CourseId)?"Satisfied":waived.Contains(c.CourseId)?"Waived":pending.Contains(c.CourseId)?"Pending":"Unsatisfied",
                evidence.GetValueOrDefault(c.CourseId)??(pending.Contains(c.CourseId)?"ActiveEnrollment":"No qualifying academic achievement"),
                data.Attempts.Count(x=>x.CourseId==c.CourseId&&!x.Passed),missing);
        }).OrderBy(x=>x.Code,StringComparer.Ordinal).ThenBy(x=>x.CourseId).ToArray();
        var earnedRequired=data.Courses.Where(x=>x.RequirementKind=="Required"&&completed.Contains(x.CourseId)).Sum(x=>x.CreditHours);
        var earnedElectives=data.Courses.Where(x=>x.RequirementKind=="Elective"&&completed.Contains(x.CourseId)).Sum(x=>x.CreditHours);
        var pendingCredits=data.Courses.Where(x=>pending.Contains(x.CourseId)).Sum(x=>x.CreditHours);
        var gpa=CurriculumPolicy.WeightedGpa(data.Courses.Where(x=>best.ContainsKey(x.CourseId))
            .Select(x=>(x.CreditHours,CurriculumPolicy.Band(best[x.CourseId].Percentage,data.Bands).GradePoints)));
        var requirements=new List<AuditRequirement>();
        void Credits(string code,decimal required,decimal earned,decimal inProgress,string explanation)
            =>requirements.Add(new(code,earned>=required?"Satisfied":earned+inProgress>=required?"Pending":"Unsatisfied",required,earned,inProgress,explanation));
        Credits("TotalCredits",data.MinimumCredits,earnedRequired+earnedElectives,pendingCredits,"Distinct curriculum achievements; waived requirements do not grant credit.");
        Credits("ElectiveCredits",data.MinimumElectives,earnedElectives,data.Courses.Where(x=>x.RequirementKind=="Elective"&&pending.Contains(x.CourseId)).Sum(x=>x.CreditHours),"Distinct elective credits across the assigned curriculum.");
        foreach(var c in results.Where(x=>x.Kind=="Required"))
            requirements.Add(new("Required:"+c.CourseId,c.Status=="Waived"?"Satisfied":c.Status,1,c.Status is "Satisfied" or "Waived"?1:0,c.Status=="Pending"?1:0,c.Evidence));
        foreach(var group in data.Groups.OrderBy(x=>x.Name,StringComparer.Ordinal))
            Credits("ElectiveGroup:"+group.Name,group.MinimumCredits,
                data.Courses.Where(x=>group.CourseIds.Contains(x.CourseId)&&completed.Contains(x.CourseId)).Sum(x=>x.CreditHours),
                data.Courses.Where(x=>group.CourseIds.Contains(x.CourseId)&&pending.Contains(x.CourseId)).Sum(x=>x.CreditHours),"Courses are allocated to exactly one configured elective group.");
        requirements.Add(new("MinimumGpa",data.MinimumGpa==0||gpa.HasValue&&gpa>=data.MinimumGpa?"Satisfied":"Unsatisfied",data.MinimumGpa,gpa??0,0,
            data.MinimumGpa==0?"No minimum GPA is configured for this curriculum.":gpa.HasValue?"Best-attempt GPA from published transcript grades; recognized credits do not invent grades.":"No authoritative graded GPA is available."));
        requirements.AddRange(data.Conditions);
        if(data.Exceptions.Any(x=>x.Status=="Submitted"))requirements.Add(new("ExceptionReview","Pending",1,0,1,"An academic exemption/substitution awaits an independent decision."));
        var blockers=requirements.Where(x=>x.Status!="Satisfied").Select(x=>x.Code+": "+x.Explanation).ToArray();
        var missingRequired=results.Count(x=>x.Kind=="Required"&&x.Status is not ("Satisfied" or "Waived"));
        var indicators=new List<ProgressIndicator>();
        foreach(var c in results)
        {
            if(c.FailedAttempts>=data.RepeatedFailureThreshold)indicators.Add(new("RepeatedFailure",c.CourseId,$"{c.FailedAttempts} published failed attempts."));
            if(c.Status is not ("Satisfied" or "Waived")&&c.MissingPrerequisites.Count>0)indicators.Add(new("MissingPrerequisites",c.CourseId,string.Join(",",c.MissingPrerequisites)));
            if(c.Kind=="Required"&&c.Status is not ("Satisfied" or "Waived"))indicators.Add(new("MandatoryCourseRemaining",c.CourseId,"Required by the assigned curriculum revision."));
        }
        foreach(var c in results.Where(x=>x.Status is not ("Satisfied" or "Waived")))
        {
            var dependents=data.Prerequisites.Count(x=>x.RequiredCourseId==c.CourseId&&results.Any(r=>r.CourseId==x.CourseId&&r.Status is not ("Satisfied" or "Waived")));
            if(dependents>0)indicators.Add(new("CompletionBottleneck",c.CourseId,$"Required prerequisite for {dependents} remaining curriculum courses."));
        }
        if(earnedRequired+earnedElectives<data.MinimumProgressCredits)indicators.Add(new("InsufficientProgressCredits",null,$"Earned credits below institution's configured progress threshold {data.MinimumProgressCredits}."));
        if(missingRequired>0)indicators.Add(new("PotentialGraduationDelay",null,$"{missingRequired} mandatory courses remain; no predicted graduation date is asserted."));
        var fractions=new List<decimal>{data.MinimumCredits==0?1:Math.Min(1,(earnedRequired+earnedElectives)/data.MinimumCredits)};
        var mandatory=results.Count(x=>x.Kind=="Required");if(mandatory>0)fractions.Add((mandatory-missingRequired)/(decimal)mandatory);
        fractions.AddRange(requirements.Where(x=>x.Code.StartsWith("Elective",StringComparison.Ordinal)&&x.Required>0).Select(x=>Math.Min(1,x.Completed/x.Required)));
        var legacyCourses=data.Courses.Select(c=>new CurriculumAuditCourse(c.CourseId,c.CourseCode,c.RequirementKind,c.CreditHours,
            completed.Contains(c.CourseId)||waived.Contains(c.CourseId),best.GetValueOrDefault(c.CourseId)?.Percentage,
                best.TryGetValue(c.CourseId,out var a)?CurriculumPolicy.Band(a.Percentage,data.Bands).GradePoints:null,a?.TermId))
            .OrderBy(x=>x.CourseCode,StringComparer.Ordinal).ThenBy(x=>x.CourseId).ToArray();
        var audit=new StudentCurriculumAudit(data.StudentId,data.PlanId,blockers.Length==0,earnedRequired,earnedElectives,earnedRequired+earnedElectives,
            data.MinimumCredits,data.MinimumElectives,gpa,data.MinimumGpa,missingRequired,legacyCourses,blockers);
        return new(audit,data.CatalogYear,data.Revision,pendingCredits,decimal.Round(100*fractions.Min(),2),
            requirements.Any(x=>x.Status=="Unsatisfied")?"NotEligible":blockers.Length>0?"PendingReview":"Eligible",requirements,results,indicators,DateTime.UtcNow);
    }
}
