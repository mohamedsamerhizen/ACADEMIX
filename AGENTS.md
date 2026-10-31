# ACADEMIX — Engineering Guidelines

- Keep .NET 10 and PostgreSQL as the source of truth.
- Prioritize correctness, security, and meaningful tests.
- Current two-project layout matches the requested portfolio structure. Organize features coherently inside Academix.Api.
- Avoid writing business logic inside HTTP endpoints.
- Preserve EF Core migrations and existing data.
- Never commit `.env`, passwords, tokens, or other secrets.
- Enforce institution ownership and resource authorization before adding private academic features.
- Do not claim build, tests, or Docker health without actual execution evidence.
- Do not implement future phases while fixing this foundation.
