# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Infrastructure:** Subscription-scoped Bicep (`infra/`), AVM modules, raw Bicep for DurableTask/AI Foundry, RBAC via managed identity, zero secrets
- **Created:** 2026-03-08

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-08 — Added `inbound-orders` Service Bus queue

**What changed:**
- `infra/main.bicep`: Added second queue entry `inbound-orders` (identical settings to `inbound-feedback`: `maxDeliveryCount: 10`, `deadLetteringOnMessageExpiration: true`, `lockDuration: 'PT1M'`). Added `ORDER_QUEUE_NAME: 'inbound-orders'` to the Function App `appSettings` alongside `SERVICEBUS_QUEUE_NAME`.
- `source/DurableAgent.AppHost/AppHost.cs`: Read `ORDER_QUEUE_NAME` from `Parameters` config, registered it with `sb.AddServiceBusQueue(orderQueueName)`, and wired it into the `func` project via `.WithEnvironment("ORDER_QUEUE_NAME", orderQueueName)`.
- `source/DurableAgent.AppHost/appsettings.json` and `appsettings.Development.json`: Added `"ORDER_QUEUE_NAME": "inbound-orders"` to the `Parameters` section.

**Key patterns:**
- New queues mirror the existing `inbound-feedback` Bicep block exactly — same delivery count, dead-lettering, and lock duration.
- AppHost reads queue names from `Parameters:*` config keys (not from AVM outputs), so both appsettings files must be updated alongside `AppHost.cs`.
- Bicep `queues:` array on the AVM Service Bus module accepts multiple objects — just append a new object, no other module changes needed.
