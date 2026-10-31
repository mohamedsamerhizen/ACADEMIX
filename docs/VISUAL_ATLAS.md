# Visual atlas

[Back to README](../README.md)

ACADEMIX uses an original navy, teal, blue and warm-gold palette with opaque light diagram surfaces. Assets are self-contained SVGs with system-font fallbacks, descriptive titles and no external image/font requests. Matching PNGs provide raster fallbacks. The diagrams describe the implemented backend and the accepted release snapshot; they are not application screenshots.

## Institutional backend identity

![ACADEMIX institutional backend hero](assets/graphics/hero-banner.svg)

[PNG fallback](assets/graphics/hero-banner.png)

## Connected system overview

![Admissions, teaching, records and university operations around a shared academic model](assets/graphics/system-overview.svg)

[PNG fallback](assets/graphics/system-overview.png)

## Implemented business modules

![Twelve domain groups implemented inside Academix.Api](assets/graphics/business-modules.svg)

[PNG fallback](assets/graphics/business-modules.png)

## Modular monolith

![Consumers, authentication, controllers and scoped services connected to relational persistence, private storage and background work](assets/graphics/architecture.svg)

[PNG fallback](assets/graphics/architecture.png)

## Student academic lifecycle

![Nine numbered stages from admission to institutional credentials](assets/graphics/student-lifecycle.svg)

[PNG fallback](assets/graphics/student-lifecycle.png)

## Examination and grade reconciliation

![Assessment evidence, official grades, synchronized transcripts, reassessment and retained credential history](assets/graphics/grade-reconciliation.svg)

[PNG fallback](assets/graphics/grade-reconciliation.png)

## Student finance lifecycle

![Fees, assistance, payment records, allocations, balanced ledger, corrections and holds](assets/graphics/finance-lifecycle.svg)

[PNG fallback](assets/graphics/finance-lifecycle.png)

## Security and institution boundaries

![Identity, operation-level access and protected resources under explicit institution-aware authorization](assets/graphics/security-isolation.svg)

[PNG fallback](assets/graphics/security-isolation.png)

## Infrastructure and reliability

![Database Outbox and MinIO cleanup recovery across separate transaction boundaries](assets/graphics/infrastructure-reliability.svg)

[PNG fallback](assets/graphics/infrastructure-reliability.png)

## Accepted verification snapshot

![Dated 2026-10-31 release verification summary with 696 passes, 39 migrations and clean Release build](assets/graphics/verification-snapshot.svg)

[PNG fallback](assets/graphics/verification-snapshot.png) · [Unchanged release evidence](../verification/FINAL_RELEASE_CLOSURE.md)

## Technology badges

![Static badges describing the actual .NET, ASP.NET Core, PostgreSQL, EF Core, MinIO and Docker stack](assets/graphics/technology-badges.svg)

[PNG fallback](assets/graphics/technology-badges.png)

## Regeneration

Run from the repository root with Node.js and PowerShell. The renderer uses the installed Microsoft Edge headless engine and an isolated temporary profile; it changes no application source or configuration.

```powershell
node docs/tools/build-graphics.mjs
./docs/tools/render-graphics.ps1
```

The generator is a documentation utility. Static verification counts intentionally remain tied to the accepted release date; update them only against a newly executed verification report. Review actual renders when changing typography or labels. The [render manifest](assets/graphics/render-manifest.json) records dimensions and PNG sizes. The [presentation report](PORTFOLIO_PRESENTATION_REPORT.md) records final QA and screenshot limitations.
