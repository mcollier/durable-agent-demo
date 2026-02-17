# Copilot Instructions — Durable Agent Demo

## Project Overview

A serverless, event-driven application demonstrating **[Durable Agents](https://learn.microsoft.com/en-us/agent-framework/integrations/azure-functions?pivots=programming-language-csharp)** — the integration of the **Microsoft Agent Framework** with **Azure Durable Functions** to create stateful AI agents that persist conversation state, survive failures, and orchestrate multi-agent workflows with reliable execution guarantees.

The project uses **Azure Functions (Flex Consumption)** with the **Durable Task Scheduler** to host an Azure OpenAI-backed AI agent that analyzes customer feedback for **Froyo Foundry**, a fictional frozen yogurt chain. Customer feedback arrives via **Azure Service Bus** or an **HTTP endpoint**, is processed by a `CustomerServiceAgent` with tool-calling capabilities, and flows through an extensible activity pipeline — secured entirely with managed identity (zero secrets).

Infrastructure is defined in Azure Bicep (`infra/`), application code in C#/.NET 10 (`source/`).

**Data flow:** HTTP POST /api/feedback → Service Bus queue (`inbound-feedback`) → `InboundFeedbackTrigger` → `FeedbackOrchestrator` (with `CustomerServiceAgent` AI + `EmailAgent`) → `SendCustomerEmailActivity` → `ProcessFeedbackActivity`

## Repository Structure

```
infra/bicep/                            # Azure Bicep infrastructure-as-code
  main.bicep                            # Subscription-scoped deployment (4 phases)
  main.bicepparam                       # Parameters (baseName, region, tags)
  deploy.sh                             # CLI wrapper: deploy / what-if / delete
  modules/
    ai-foundry.bicep                    # Azure AI Foundry (Cognitive Services)
    durable-task.bicep                  # Durable Task Scheduler + TaskHub
    rbac.bicep                          # RBAC role assignments

source/
  DurableAgent.slnx                     # .NET 10 XML solution file
  Directory.Build.props                 # Shared build properties
  global.json                           # SDK version pin (10.0.102)
  DurableAgent.Core/                    # Domain logic (zero cloud SDK deps)
    Models/
      ContactMethod.cs                  # Enum: Email, Phone
      CustomerInfo.cs                   # Sealed record: customer details
      FeedbackMessage.cs                # Sealed record: inbound feedback DTO
      FeedbackResult.cs                 # Sealed record: AI analysis result (with nested types)
      Flavor.cs                         # Sealed record: frozen yogurt flavor
      Store.cs                          # Sealed record: store details
  DurableAgent.Functions/               # Azure Functions isolated worker
    Program.cs                          # App entry point, AI agent + DI config
    host.json                           # Durable Task + Service Bus config
    Triggers/
      InboundFeedbackTrigger.cs         # Service Bus trigger → starts orchestration
      SubmitFeedbackTrigger.cs          # HTTP POST /api/feedback → enqueues to Service Bus
    Orchestrations/
      FeedbackOrchestrator.cs           # Durable orchestrator with AI agent
    Activities/
      ProcessFeedbackActivity.cs        # Processes feedback after AI analysis
      SendCustomerEmailActivity.cs      # Sends follow-up email to the customer
    Services/
      IFeedbackQueueSender.cs           # Queue sender abstraction
      ServiceBusFeedbackQueueSender.cs  # Service Bus implementation
    Models/
      FeedbackSubmissionRequest.cs      # HTTP request DTO with validation
      SendCustomerEmailInput.cs         # Customer email activity input
    Tools/                              # AI agent tool functions
      GenerateCouponCodeTool.cs         # Generates coupon codes
      GetCurrentUtcDateTimeTool.cs      # Returns current UTC timestamp
      GetStoreDetailsTool.cs            # Looks up store info by ID
      ListFlavorsTool.cs                # Lists available flavors
      OpenCustomerServiceCaseTool.cs    # Opens a customer service case
      RedactPiiTool.cs                  # Redacts PII from text
  DurableAgent.Core.Tests/              # xUnit tests for Core
  DurableAgent.Functions.Tests/         # xUnit + FakeItEasy tests for Functions

docs/
  plan-durableAgentServerless.md        # Application implementation plan
  bicep-planning-files/                 # Infrastructure plans

.github/
  agents/                               # Custom Copilot agents
    bicep-plan.agent.md                 # Plans infra → docs/bicep-planning-files/
    bicep-impl.agent.md                 # Implements Bicep from plans
    csharp-expert.agent.md              # .NET development guidance
  skills/git-commit/                    # Git commit skill for conventional commits
  workflows/dotnet-ci.yml               # CI: build and test on push/PR
  copilot-instructions.md               # This file — repository-wide Copilot context
```

## Architecture & Key Decisions

- **[Durable Agents](https://learn.microsoft.com/en-us/agent-framework/integrations/azure-functions?pivots=programming-language-csharp)** — Microsoft Agent Framework + Azure Durable Functions for stateful AI agents with automatic state persistence, failure recovery, and deterministic orchestrations
- **DurableAIAgent orchestration** — The orchestrator uses `context.GetAgent()` to get a `DurableAIAgent` wrapper that checkpoints agent calls within the durable orchestration framework
- **Structured AI output** — Azure OpenAI with `ChatResponseFormat.ForJsonSchema` produces typed `FeedbackResult` responses including sentiment, risk assessment, recommended actions, and optional coupons
- **AI tool calling** — The agent has 6 tool functions (store lookup, coupon generation, case management, PII redaction, etc.) that it invokes autonomously during analysis
- **Subscription-scoped deployment** (`targetScope = 'subscription'` in main.bicep) — creates the resource group, then deploys into it.
- **4-phase deployment**: (1) Foundation (Log Analytics, Storage, Service Bus, Durable Task), (2) Monitoring (App Insights), (3) Compute (FC1 plan + Function App), (4) RBAC.
- **Azure Verified Modules (AVM)** via `br/public:avm/res/...` for standard resources. Raw Bicep only for `Microsoft.DurableTask/schedulers@2025-11-01` (no AVM exists).
- **Zero secrets**: All auth uses system-assigned managed identity + RBAC. No connection strings, SAS tokens, or shared keys.
- **Flex Consumption (FC1)**: Function App uses `functionAppConfig` with blob-based deployment storage via `SystemAssignedIdentity`.
- Resource names are deterministic: `{prefix}-{baseName}-{uniqueString}` pattern using `resourceToken`.

## Azure Resources

| Resource | SKU / Tier | Purpose |
|---|---|---|
| Azure Functions | Flex Consumption (FC1) | Serverless compute with per-execution billing |
| Durable Task Scheduler | Consumption | Managed backend for durable orchestrations |
| Azure AI Foundry | Cognitive Services | Azure OpenAI endpoint for AI agent |
| Azure Service Bus | Standard | Reliable async messaging (queues/topics) |
| Azure Storage Account | Standard LRS | Function runtime deployment artifacts |
| Application Insights | Workspace-based | Monitoring, diagnostics, and telemetry |
| Log Analytics Workspace | PerGB2018 | Backing store for Application Insights |

## Durable Functions Pattern

Uses the **function-based (static method)** pattern for the isolated worker model — NOT the class-based `[DurableTask]` / `TaskOrchestrator<>` pattern:

```csharp
// Orchestrator — static class + [OrchestrationTrigger] with AI agent
[Function(nameof(FeedbackOrchestrator))]
public static async Task<string> RunAsync(
    [OrchestrationTrigger] TaskOrchestrationContext context, FeedbackMessage input)
{
    // Get DurableAIAgent wrapper that checkpoints agent calls
    var agent = context.GetAgent("CustomerServiceAgent");
    var session = await agent.CreateSessionAsync();
    var result = await session.RunAsync<FeedbackResult>(input);
    // ... call activities
}

// Activity — static class + [ActivityTrigger]
[Function(nameof(ProcessFeedbackActivity))]
public static string Run([ActivityTrigger] FeedbackMessage input, FunctionContext ctx)

// Trigger — instance class with [DurableClient] binding
[Function(nameof(InboundFeedbackTrigger))]
public async Task RunAsync(
    [ServiceBusTrigger(...)] ServiceBusReceivedMessage message,
    [DurableClient] DurableTaskClient durableClient, CancellationToken cancellationToken)

// AI Agent Tool — static class with [FunctionInvocation] method
public static class GenerateCouponCodeTool
{
    [FunctionInvocation("Generate a coupon code...")]
    public static async Task<string> RunAsync(...)
}
```

- Trigger classes use **primary constructor DI** (`sealed class Foo(ILogger<Foo> logger)`).
- Orchestrators/activities are **static classes** — no DI, use `context.CreateReplaySafeLogger()` or `FunctionContext.GetLogger()`.
- **AI agent tools** are static classes with `[FunctionInvocation]` attributes that define the tool description for the AI model.
- **DurableAIAgent** provides the wrapper for agent calls in orchestrations: `context.GetAgent(name)` → `CreateSessionAsync()` → `RunAsync<TResult>()`.
- Durable Task storage provider is `"azureManaged"` in host.json with `connectionStringName: "DURABLE_TASK_SCHEDULER_CONNECTION_STRING"`.

## C#/.NET Conventions

- **.NET 10 / C# 14**, `dotnet-isolated` worker model. Solution uses `.slnx` XML format.
- `Directory.Build.props` centralizes TFM, nullable, and implicit usings for all 4 projects.
- **Namespace convention**: `DurableAgent.{Project}.{Folder}` — matches directory structure.
- **Functions project** depends on Core; Core has zero cloud SDK references.
- **DTOs**: `sealed record` with `required` properties (e.g., `FeedbackMessage`, `FeedbackResult`, `CustomerInfo`, `Store`, `Flavor`).
- **Enums**: Use `JsonStringEnumConverter` for JSON serialization (e.g., `ContactMethod`).
- **AI Models**: `FeedbackResult` is a nested sealed record structure with `Sentiment`, `RiskAssessment`, `RecommendedAction`, and optional `CouponDetails`.
- **Guard clauses**: `ArgumentNullException.ThrowIfNull(param)` at method entry.
- **Entry point**: `FunctionsApplication.CreateBuilder(args)` in Program.cs (not `HostApplicationBuilder`).
- **AI agent registration**: Register agents in Program.cs with `builder.RegisterDurableAgent<TAgent>(name)` and configure tools with `[FunctionInvocation]` attributes.

## Testing

- **Framework**: xUnit 2.9.3 with global `<Using Include="Xunit" />`.
- **Mocking**: FakeItEasy 9.0.1 — use `A.Fake<T>()`, `A.CallTo(...)`, `Fake.GetCalls(...)`.
- **Naming**: `When{Condition}_Then{Outcome}` (e.g., `WhenValidMessageReceived_ThenSchedulesOrchestration`).
- **Test message creation**: Use `ServiceBusModelFactory.ServiceBusReceivedMessage(...)` for trigger tests.
- **DurableTaskClient verification**: Use `Fake.GetCalls()` + `call.GetArgument<T>(index)` for assertion (NSubstitute-style `Received()` matchers don't work well with `DurableTaskClient`'s `object?` parameters).
- Run tests: `cd source && dotnet test DurableAgent.slnx`

## Bicep Conventions

- Use AVM modules from `br/public:avm/res/...` whenever available. Check [AVM index](https://aka.ms/avm/index).
- Raw Bicep (explicit API versions) in `modules/` for resources without AVM (`durable-task.bicep`, `ai-foundry.bicep`).
- RBAC in `modules/rbac.bicep`, scoped to individual resources via `existing` references.
- `guid(resourceName, roleId, principalName)` for deterministic role assignment names.
- Parameters use `@description`, `@allowed`, `@minLength`/`@maxLength` decorators.
- Section comments: `// ═══` for major phases, `// ───` for resource blocks.

## Infrastructure Deployment

```bash
./infra/bicep/deploy.sh      # Deploy
./infra/bicep/deploy.sh -w    # What-if (preview)
./infra/bicep/deploy.sh -d    # Delete
./infra/bicep/deploy.sh -l westus2  # Override location
```

Validate Bicep: `az bicep build --file infra/bicep/main.bicep --stdout`

## Planning Workflow

Infrastructure changes follow a **plan-then-implement** pattern:
1. The `@bicep-plan` agent writes a plan to `docs/bicep-planning-files/INFRA.{goal}.md` with YAML resource blocks and phased task tables.
2. The `@bicep-impl` agent implements from the plan, validates with `bicep build`/`bicep lint`/`bicep format`.
3. Plans are the source of truth for AVM module versions, API versions, dependencies, and RBAC role IDs.

## CI/CD (GitHub Actions)

- **Workflow**: `.github/workflows/dotnet-ci.yml` — runs on push/PR to `main`.
- Performs: .NET restore, build, and test for all projects in the solution.
- Uses .NET 10 SDK.
- Future: Add Bicep validate/what-if on PR, deploy on merge to `main`.
- Use `azure/login` with OIDC federated credentials (no stored secrets) for deployment workflows.

## Dev Environment

- Dev container: .NET 10 SDK, Azure CLI (with Bicep), Azure Functions Core Tools, Node LTS, GitHub CLI.
- MCP servers in `.vscode/mcp.json`: Context7 (docs lookup) and Azure MCP.
- Commits use **Conventional Commits** format (`feat`, `fix`, `docs`, `ci`, etc.) — see `.github/skills/git-commit/SKILL.md`.
- `local.settings.json` and Azurite files are gitignored — never commit these.
