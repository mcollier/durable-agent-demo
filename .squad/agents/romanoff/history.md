# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Test framework:** xUnit 2.9.3 + FakeItEasy 9.0.1. Test naming: When{Condition}_Then{Outcome}.
- **Created:** 2026-03-08

## Learnings

### 2026-03-08 — Wave 3: Quantity validation tests

- **`int?` null vs. out-of-range are one validation rule, not two.** Stark's implementation uses `Quantity is null or < 1 or > 10` in a single `if` — null and out-of-range share the same error message `"quantity must be between 1 and 10."`. Tests for null, zero, and eleven all assert the same error key (`quantity`), which is the right level of specificity.
- **Omitting an `int?` JSON property → `null`, not `0`.** In trigger tests, omitting `quantity` from the anonymous object sends a body without the field at all. `System.Text.Json` deserializes a missing `int?` property as `null`, which correctly triggers the null-check validation. This is the right approach for `WhenQuantityIsNull` trigger tests — do NOT send `quantity = null` explicitly (that also works, but omitting is more realistic for a missing field).
- **Parallel implementation was already done by the time tests ran.** Stark had already added `Quantity` to `OrderRequest` (with `is null or < 1 or > 10` validation) before tests were written. All 5 new quantity tests in both `SubmitOrderTriggerTests` and `OrderRequestTests` compiled and went **green immediately** — no red phase at all.
- **Boundary tests (1 and 10) belong in both layers.** Trigger boundary tests (`WhenQuantityIsOne_ThenReturns200`, `WhenQuantityIsTen_ThenReturns200`) confirm the full HTTP stack handles edges correctly. Model unit tests (`WhenQuantityIsOne_ThenValidateReturnsNoErrors`, `WhenQuantityIsTen_ThenValidateReturnsNoErrors`) confirm `Validate()` itself is correct. Both are worth having.
- **`WhenOptionalFieldsOmitted_ThenReturns200` needed `quantity = 1`.** Quantity is required, so the "minimal valid request" test must include a valid quantity. Boundary min (1) is the natural choice to keep the test name honest — it proves the minimum is accepted, not just any valid value.
- **`CreateValidRequest()` in model tests needs `Quantity = 5`** (mid-range) to keep the baseline valid after the new required field is added. Without it, all existing model tests that start from `CreateValidRequest()` would fail with a quantity error instead of testing the field they're targeting.
- **Total test count after wave 3: 122 (Functions.Tests) + 51 (Core.Tests) = 173.** All passing.

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
