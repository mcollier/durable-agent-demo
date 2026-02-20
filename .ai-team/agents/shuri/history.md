# History

## Project Learnings (from import)

**Project:** FroyoFoundry.com — AI Agent Demo
**Stack:** .NET 10, C# 14, Razor Pages (DurableAgent.Web), Azure Functions Isolated Worker (DurableAgent.Functions), Azure Durable Task Scheduler, Azure OpenAI, Service Bus, Bicep IaC
**Owner:** Michael S. Collier
**Goal:** Showcase Microsoft Agent Framework + Azure Durable Functions for stateful AI agents

### Key Architecture

- `DurableAgent.Core/` — domain models (sealed records), zero cloud SDK deps
- `DurableAgent.Functions/` — Azure Functions isolated worker with Durable orchestrations + AI agents
- `DurableAgent.Web/` — Razor Pages frontend (if present)
- `infra/bicep/` — subscription-scoped Bicep deployment (4-phase, managed identity, zero secrets)
- AI data flow: HTTP POST /api/feedback → Service Bus → InboundFeedbackTrigger → FeedbackOrchestrator (CustomerServiceAgent + EmailAgent) → Activities

### Key Conventions

- Durable Functions: function-based static method pattern (NOT class-based TaskOrchestrator<>)
- DTOs: `sealed record` with `required` properties
- AI agent tools: static classes with `[FunctionInvocation]` attribute
- DurableAIAgent pattern: `context.GetAgent(name) → CreateSessionAsync() → RunAsync<TResult>()`
- No secrets — all auth via managed identity + RBAC
- Tests: xUnit + FakeItEasy, naming `When{Condition}_Then{Outcome}`
- Build: `cd source && dotnet test DurableAgent.slnx`

## Learnings

