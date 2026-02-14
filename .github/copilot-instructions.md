# Copilot Instructions — Durable Agent Demo

## Project Overview

This is an **Azure serverless event-driven project** using Azure Functions (Flex Consumption), Durable Task Scheduler, Service Bus, and supporting resources. Infrastructure is defined in Azure Bicep (`infra/`), application code in C#/.NET 10 (`source/`).

**Data flow:** Service Bus queue (`inbound-feedback`) → `InboundFeedbackTrigger` → `FeedbackOrchestrator` → `ProcessFeedbackActivity`

## Repository Structure

```
infra/bicep/durable-agent-serverless/   # Bicep IaC
  main.bicep                            # Subscription-scoped orchestrator (4 phases)
  main.bicepparam                       # Parameters (baseName, region, tags)
  deploy.sh                             # CLI wrapper: deploy / what-if / delete
  modules/
    durable-task.bicep                  # Raw: Scheduler + TaskHub (no AVM exists)
    rbac.bicep                          # RBAC for managed identity (4 roles)
source/
  DurableAgent.slnx                     # .NET 10 XML solution file (not .sln)
  Directory.Build.props                 # Shared: net10.0, nullable, implicit usings
  global.json                           # SDK pin: 10.0.102
  DurableAgent.Core/                    # Domain logic (zero cloud SDK deps)
    Models/FeedbackMessage.cs           # sealed record: FeedbackId, StoreId, OrderId, Customer, Channel, Rating, Comment
    Models/CustomerInfo.cs              # sealed record: PreferredName, FirstName, LastName, Email, PhoneNumber, PreferredContactMethod
    Models/ContactMethod.cs             # enum: Email, Phone (with JsonStringEnumConverter)
  DurableAgent.Functions/               # Azure Functions isolated worker project
    Program.cs                          # FunctionsApplication.CreateBuilder(args)
    Triggers/InboundFeedbackTrigger.cs  # ServiceBus trigger → starts orchestration
    Orchestrations/FeedbackOrchestrator.cs  # [OrchestrationTrigger] static class
    Activities/ProcessFeedbackActivity.cs   # [ActivityTrigger] static class
    host.json                           # Durable Task azureManaged + tracing V2
  DurableAgent.Core.Tests/              # xUnit tests for Core
  DurableAgent.Functions.Tests/         # xUnit + FakeItEasy tests for Functions
docs/
  bicep-planning-files/                 # Infra plans (YAML resource blocks)
    INFRA.durable-agent-serverless.md   # Canonical infra plan
  plan-durableAgentServerless.md        # App implementation plan
.github/agents/                         # Copilot agent definitions
  bicep-plan.agent.md                   # Plans infra → docs/bicep-planning-files/
  bicep-impl.agent.md                   # Implements Bicep from plans
  csharp-expert.agent.md                # .NET development guidance
```

## Architecture & Key Decisions

- **Subscription-scoped deployment** (`targetScope = 'subscription'` in main.bicep) — creates the resource group, then deploys into it.
- **4-phase deployment**: (1) Foundation (Log Analytics, Storage, Service Bus, Durable Task), (2) Monitoring (App Insights), (3) Compute (FC1 plan + Function App), (4) RBAC.
- **Azure Verified Modules (AVM)** via `br/public:avm/res/...` for standard resources. Raw Bicep only for `Microsoft.DurableTask/schedulers@2025-11-01` (no AVM exists).
- **Zero secrets**: All auth uses system-assigned managed identity + RBAC. No connection strings, SAS tokens, or shared keys.
- **Flex Consumption (FC1)**: Function App uses `functionAppConfig` with blob-based deployment storage via `SystemAssignedIdentity`.
- Resource names are deterministic: `{prefix}-{baseName}-{uniqueString}` pattern using `resourceToken`.

## Durable Functions Pattern

Uses the **function-based (static method)** pattern for the isolated worker model — NOT the class-based `[DurableTask]` / `TaskOrchestrator<>` pattern:

```csharp
// Orchestrator — static class + [OrchestrationTrigger]
[Function(nameof(FeedbackOrchestrator))]
public static async Task<string> RunAsync(
    [OrchestrationTrigger] TaskOrchestrationContext context, FeedbackMessage input)

// Activity — static class + [ActivityTrigger]
[Function(nameof(ProcessFeedbackActivity))]
public static string Run([ActivityTrigger] FeedbackMessage input, FunctionContext ctx)

// Trigger — instance class with [DurableClient] binding
[Function(nameof(InboundFeedbackTrigger))]
public async Task RunAsync(
    [ServiceBusTrigger(...)] ServiceBusReceivedMessage message,
    [DurableClient] DurableTaskClient durableClient, CancellationToken cancellationToken)
```

- Trigger classes use **primary constructor DI** (`sealed class Foo(ILogger<Foo> logger)`).
- Orchestrators/activities are **static classes** — no DI, use `context.CreateReplaySafeLogger()` or `FunctionContext.GetLogger()`.
- Durable Task storage provider is `"azureManaged"` in host.json with `connectionStringName: "DURABLE_TASK_SCHEDULER_CONNECTION_STRING"`.

## C#/.NET Conventions

- **.NET 10 / C# 14**, `dotnet-isolated` worker model. Solution uses `.slnx` XML format.
- `Directory.Build.props` centralizes TFM, nullable, and implicit usings for all 4 projects.
- **Namespace convention**: `DurableAgent.{Project}.{Folder}` — matches directory structure.
- **Functions project** depends on Core; Core has zero cloud SDK references.
- **DTOs**: `sealed record` with `required` properties (e.g., `FeedbackMessage`).
- **Guard clauses**: `ArgumentNullException.ThrowIfNull(param)` at method entry.
- **Entry point**: `FunctionsApplication.CreateBuilder(args)` in Program.cs (not `HostApplicationBuilder`).

## Testing

- **Framework**: xUnit 2.9.3 with global `<Using Include="Xunit" />`.
- **Mocking**: FakeItEasy 9.0.1 — use `A.Fake<T>()`, `A.CallTo(...)`, `Fake.GetCalls(...)`.
- **Naming**: `When{Condition}_Then{Outcome}` (e.g., `WhenValidMessageReceived_ThenSchedulesOrchestration`).
- **Test message creation**: Use `ServiceBusModelFactory.ServiceBusReceivedMessage(...)` for trigger tests.
- **DurableTaskClient verification**: Use `Fake.GetCalls()` + `call.GetArgument<T>(index)` for assertion (NSubstitute-style `Received()` matchers don't work well with `DurableTaskClient`'s `object?` parameters).
- Run tests: `cd source && dotnet test DurableAgent.slnx`

## Bicep Conventions

- Use AVM modules from `br/public:avm/res/...` whenever available. Check [AVM index](https://aka.ms/avm/index).
- Raw Bicep (explicit API versions) in `modules/` for resources without AVM.
- RBAC in `modules/rbac.bicep`, scoped to individual resources via `existing` references.
- `guid(resourceName, roleId, principalName)` for deterministic role assignment names.
- Parameters use `@description`, `@allowed`, `@minLength`/`@maxLength` decorators.
- Section comments: `// ═══` for major phases, `// ───` for resource blocks.

## Infrastructure Deployment

```bash
./infra/bicep/durable-agent-serverless/deploy.sh      # Deploy
./infra/bicep/durable-agent-serverless/deploy.sh -w    # What-if (preview)
./infra/bicep/durable-agent-serverless/deploy.sh -d    # Delete
./infra/bicep/durable-agent-serverless/deploy.sh -l westus2  # Override location
```

Validate Bicep: `az bicep build --file infra/bicep/durable-agent-serverless/main.bicep --stdout`

## Planning Workflow

Infrastructure changes follow a **plan-then-implement** pattern:
1. The `@bicep-plan` agent writes a plan to `docs/bicep-planning-files/INFRA.{goal}.md` with YAML resource blocks and phased task tables.
2. The `@bicep-impl` agent implements from the plan, validates with `bicep build`/`bicep lint`/`bicep format`.
3. Plans are the source of truth for AVM module versions, API versions, dependencies, and RBAC role IDs.

## CI/CD (GitHub Actions)

- **Not yet implemented.** Workflows will live in `.github/workflows/`.
- Expected: Bicep validate/what-if on PR, deploy on merge to `main`, .NET build/test on PR.
- Use `azure/login` with OIDC federated credentials (no stored secrets).

## Dev Environment

- Dev container: .NET 10 SDK, Azure CLI (with Bicep), Azure Functions Core Tools, Node LTS, GitHub CLI.
- MCP servers in `.vscode/mcp.json`: Context7 (docs lookup) and Azure MCP.
- Commits use **Conventional Commits** format (`feat`, `fix`, `docs`, `ci`, etc.) — see `.github/skills/git-commit/SKILL.md`.
- `local.settings.json` and Azurite files are gitignored — never commit these.
