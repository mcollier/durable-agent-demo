# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Test framework:** xUnit 2.9.3 + FakeItEasy 9.0.1. Test naming: When{Condition}_Then{Outcome}.
- **Created:** 2026-03-08

## Learnings

### 2026-03-09 — Quantity field validation tests (Wave 3)

- **5 trigger tests added**: `WhenQuantityIsNull`, `WhenQuantityIsZero`, `WhenQuantityIsNegative` (400 responses with "quantity" error key), `WhenQuantityIsOne`, `WhenQuantityIsTen` (200 OK for boundary min/max).
- **5 model tests added**: Matching unit tests on `Validate()` method with same boundary and out-of-range cases; `CreateValidRequest()` baseline updated to `Quantity = 5` (mid-range) to keep existing model tests valid after the new required field is added.
- **Baseline updates**: `CreateValidRequestBody()` in trigger tests now includes `"quantity": 1`; `WhenOptionalFieldsOmitted_ThenReturns200` test body updated to include valid quantity.
- **Null vs. out-of-range semantics**: Both null and out-of-range collapse into the same error message by design — there is no separate "quantity is required" error, only "quantity must be between 1 and 10."
- **Boundary tests prove inclusivity**: Tests for 1 and 10 explicitly confirm the range is inclusive (implementation uses `< 1` and `> 10`, not `<=`/`>=`).
- **Total: 163 tests passing** (108 Functions + 55 Core, up from prior wave). No red phase — Stark had already implemented the feature; tests compiled and passed immediately.

### 2026-03-09 — Decision merging & archiving

- **Inbox clearing**: 7 inbox files processed into main `decisions.md`, deduplicating entries from same author+date and consolidating test strategy decisions (Wave 2 + Wave 3 combined into one compound decision with subsections).
- **Inbox deleted**: All 7 files in `.squad/decisions/inbox/` removed after merge complete.
- **History consolidation**: No new learnings added to Romanoff history; existing learnings already capture the testing patterns used in Wave 3 (boundary values, null handling, baseline updates).


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

### 2026-03-08 — Wave 2: order queue sender mock and InboundOrderTrigger tests

- **Constructor parameter order matters for parallel work.** `SubmitOrderTrigger` uses `(ILogger, IOrderQueueSender)` — logger first, queue sender second. This differs from `SubmitFeedbackTrigger` where the queue sender is first. Always check the actual trigger constructor before writing test instantiation.
- **Stark lands changes concurrently — read the implementation first.** By the time Wave 2 tests ran, Stark had already updated `SubmitOrderTrigger.cs` with the queue sender, updated the test file with the new field/usings, and even added the Service Bus error tests. Spec-first tests are safest when written last in a race; otherwise check what's already in the file.
- **ILogger verification via FakeItEasy uses `call.Method.Name == "Log"` and `call.GetArgument<LogLevel>(0)`.** Extension methods (`LogInformation`, `LogWarning`) call through to `ILogger.Log<TState>()`. To verify them, intercept on `_logger` with `.Where(call => call.Method.Name == "Log" && call.GetArgument<LogLevel>(0) == LogLevel.Information)`.
- **`WhenValidRequest_ThenEnqueuesOrder` is distinct from `WhenValidOrderPayload_ThenReturns200`.** The 200 test only checks status code. The enqueue test uses `A.CallTo(() => sender.SendAsync(A<OrderRequest>.That.Matches(...), A<CancellationToken>._)).MustHaveHappenedOnceExactly()` to confirm the correct payload was sent. Both are needed.
- **No-op triggers log as their only observable side effect.** `InboundOrderTrigger` is a stub that does no orchestration. Its only testable behavior is `LogInformation` on success and `LogWarning` on null body. Tests for stubs should focus on log level assertions rather than downstream side effects.
- **`BinaryData.FromString("null")` triggers null-body path in Service Bus triggers.** `message.Body.ToObjectFromJson<T>()` returns `null` when the JSON literal `"null"` is passed — reliable way to exercise the `order is null` guard in `InboundOrderTrigger`.

### 2026-03-14 — Flavor ID Migration Audit

- **Three test suites have hardcoded legacy IDs:** `FlavorTests.cs`, `GetFlavorsTriggerTests.cs`, `SubmitOrderTriggerTests.cs`. All use `flv-001` or `flavor-001` and will fail red when repository changes to SKU codes.
- **HTTP payload format differs from repository format:** Tests use `"flavor-001"` but repository uses `"flv-001"`; test.http mixes `"VNE-TUB"` (SKU) with `"flavor-001"` (legacy order format). Ambiguity must be resolved before implementation.
- **Inventory-only codes need decision:** `QCC` and `AAL` exist in `InventoryRepository` but not in `FlavorRepository`. Tests don't reference them, so removal is low-risk; addition would require test updates to expect 12 flavors instead of 10.
- **No SKU bridge test exists:** Currently no test verifies that a flavor ID correctly converts to inventory SKU. E2E gap.
- **Invalid flavor ID handling not covered:** Tests don't verify what happens when an order references a non-existent flavor ID. Current validation only checks non-empty, not existence.
- **API contract breaking change documented:** Clients that POST `flavor-001` or expect `flv-001` in responses will break. Mitigation: communicate change; no backward compat layer needed for demo.
