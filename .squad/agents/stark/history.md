# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Created:** 2026-03-08

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-08 — Order endpoint stub (SubmitOrderTrigger)

- **Pattern**: New HTTP triggers follow the same pattern as `SubmitFeedbackTrigger`: sealed class with primary constructor DI for `ILogger<T>`, static `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true` and `JsonStringEnumConverter`, and shared `CreateErrorResponseAsync` helper.
- **Model**: `OrderRequest` is a `sealed record` with all-nullable `init`-only properties — no validation method needed for stubs; validation can be layered in later.
- **Web integration**: `Order.cshtml.cs` uses `JsonContent.Create(anonymousObj, options: JsonOptions)` with the existing camelCase `JsonOptions`; the Functions side accepts `PropertyNameCaseInsensitive = true`, so the naming policies are compatible.
- **Error handling in Razor Pages**: On HTTP failure or exception from the "func" named client, `LoadFlavorsAsync()` is called before re-rendering the page so the flavor dropdown is still populated.
- **appsettings.json**: New function paths are registered in the `AzureFunctions` config section (e.g., `"OrderPath": "api/orders"`), mirroring the existing `FeedbackPath`, `StoresPath`, `FlavorsPath` entries.
- **Decision archived**: Endpoint shape decision (`OrderRequest` model, `SubmitOrderTrigger` trigger, web integration approach) moved from decisions inbox → decisions.md. See `.squad/decisions.md` for full decision context and alternatives considered.


### 2026-03-08 — Order queue sender interface and inbound trigger (Wave 1)

- **Created**: `IOrderQueueSender.cs` (Services) and `InboundOrderTrigger.cs` (Triggers) — both mirror the `IFeedbackQueueSender` / `InboundFeedbackTrigger` pattern exactly.
- **Interface**: `IOrderQueueSender` lives in `DurableAgent.Functions.Services`, references `OrderRequest` from `DurableAgent.Functions.Models` (not Core), and exposes a single `Task SendAsync(OrderRequest order, CancellationToken cancellationToken = default)`.
- **Trigger**: `InboundOrderTrigger` is a no-op stub — deserializes the Service Bus body to `OrderRequest`, logs a warning on null, logs `"Received order {OrderReference}."` on success, and returns without starting any orchestration. No `[DurableClient]` binding.
- **Env var**: `ORDER_QUEUE_NAME` is the Service Bus queue name environment variable for orders (mirrors `FEEDBACK_QUEUE_NAME` for feedback).
- **JsonOptions**: Same static `JsonSerializerOptions` pattern — `PropertyNameCaseInsensitive = true` + `JsonStringEnumConverter`.
- **Build**: Solution builds cleanly (0 warnings, 0 errors) after both files were added.

### 2026-03-09 — Queue env var naming convention

- **Convention**: All Service Bus queue name env vars follow `{DOMAIN}_QUEUE_NAME` pattern. `FEEDBACK_QUEUE_NAME` = `inbound-feedback`, `ORDER_QUEUE_NAME` = `inbound-orders`. The old `SERVICEBUS_QUEUE_NAME` name was retired in a refactor.
- **Trigger binding**: Uses `%FEEDBACK_QUEUE_NAME%` syntax in `[ServiceBusTrigger]` attribute for env var substitution.
- **Queue sender reads**: `ServiceBusFeedbackQueueSender` reads `FEEDBACK_QUEUE_NAME`; `ServiceBusOrderQueueSender` reads `ORDER_QUEUE_NAME` — both at construction time via `Environment.GetEnvironmentVariable`.

### 2026-03-08 — OrderRequest validation

- **Pattern**: Added `Validate()` to `OrderRequest` following the identical pattern from `FeedbackSubmissionRequest.Validate()` — returns `IReadOnlyList<string>` with human-readable camelCase field names (e.g., `"orderReference is required."`).
- **Required fields**: `OrderReference`, `FlavorId`, `FirstName`, `LastName`, `StreetAddress`, `City`, `State`, `ZipCode`. Optional (no validation): `AddressLine2`, `Email`, `PhoneNumber`.
- **Error response shape**: Validation errors in `SubmitOrderTrigger` use `{ "errors": [...] }` (array), distinct from the existing `CreateErrorResponseAsync` helper which uses `{ "error": "..." }` (singular string). Inline response construction was used rather than abusing the helper.
- **Expected test breaks**: `WhenAllFieldsNull_ThenReturns200` and `WhenOrderReferenceProvided_ThenReturns200` now correctly return 400 — these are owned by Romanoff for update.

### 2026-03-08 — Wave 2: ServiceBusOrderQueueSender + SubmitOrderTrigger enqueue

- **Created**: `ServiceBusOrderQueueSender.cs` — mirrors `ServiceBusFeedbackQueueSender` exactly: takes `ServiceBusClient` via constructor, reads `ORDER_QUEUE_NAME` env var, builds `ServiceBusMessage` with `BinaryData.FromObjectAsJson(order)` and `MessageId = order.OrderReference`.
- **Updated**: `SubmitOrderTrigger` now injects `IOrderQueueSender` via primary constructor DI and calls `await orderQueueSender.SendAsync(order, cancellationToken)` after validation passes.
- **Error handling**: Mirrors `SubmitFeedbackTrigger` exactly — `ServiceBusException` with `IsTransient` → 503, non-transient `ServiceBusException` → 500, unexpected `Exception` → 500.
- **Tests updated**: All existing `SubmitOrderTriggerTests` constructor calls updated to pass `IOrderQueueSender` fake; 3 new ServiceBus error tests added (`WhenTransientServiceBusError_ThenReturns503`, `WhenNonTransientServiceBusError_ThenReturns500`, `WhenUnexpectedError_ThenReturns500`).
- **Build**: 0 warnings, 0 errors. 159 tests pass (108 Functions + 51 Core).

### 2026-03-08 — Wave 3: IOrderQueueSender registered in Program.cs

- **Registration**: `IOrderQueueSender` registered as singleton mapped to `ServiceBusOrderQueueSender` in `Program.cs`, immediately after the `IFeedbackQueueSender` registration — mirrors the same pattern.
- **No new usings needed**: Both types are already in `DurableAgent.Functions.Services`, which was already imported.
- **Build + tests**: 0 warnings, 0 errors. All 163 tests pass (51 Core + 112 Functions).
