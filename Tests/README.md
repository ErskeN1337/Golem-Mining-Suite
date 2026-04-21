# Golem Mining Suite — Tests

## Running

From the repo root:

```bash
dotnet test "Golem Mining Suite.sln"
```

Or, to run a single test project: `dotnet test Tests/Golem.Mining.Suite.Tests/Golem.Mining.Suite.Tests.csproj`.

## Expected runtime

The full suite is pure-logic (no I/O, no WPF bootstrapping) and completes in under 10 seconds on a warm build — most of the time is framework startup. First cold run may take ~30 s while NuGet restores.

## Where to put new tests

- Unit tests for classes under `Golem Mining Suite/Services/` → `Tests/Golem.Mining.Suite.Tests/Services/<ServiceName>Tests.cs`
- Unit tests for `Golem Mining Suite/Models/` → `Tests/Golem.Mining.Suite.Tests/Models/`
- Unit tests for `Golem Mining Suite/ViewModels/` → `Tests/Golem.Mining.Suite.Tests/ViewModels/`
- Shared fakes / stubs (e.g., `StubHttpClientFactory`) → `Tests/Golem.Mining.Suite.Tests/Helpers/`

Prefer exercising fallback / in-memory code paths over mocking. If HTTP is unavoidable, use `StubHttpClientFactory` in `Helpers/` rather than adding Moq.
