using Academix.Api.Services.Curriculum;

namespace Academix.Tests.Unit;

public sealed class FinalAuditRecognitionRulesTests
{
    [Theory]
    [InlineData(null,false)]
    [InlineData(40,false)]
    [InlineData(80,true)]
    public void LocalRecognitionRequiresCurrentPassedOriginalAchievement(int? percentage,bool ready)
    {
        var source=Guid.NewGuid();var target=Guid.NewGuid();
        var data=new DegreeAuditData(Guid.NewGuid(),Guid.NewGuid(),2027,2,3,0,0,
            [new(target,"NEW","Replacement","Required",3,1)],[new(0,0,"F"),new(60,3,"B")],
            percentage is int grade?[new(source,Guid.NewGuid(),"OLD",grade,grade>=60)]:[],
            [new(target,"course:"+source)],[],new HashSet<Guid>(),[],[],[],2,0);
        var result=DegreeAuditEngine.Evaluate(data);
        Assert.Equal(ready,result.Audit.GraduationReady);Assert.Equal(ready?3:0,result.Audit.EarnedTotalCredits);
        Assert.Equal(ready?"Eligible":"NotEligible",result.Eligibility);
    }
    [Fact]
    public void QualifiedExternalRecognitionNeedsNoFabricatedLocalGradeAndCannotDoubleCountItsOrigin()
    {
        var first=Guid.NewGuid();var second=Guid.NewGuid();
        var data=new DegreeAuditData(Guid.NewGuid(),Guid.NewGuid(),2027,2,6,0,0,
            [new(first,"N1","First","Required",3,1),new(second,"N2","Second","Required",3,1)],
            [new(0,0,"F"),new(60,3,"B")],[],[new(first,"transfer:ORIGIN:A1"),new(second,"transfer:ORIGIN:A1")],[],new HashSet<Guid>(),[],[],[],2,0);
        var result=DegreeAuditEngine.Evaluate(data);
        Assert.Equal(3,result.Audit.EarnedTotalCredits);Assert.False(result.Audit.GraduationReady);Assert.Null(result.Audit.CumulativeGpa);
    }
}
