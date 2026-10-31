# Changed C# files

## 2026-10-30 remediation source changes

The historical manifest list below does not describe a rebuilt installer payload. Current changes are confined to:

- `Academix.Api/Services/Infrastructure/AcademicBackgroundWorker.cs`: independent failure boundaries, cancellation and fair bounded scans.
- `Academix.Api/Services/Infrastructure/OutboxService.cs`: durable artifact reconciliation delivery.
- `Academix.Api/Services/Institutional/DocumentArtifactCleanup.cs` (new): independent pre-upload intent and lock-protected reference checking/removal.
- `Academix.Api/Services/Institutional/InstitutionalDocumentService.cs`: schedule cleanup, compatible institution lock, fresh-scope issuance retries.
- `Academix.Api/Services/Institutional/DocumentKeyService.cs`: explicit missing/unreadable-key conflicts.
- `Academix.Api/Extensions/ServiceCollectionExtensions.cs`: cleanup service registration.
- `Academix.Tests/Integration/Phase19CompletionWorkflowTests.cs`: partial class for reuse of real setup.
- `Academix.Tests/Integration/TestArtifactStore.cs`: remove-failure injection and object presence inspection.
- `Academix.Tests/Integration/Phase19RemediationTests.cs` (new): poison attempts, page starvation, Outbox independence, PDF rollback/retry/cleanup.
- `Academix.Tests/Integration/Phase19MigrationExecutionTests.cs` (new): clean and pre-Phase19 migration execution cases.
- `Academix.Tests/Unit/BackgroundWorkerCancellationTests.cs` (new): cancellation propagation.
- `Academix.Tests/Unit/Phase19CompletionTests.cs`: unavailable-keyring regression.

Reports, operations guidance, this inventory, regenerated full migration SQL and `evidence/remediation-*` execution artifacts were also updated. Historical migrations and the model snapshot are unchanged. No Git metadata is available for a repository diff.

## Historical package inventory

23 existing C# files modified; 24 C# files added. Exact baseline and installed SHA256 values are in manifest.json.

- `Academix.Api/Controllers/AcademicPoliciesController.cs`
- `Academix.Api/Controllers/EnrollmentsController.cs`
- `Academix.Api/Controllers/InstitutionalImportsController.cs`
- `Academix.Api/Data/AcademixDbContext.cs`
- `Academix.Api/Data/Configurations/ImportConfiguration.cs`
- `Academix.Api/Data/Configurations/RegistrarConfiguration.cs`
- `Academix.Api/Data/Migrations/20261031190000_Phase19RegistrarPoliciesAndImports.cs`
- `Academix.Api/Data/Migrations/20261031190000_Phase19RegistrarPoliciesAndImports.Designer.cs`
- `Academix.Api/Data/Migrations/20261031190100_Phase19RegistrarForeignKeys.cs`
- `Academix.Api/Data/Migrations/20261031190100_Phase19RegistrarForeignKeys.Designer.cs`
- `Academix.Api/Data/Migrations/AcademixDbContextModelSnapshot.cs`
- `Academix.Api/DTOs/Registrar/ImportContracts.cs`
- `Academix.Api/DTOs/Registrar/RegistrarContracts.cs`
- `Academix.Api/Entities/Registrar/ImportEntities.cs`
- `Academix.Api/Entities/Registrar/RegistrarEntities.cs`
- `Academix.Api/Extensions/ServiceCollectionExtensions.cs`
- `Academix.Api/Services/Academic/AcademicOperations.cs`
- `Academix.Api/Services/Academic/RegistrationCoordination.cs`
- `Academix.Api/Services/Academic/StructureService.cs`
- `Academix.Api/Services/AdvancedLearning/GroupLearningService.cs`
- `Academix.Api/Services/Assessment/GradebookService.cs`
- `Academix.Api/Services/Communications/CommunicationService.cs`
- `Academix.Api/Services/Curriculum/CurriculumService.cs`
- `Academix.Api/Services/Examinations/ExamAttemptPolicy.cs`
- `Academix.Api/Services/Examinations/ExamExpirationService.cs`
- `Academix.Api/Services/Examinations/ExamGradingService.cs`
- `Academix.Api/Services/Examinations/ExamTakingService.cs`
- `Academix.Api/Services/Governance/GovernanceService.cs`
- `Academix.Api/Services/Identity/AuthService.cs`
- `Academix.Api/Services/Identity/IdentityInputPolicy.cs`
- `Academix.Api/Services/Infrastructure/AcademicBackgroundWorker.cs`
- `Academix.Api/Services/Infrastructure/AcademicFreshness.cs`
- `Academix.Api/Services/Institutional/InstitutionalDocumentService.cs`
- `Academix.Api/Services/Lifecycle/LifecycleService.cs`
- `Academix.Api/Services/Records/RecordsService.cs`
- `Academix.Api/Services/Registrar/AcademicPolicyEngine.cs`
- `Academix.Api/Services/Registrar/AcademicPolicyService.cs`
- `Academix.Api/Services/Registrar/ImportValidation.cs`
- `Academix.Api/Services/Registrar/InstitutionalImportService.cs`
- `Academix.Tests/Integration/ApiFixture.cs`
- `Academix.Tests/Integration/Phase11GovernanceTests.cs`
- `Academix.Tests/Integration/Phase19RegistrarTests.cs`
- `Academix.Tests/Integration/Phase4ExamExecutionTests.cs`
- `Academix.Tests/Security/ControllerAuthorizationTests.cs`
- `Academix.Tests/Unit/ControllerRouteTests.cs`
- `Academix.Tests/Unit/Phase19MigrationTests.cs`
- `Academix.Tests/Unit/Phase19PolicyAndImportTests.cs`
- `docs/phase19/PHASE19_IMPLEMENTATION_REPORT.md`
- `docs/phase19/PHASE19_VERIFICATION_REPORT.md`
