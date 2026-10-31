# Portfolio presentation report

Date: 2026-10-31 (Asia/Baghdad). Target: `phase24-final`.

## Decision

**PRESENTATION_COMPLETE_WITH_SCREENSHOT_LIMITATIONS**

The complete README, all ten required original graphics, technology badges, matching PNG fallbacks and genuine API captures are delivered. The screenshot limitation is the actual interface: this repository exposes Development OpenAPI JSON, with no Swagger UI or operational frontend to capture. The gallery truthfully shows two native JSON response excerpts.

## Delivered presentation

The README now covers identity and motivation, a verified overview, implemented capabilities, real captures, connected academic/finance/campus/service workflows, architecture, authorization, data integrity, reliability, accepted evidence, stack, source tree, Docker quick start, host development, tests, API discovery, documentation, limitations and interview topics. Author/license text reports only what this snapshot declares.

Original assets in [the visual atlas](VISUAL_ATLAS.md):

- `hero-banner`, `system-overview`, `business-modules`, `architecture`.
- `student-lifecycle`, `grade-reconciliation`, `finance-lifecycle`.
- `security-isolation`, `infrastructure-reliability`, `verification-snapshot`.
- Additional `technology-badges` strip.

All eleven have self-contained SVG and PNG versions under `docs/assets/graphics/`. The hero includes an original institutional line drawing; the diagrams share a navy/teal/light-neutral identity. PNGs were rasterized with installed Microsoft Edge in an isolated headless profile. The generator, renderer and validator are documentation utilities under `docs/tools/`.

Supporting files: [DEVELOPMENT.md](DEVELOPMENT.md), [API_GUIDE.md](API_GUIDE.md), [VISUAL_ATLAS.md](VISUAL_ATLAS.md), this report, a documentation-only loopback MinIO [Compose override](examples/compose.host-storage.yaml), asset manifests and validation evidence. The root Compose configuration was not modified.

## Screenshot provenance and limits

[OpenAPI overview](assets/screenshots/openapi-overview.jpg) and [academic workspace schema](assets/screenshots/academic-schema.jpg) are genuine browser viewport JPEGs from the unchanged current Release assembly. The live contract identified `Academix.Api | v1`, OpenAPI 3.1.1. Capture used loopback port 5089 and a new dedicated PostgreSQL 18 container/database; all 39 migrations applied and database readiness returned `Healthy`. Bootstrap and background work were disabled. No existing database or user data was accessed.

Images were inspected before publishing them to the documentation directory. Only schema text and loopback URLs are visible; no credentials, tokens, records, personal emails, filesystem paths or unrelated browser content appear. There are no synthetic UI images, edited response values or reused screenshots from another project. The second capture shows response fields, not a populated dashboard. There is no Swagger gallery because no Swagger UI is configured. [Session metadata](assets/screenshots/capture-runtime.json) records the live discovery context without secrets.

The dedicated API process and documentation PostgreSQL container were stopped after capture. The demo container/data were retained. Existing containers, volumes, images, databases and backups were untouched. No MinIO verification was rerun for documentation work.

## Accuracy and presentation QA

The unchanged [final release closure](../verification/FINAL_RELEASE_CLOSURE.md) supplies the dated accepted baseline: 696 passed, zero failed/skipped; 540 local + 155 PostgreSQL + one real MinIO orchestration; 39 migrations; clean warnings-as-errors Release build; EF model consistency. These are historical executed results, not newly claimed test executions or a live CI badge.

Source checks covered actual projects, dependency versions, role constants, pipeline/OpenAPI mapping, representative routes, Compose ports, startup migrations, bootstrap and configuration requirements. The README distinguishes HMAC proofs from certificate signatures, recorded finance from banking, and portfolio acceptance from production certification. It does not imply a frontend, universal tenant filter or exactly-once guarantee.

The automated [validation record](assets/presentation-validation.json) checks local links and Markdown anchors in the five delivered Markdown documents, all SVG/XML, PNG dimensions/decoding, genuine JPEG decoding, 436 source/project/config fingerprints and historical verification file fingerprints. The final second pass passed with zero broken references, zero source differences and zero historical evidence differences (436 source/config entries and 675 verification files checked). Actual diagram renders were visually inspected, including a full-size architecture check and a [contact sheet](assets/graphics/visual-review.png). A misleading architecture arrow was corrected before acceptance. Local PowerShell/Markdig README previews were rendered and visually inspected on [light](assets/previews/readme-light.png) and [dark](assets/previews/readme-dark.png) backgrounds. These are presentation QA previews, separate from genuine API captures; they do not certify GitHub-hosted rendering. The README uses GitHub-compatible Markdown, `picture`, tables and collapsible detail blocks with descriptive alt text and local paths; no remote image/font requests are needed for its critical visuals.

No services, controllers, entities, migrations, tests, project files or root runtime configuration were changed. Existing final acceptance reports remain byte-for-byte unchanged. The full test suite was not rerun for this documentation-only change. No push, upload or publication occurred.

## Design references and remaining limits

The public READMEs of [Relytica](https://github.com/mohamedsamerhizen/Relytica), [RIAYA](https://github.com/mohamedsamerhizen/RIAYA), [MATGER](https://github.com/mohamedsamerhizen/MATGER) and [DARAK](https://github.com/mohamedsamerhizen/DARAK) were reviewed for hierarchy, evidence placement, genuine captures and coherent diagram use. ACADEMIX's wording, graphics and academic identity are original.

GitHub-hosted rendering and external link availability were not certified by a publication run. The source snapshot has no declared root license or verified author metadata. Older phase design documents remain historical; the new index directs current acceptance claims to the final closure. The native JSON interface limits the screenshot presentation but does not leave required architecture graphics or essential setup documentation missing.

