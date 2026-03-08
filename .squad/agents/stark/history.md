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

