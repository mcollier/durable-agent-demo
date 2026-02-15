## Plan: Service Bus → Function (Flex Consumption) → Durable Orchestration

Create an Azure Functions (Flex Consumption / FC1) app that reads from a Service Bus queue (`inbound-feedback`) using managed identity (no secrets) and starts a Durable orchestration. Infra already provisions the Function App, Service Bus namespace, and RBAC; we add the missing queue + align runtime to .NET 10, then scaffold `source/` with the isolated worker function + orchestration + tests.

**Steps**
1. Infra: add queue definition to Service Bus namespace (depends on existing namespace module)
   - Update `infra/bicep/main.bicep`
   - In the existing `serviceBusNamespace` AVM module (`br/public:avm/res/service-bus/namespace:0.16.1`), add `queues` parameter with explicit production-ready properties:
     ```bicep
     queues: [
       {
         name: 'inbound-feedback'
         maxDeliveryCount: 10
         deadLetteringOnMessageExpiration: true
         lockDuration: 'PT1M'
       }
     ]
     ```
2. Infra: align Function runtime to .NET 10
   - Update `infra/bicep/main.bicepparam`
   - Change `functionRuntimeVersion` from `9.0` to `10.0`
3. Infra: add queue name as an app setting (avoid hardcoding in code)
   - Update `infra/bicep/main.bicep`
   - Add `SERVICEBUS_QUEUE_NAME: 'inbound-feedback'` inside the existing `configs[0].properties` block (alongside the other app settings, ~line 195)
4. App: scaffold solution/projects under `source/` (parallel with steps 1–3)
   - Create `source/global.json` to pin SDK version:
     ```json
     { "sdk": { "version": "10.0.102", "rollForward": "latestPatch" } }
     ```
   - Create `source/Directory.Build.props` for shared project settings (avoids repetition across 4 `.csproj` files):
     ```xml
     <Project>
       <PropertyGroup>
         <TargetFramework>net10.0</TargetFramework>
         <Nullable>enable</Nullable>
         <ImplicitUsings>enable</ImplicitUsings>
       </PropertyGroup>
     </Project>
     ```
   - Create `source/DurableAgent.sln`
   - Create projects:
     - `source/DurableAgent.Functions` (isolated worker, .NET 10)
     - `source/DurableAgent.Core` (no Azure SDK deps)
     - `source/DurableAgent.Functions.Tests` (xUnit)
     - `source/DurableAgent.Core.Tests` (xUnit)
5. App: implement Service Bus trigger that starts an orchestration
   - File: `source/DurableAgent.Functions/Triggers/InboundFeedbackTrigger.cs`
   - Use `[ServiceBusTrigger("%SERVICEBUS_QUEUE_NAME%", Connection = "ServiceBusConnection")]`
   - Deserialize payload to Core DTO and call Durable client to schedule orchestration
6. App: implement orchestration + activity (minimal)
   - Orchestrator file: `source/DurableAgent.Functions/Orchestrations/FeedbackOrchestrator.cs`
   - Activity file: `source/DurableAgent.Functions/Activities/ProcessFeedbackActivity.cs`
   - Activity can initially just log and return a simple result string (placeholder)
7. App: configuration files for Functions
   - Add `source/DurableAgent.Functions/host.json` — **must** include the Durable Task Scheduler extension config so the orchestration connects to the external scheduler (not the default Azure Storage backend):
     ```json
     {
       "version": "2.0",
       "logging": {
         "applicationInsights": { "samplingSettings": { "isEnabled": true, "excludedTypes": "Request" } }
       },
       "extensions": {
         "durableTask": {
           "hubName": "%TASKHUB_NAME%",
           "storageProvider": {
             "type": "DurableTaskScheduler",
             "connectionStringName": "DURABLE_TASK_SCHEDULER_CONNECTION_STRING"
           }
         },
         "serviceBus": {
           "prefetchCount": 0,
           "autoCompleteMessages": true
         }
       }
     }
     ```
   - Add `source/DurableAgent.Functions/local.settings.json` for local runs
8. Tests
   - Add trigger test that verifies “message → schedules orchestration” (mock Durable client)
   - Add Core test(s) only if meaningful (keep minimal)

**Relevant files**
- `infra/bicep/main.bicep` — add `queues` param to Service Bus namespace module; add `SERVICEBUS_QUEUE_NAME` to existing `configs[0].properties`
- `infra/bicep/main.bicepparam` — set `functionRuntimeVersion` to `10.0`
- `infra/bicep/modules/rbac.bicep` — no changes expected (already has Service Bus Data Receiver/Sender)
- `source/global.json` — pin .NET SDK version for reproducible builds
- `source/Directory.Build.props` — shared TFM, nullable, and implicit usings
- `source/DurableAgent.sln` — solution file
- `source/DurableAgent.Functions/` — Function App project, `Program.cs`, triggers, orchestrations, activities, `host.json`, `local.settings.json`
- `source/DurableAgent.Core/` — shared domain logic (models/DTOs)
- `source/DurableAgent.Functions.Tests/` — xUnit tests for Functions project
- `source/DurableAgent.Core.Tests/` — xUnit tests for Core project

**Verification**
- Bicep: `bicep build infra/bicep/main.bicep --stdout`
- .NET: `dotnet build source/DurableAgent.sln`
- Tests: `dotnet test source/DurableAgent.sln`
- Optional infra preview: `./infra/bicep/deploy.sh -w` (confirm queue creation + runtime update)

**Decisions**
- Queue name: `inbound-feedback`
- Trigger behavior: start a Durable orchestration per message
- Runtime: .NET 10 (requires updating Bicep param from 9.0 → 10.0)
- Auth model: managed identity + RBAC (already configured), using `ServiceBusConnection__fullyQualifiedNamespace` app setting and binding name `ServiceBusConnection`
- Out of scope: CI/CD workflows, DLQ handling policy, advanced retry/poison message strategy (can be added later)
