using System.Text;
using Academix.Api.Domain.Institutional;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Registrar;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.DataProtection;

namespace Academix.Tests.Unit;
public sealed class Phase19CompletionTests
{
    [Fact]
    public void LostDataProtectionKeyringFailsClosedWithAnExplicitConflict()
    {
        using var db=new Academix.Api.Data.AcademixDbContextFactory().CreateDbContext([]);
        var original=new EphemeralDataProtectionProvider();
        var key=new DocumentVerificationKey { ProtectedSecret=original.CreateProtector("ACADEMIX.DocumentVerificationKeys.v2")
            .Protect(Convert.ToBase64String(new byte[32])) };
        var restarted=new DocumentKeyService(db,new EphemeralDataProtectionProvider());
        Assert.Equal(409,Assert.Throws<ApiFault>(()=>restarted.Sign(key,new IssuedAcademicDocument { SnapshotJson="{}" })).StatusCode);
    }
    [Fact]
    public void DetachedArtifactSignatureBindsThePdfDigestWithoutCircularProofHashing()
    {
        using var db=new Academix.Api.Data.AcademixDbContextFactory().CreateDbContext([]);
        var protection=new EphemeralDataProtectionProvider();
        var key=new DocumentVerificationKey{ProtectedSecret=protection.CreateProtector("ACADEMIX.DocumentVerificationKeys.v2").Protect(Convert.ToBase64String(new byte[32]))};
        var keys=new DocumentKeyService(db,protection);var doc=new IssuedAcademicDocument{Kind="Transcript",TemplateId=Guid.NewGuid(),SnapshotJson="{}",ArtifactDigest=DocumentIntegrity.Digest([1,2,3])};
        var publicProof=keys.Sign(key,doc);var contentSignature=keys.SignArtifact(key,doc);
        doc.ArtifactDigest=DocumentIntegrity.Digest([1,2,4]);
        Assert.Equal(publicProof,keys.Sign(key,doc));Assert.False(DocumentIntegrity.Equal(contentSignature,keys.SignArtifact(key,doc)));
    }
    [Fact]
    public void StoredJsonIdempotencySurvivesPostgreSqlFormatting()
    {
        var value=new RequestExecutionData(Guid.NewGuid(),null,null,null);
        var compact=System.Text.Json.JsonSerializer.Serialize(value);
        var formatted=System.Text.Json.JsonSerializer.Serialize(value,new System.Text.Json.JsonSerializerOptions{WriteIndented=true});
        Assert.True(StoredJson.Same<RequestExecutionData>(compact,formatted));
        Assert.False(StoredJson.Same<RequestExecutionData>(compact,System.Text.Json.JsonSerializer.Serialize(value with{SessionId=Guid.NewGuid()})));
        Assert.False(StoredJson.Same<RequestExecutionData>(compact,null));
    }
    [Theory]
    [InlineData("Invited")][InlineData("Submitted")][InlineData("Rejected")][InlineData("invalid")]
    public void ApplicantCannotReceiveStudentAccountBeforeAcceptance(string status)=>Assert.Throws<ApiFault>(()=>ApplicantRules.CanActivate(status));
    [Theory]
    [InlineData("Accepted")][InlineData("Converted")]
    public void AcceptedApplicantCanActivateOrRetry(string status)=>ApplicantRules.CanActivate(status);
    [Theory]
    [InlineData(null)][InlineData("")][InlineData("invalid")][InlineData("person <a@example.test>")]
    public void ApplicantEmailMustBeAnActualAddress(string? email)=>Assert.Throws<ApiFault>(()=>ApplicantRules.Email(email));
    [Fact]
    public void ApplicantProfilePreservesUnicodeAndNormalizesOnlyWhitespace()
    {
        Assert.Equal("طالب جامعي",ApplicantRules.Text(" طالب جامعي ",2,200));
        Assert.Equal("student@example.test",ApplicantRules.Email(" student@example.test "));
        Assert.Throws<ApiFault>(()=>ApplicantRules.Text(new string('x',201),2,200));
    }
    [Fact]
    public void DocumentProofBindsItsSnapshotAndVersionedContext()
    {
        var id=Guid.NewGuid();var key=Enumerable.Range(1,32).Select(x=>(byte)x).ToArray();
        var proof=DocumentIntegrity.Sign(key,id,"Transcript:revision1","{\"grade\":80}");
        Assert.True(DocumentIntegrity.Equal(proof,DocumentIntegrity.Sign(key,id,"Transcript:revision1","{\"grade\":80}")));
        Assert.False(DocumentIntegrity.Equal(proof,DocumentIntegrity.Sign(key,id,"Transcript:revision2","{\"grade\":80}")));
        Assert.False(DocumentIntegrity.Equal(proof,DocumentIntegrity.Sign(key,id,"Transcript:revision1","{\"grade\":81}")));
        Assert.False(DocumentIntegrity.Equal(proof,DocumentIntegrity.Sign(new byte[32],id,"Transcript:revision1","{\"grade\":80}")));
        Assert.False(DocumentIntegrity.Equal(proof,"bad"));
    }
    [Fact]
    public void DocumentProofSurvivesJsonbObjectOrderingWhitespaceAndNumericNormalization()
    {
        var key=new byte[32];var id=Guid.NewGuid();
        var original=DocumentIntegrity.Sign(key,id,"Transcript:revision1","{\"grade\":80.00,\"student\":{\"b\":true,\"a\":\"Student\"}}");
        var postgres=DocumentIntegrity.Sign(key,id,"Transcript:revision1","{\"student\": {\"a\": \"Student\", \"b\": true}, \"grade\": 80}");
        Assert.True(DocumentIntegrity.Equal(original,postgres));
        Assert.False(DocumentIntegrity.Equal(original,DocumentIntegrity.Sign(key,id,"Transcript:revision1","{\"grade\":81,\"student\":{\"a\":\"Student\",\"b\":true}}")));
    }
    [Fact]
    public void PdfRendererProducesARealMultiPageArtifactAndDetectsChanges()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Documents:PdfLicense"]="Evaluation"}).Build();
        var renderer=new DocumentPdfRenderer(config);
        var document=new IssuedAcademicDocument{Kind="Transcript",ReferenceNumber="TEST-REFERENCE",IssuedAtUtc=DateTime.UtcNow,ExpiresAtUtc=DateTime.UtcNow.AddDays(10),LegacyProof=new string('A',64),
            SnapshotJson=System.Text.Json.JsonSerializer.Serialize(new{StudentId=Guid.NewGuid(),Courses=Enumerable.Range(1,120).Select(x=>new{Course="C"+x,Percentage=80})})};
        var template=new DocumentTemplateRevision{Kind="Transcript",Revision=1,Heading="Test University",Footer="Academic Registrar",FontFamily="Lato"};
        var bytes=renderer.Render(document,template);
        Assert.Equal("%PDF-",Encoding.ASCII.GetString(bytes,0,5));Assert.True(bytes.Length>1000);
        var digest=DocumentIntegrity.Digest(bytes);bytes[^20]^=1;Assert.False(DocumentIntegrity.Equal(digest,DocumentIntegrity.Digest(bytes)));
    }
    [Fact]
    public void PdfIssuanceFailsClosedWhenLicenseNotConfigured()
    {
        var renderer=new DocumentPdfRenderer(new ConfigurationBuilder().Build());
        Assert.Equal(409,Assert.Throws<ApiFault>(()=>renderer.Render(new(),new())).StatusCode);
    }
    [Fact]
    public void TypedRequestsRejectMissingTargetsAndBroadExceptions()
    {
        AcademicRequestExecutionRules.Validate("General",null);
        Assert.Throws<ApiFault>(()=>AcademicRequestExecutionRules.Validate("AttendanceExcuse",null));
        AcademicRequestExecutionRules.Validate("AttendanceExcuse",new(Guid.NewGuid(),null,null,null));
        Assert.Throws<ApiFault>(()=>AcademicRequestExecutionRules.Validate("AttendanceExcuse",new(Guid.NewGuid(),Guid.NewGuid(),"CreditLoad",DateTime.UtcNow.AddDays(1))));
        Assert.Throws<ApiFault>(()=>AcademicRequestExecutionRules.Validate("EnrollmentException",new(null,Guid.NewGuid(),"Capacity",DateTime.UtcNow.AddDays(1))));
        Assert.Throws<ApiFault>(()=>AcademicRequestExecutionRules.Validate("EnrollmentException",new(null,Guid.NewGuid(),"Prerequisites",DateTime.UtcNow.AddDays(-1))));
        AcademicRequestExecutionRules.Validate("EnrollmentException",new(null,Guid.NewGuid(),"EnrollmentApproval",DateTime.UtcNow.AddDays(1)));
    }
    [Fact]
    public void TransitionPreventsRepeatedSourceOrTargetCreditRecognition()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();
        CurriculumTransitionRules.ValidateMappings(new[]{new CourseRecognition(a,b)});
        Assert.Throws<ApiFault>(()=>CurriculumTransitionRules.ValidateMappings(new[]{new CourseRecognition(a,b),new CourseRecognition(a,c)}));
        Assert.Throws<ApiFault>(()=>CurriculumTransitionRules.ValidateMappings(new[]{new CourseRecognition(a,c),new CourseRecognition(b,c)}));
        Assert.Throws<ApiFault>(()=>CurriculumTransitionRules.ValidateMappings(new[]{new CourseRecognition(a,a)}));
        var now=DateTime.UtcNow;CurriculumTransitionRules.CanApply("Approved",now,now);
        Assert.Throws<ApiFault>(()=>CurriculumTransitionRules.CanApply("Applied",now,now));
        Assert.Throws<ApiFault>(()=>CurriculumTransitionRules.CanApply("Approved",now.AddMinutes(1),now));
    }
    [Theory]
    [InlineData(1,5)][InlineData(2,10)][InlineData(4,40)][InlineData(1000,3600)]
    public void DurableDeliveryUsesBoundedBackoff(int attempt,int seconds)=>Assert.Equal(TimeSpan.FromSeconds(seconds),DeliveryPolicy.Backoff(attempt));
    [Fact]
    public void StaleDeliveryLeaseCannotAcknowledgeAnotherAttempt()
    {
        var token=Guid.NewGuid();Assert.True(DeliveryPolicy.LeaseOwned("Processing",token,token));
        Assert.False(DeliveryPolicy.LeaseOwned("Delivered",token,token));Assert.False(DeliveryPolicy.LeaseOwned("Processing",Guid.NewGuid(),token));
    }
    [Fact]
    public void CsvParsesQuotedValuesAndRejectsAmbiguousInput()
    {
        var rows=ImportCsvParser.Parse(new StringReader("id,email,program,term,plan,name\nS1,a@example.test,p,t,c,\"Last, First\"\n"));
        Assert.Equal("Last, First",Assert.Single(rows)["name"]);
        Assert.Throws<ApiFault>(()=>ImportCsvParser.Parse(new StringReader("id,id,p,t,c\na,b,c,d,e")));
        Assert.Throws<ApiFault>(()=>ImportCsvParser.Parse(new StringReader("id,email,p,t,c\na,b,c")));
    }
    [Fact]
    public void ImportsCanMapProfilesAndOnlyExplicitPublishedHistoryReferences()
    {
        var map=new ImportFieldMap("id","email","program","term","plan","name","history");
        var section=Guid.NewGuid();
        var row=new Dictionary<string,string?>{["id"]="S1",["email"]="s@example.test",["program"]=Guid.NewGuid().ToString(),["term"]=Guid.NewGuid().ToString(),["plan"]=Guid.NewGuid().ToString(),["name"]="Student Name",["history"]=section.ToString()};
        var parsed=Assert.Single(ImportValidation.Parse(new(Guid.NewGuid(),"SIS",map,new[]{row})));
        Assert.Equal("Student Name",parsed.FullName);Assert.Equal(section,parsed.PublishedSectionId);
        row["history"]="fabricated grade";Assert.Throws<ApiFault>(()=>ImportValidation.Parse(new(Guid.NewGuid(),"SIS",map,new[]{row})));
    }
}
