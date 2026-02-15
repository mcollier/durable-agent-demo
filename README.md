# Durable Agent Demo

> ⚠️ **WARNING: This is a sample/demo project intended for learning and experimentation. It is NOT production-ready and should not be deployed to production environments without thorough review and hardening.**

A serverless, event-driven application built on **Azure Functions (Flex Consumption)** with **Durable Task Scheduler** for orchestrating long-running workflows. Messages arrive via **Azure Service Bus**, trigger a durable orchestration, and are processed through an extensible activity pipeline — all secured with managed identity (zero secrets).

## Architecture

```
External Producer ──► Service Bus Queue ──► InboundFeedbackTrigger
                        (inbound-feedback)         │
                                                   ▼
                                          FeedbackOrchestrator
                                                   │
                                                   ▼
                                        ProcessFeedbackActivity
```

All inter-service communication uses **system-assigned managed identity** with RBAC — no connection strings, SAS tokens, or shared keys.

### Azure Resources

| Resource | SKU / Tier | Purpose |
|---|---|---|
| Azure Functions | Flex Consumption (FC1) | Serverless compute with per-execution billing |
| Durable Task Scheduler | Consumption | Managed backend for durable orchestrations |
| Azure Service Bus | Standard | Reliable async messaging (queues/topics) |
| Azure Storage Account | Standard LRS | Function runtime deployment artifacts |
| Application Insights | Workspace-based | Monitoring, diagnostics, and telemetry |
| Log Analytics Workspace | PerGB2018 | Backing store for Application Insights |

## Project Structure

```
infra/bicep/                            # Azure Bicep infrastructure-as-code
  main.bicep                            # Subscription-scoped deployment (4 phases)
  main.bicepparam                       # Parameters (baseName, region, tags)
  deploy.sh                             # CLI wrapper: deploy / what-if / delete
  modules/
    durable-task.bicep                  # Durable Task Scheduler + TaskHub
    rbac.bicep                          # RBAC role assignments

source/
  DurableAgent.slnx                     # .NET 10 XML solution file
  Directory.Build.props                 # Shared build properties
  global.json                           # SDK version pin (10.0.102)
  DurableAgent.Core/                    # Domain logic (zero cloud SDK deps)
    Models/FeedbackMessage.cs           # Sealed record DTO
  DurableAgent.Functions/               # Azure Functions isolated worker
    Triggers/InboundFeedbackTrigger.cs  # Service Bus trigger → starts orchestration
    Orchestrations/FeedbackOrchestrator.cs  # Durable orchestrator
    Activities/ProcessFeedbackActivity.cs   # Activity (extensibility point)
    host.json                           # Durable Task + Service Bus config
    Program.cs                          # App entry point
  DurableAgent.Core.Tests/              # xUnit tests for Core
  DurableAgent.Functions.Tests/         # xUnit + FakeItEasy tests for Functions

docs/
  plan-durableAgentServerless.md        # Application implementation plan
  bicep-planning-files/                 # Infrastructure plans
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (pinned via `global.json`)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) with Bicep
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-tools) v4+
- An Azure subscription

> **Tip:** This repo includes a dev container with all tooling pre-installed.

## Getting Started

### Build & Test

```bash
cd source
dotnet build DurableAgent.slnx
dotnet test DurableAgent.slnx
```

### Deploy Infrastructure

```bash
# Login to Azure
az login

# Preview changes (what-if)
./infra/bicep/deploy.sh -w

# Deploy
./infra/bicep/deploy.sh

# Override region
./infra/bicep/deploy.sh -l westus2

# Delete resources
./infra/bicep/deploy.sh -d
```

### Validate Bicep

```bash
az bicep build --file infra/bicep/main.bicep --stdout
```

## Data Flow

1. An external producer sends a JSON message to the `inbound-feedback` Service Bus queue.
2. **`InboundFeedbackTrigger`** receives the message, deserializes it to a `FeedbackMessage`, and schedules a new orchestration instance.
3. **`FeedbackOrchestrator`** runs the durable workflow, calling `ProcessFeedbackActivity`.
4. **`ProcessFeedbackActivity`** executes the business logic (currently a placeholder — extend here for sentiment analysis, storage, notifications, etc.).

### Sample Message

```json
{
  "id": "fb-001",
  "content": "Great product, love the new features!",
  "timestamp": "2026-02-14T12:00:00Z"
}
```

## Key Design Decisions

- **Zero secrets** — All authentication uses system-assigned managed identity + RBAC
- **Flex Consumption (FC1)** — Serverless scaling with per-execution billing and cold-start optimization
- **Durable Task Scheduler (Consumption)** — Fully managed orchestration backend (no Azure Storage tables/queues needed)
- **Function-based pattern** — Static classes with `[OrchestrationTrigger]` / `[ActivityTrigger]` (not class-based `TaskOrchestrator<>`)
- **Azure Verified Modules (AVM)** — Bicep uses AVM for standard resources; raw Bicep only where no AVM exists
- **Subscription-scoped deployment** — Bicep creates the resource group, then deploys resources into it across 4 phases

## Technology Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 / C# 14, isolated worker model |
| Orchestration | Azure Durable Functions + Durable Task Scheduler |
| Messaging | Azure Service Bus (Standard) |
| Infrastructure | Azure Bicep with Azure Verified Modules |
| Testing | xUnit 2.9.3, FakeItEasy 9.0.1 |
| CI/CD | GitHub Actions (planned) |

## Contributing

This is a demo/sample project. Feel free to fork and experiment.

## License

This project is provided as-is for demonstration purposes.
