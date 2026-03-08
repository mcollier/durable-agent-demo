# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Test framework:** xUnit 2.9.3 + FakeItEasy 9.0.1. Test naming: When{Condition}_Then{Outcome}.
- **Created:** 2026-03-08

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-08 — SubmitOrderTrigger tests

- **Stub triggers need minimal fakes.** `SubmitOrderTrigger` has no queue sender — only `ILogger<SubmitOrderTrigger>` + `FunctionContext`. Don't over-fake.
- **All-nullable models still need a null-body test.** Even though every property on `OrderRequest` is `string?`, passing `"null"` as the body deserializes to `null` (not an empty record), so the null-body 400 path is real and testable.
- **`"{}"` vs `"null"` are distinct cases.** `"{}"` → valid `OrderRequest` with all nulls (200 OK). `"null"` → `null` reference (400). This distinction must be explicit in tests.
- **`--no-build` filter run finds nothing** if the assembly isn't current. When adding a new test file, always do a full build (`dotnet test` without `--no-build`) on the first run.
- **`FakeHttpResponseData` is declared inside `FakeHttpRequestData.cs`** — it lives in the same file, same namespace (`DurableAgent.Functions.Tests.TestHelpers`). No separate using needed.

### 2026-03-08 — OrderRequest validation tests

- **`"{}"` semantics changed with validation.** Previously `"{}"` → all-null record → 200 OK (no validation). Now `"{}"` → all-null record → 400 Bad Request (8 required fields missing). Tests that used `"{}"` as a happy path must be updated to expect 400.
- **Two error response shapes coexist.** JSON parse errors and null-body use `{ "error": "..." }` (legacy single-string shape). Validation failures use `{ "errors": [...] }` (array). Tests must assert the right key: "invalid JSON" / "empty or null" for malformed input, "errors" for field-level failures.
- **`record with { Prop = null }` is the cleanest way** to test individual missing fields in a `sealed record` — no constructor required, no builder needed. Start from a `CreateValidRequest()` baseline and null out one field per test.
- **Error message casing: camelCase in messages, PascalCase in field names.** `Validate()` returns messages like `"orderReference is required."`. Asserting with `StringComparison.OrdinalIgnoreCase` and the PascalCase property name (e.g., `"OrderReference"`) makes the test resilient to message wording changes while still being specific about which field is called out.
- **Direct `Validate()` unit tests complement trigger tests.** Trigger tests exercise the full HTTP stack (deserialization, 400 response, "errors" JSON body). `OrderRequestTests` test `Validate()` in isolation — faster, more targeted, no HTTP fake overhead. Both layers are worth having.
- **Parallel implementation is real.** Stark implemented `Validate()` and the trigger validation check while tests were being written. The tests compiled and went green immediately — writing spec-first tests against an in-progress implementation works fine in this repo.
