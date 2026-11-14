---
paths:
  - "src/Tests/**"
  - "web/src/**/*.test.ts"
  - "web/src/**/*.test.tsx"
---

# Testing

Checked by: `dotnet test src/App.slnx` and `pnpm --dir web test`, both in `ci / ci`.

| Project | What | Docker? |
|---|---|---|
| `src/Tests/Api.Tests` | Unit tests: code without the database (`PlatformConfiguration`, validation) | no |
| `src/Tests/Integration.Tests` | The API through `ApiFactory` (`WebApplicationFactory`) against Postgres in Testcontainers | **yes** |
| `web/src/**/*.test.tsx` | Vitest and Testing Library, next to the component, with `fetch` mocked | no |

- **Every behaviour change gets a test that fails without it.** A bug fix starts with the test that reproduces it. An endpoint gets an integration test for its success and each typed error it returns.
- **Names are sentences** that say the behaviour: `Uses_port_5432_when_the_url_has_none`, `it("shows the error when the API fails")`. One reason to fail per test.
- xUnit v3 with `Assert`; pass `TestContext.Current.CancellationToken` to async calls.
- Tests don't depend on each other's data or order: create what the test needs, with names unique to the test.
- No sleeps and no real clock; inject `TimeProvider` where time matters.
- If Docker isn't running, the integration tests fail at start: that is the environment, not the code. Run `Api.Tests` and say so in the PR.
