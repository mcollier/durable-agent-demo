# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Created:** 2026-03-08

## Core Context

**Summarized from early sessions (2026-03-08 to 2026-03-14):**

- **Order ingestion patterns**: HTTP `SubmitOrderTrigger` accepts `OrderRequest` sealed records with nullable int fields and camelCase JSON. Service Bus `InboundOrderTrigger` starts multi-agent orchestrations. `OrderRequest` validation uses `required` fields (OrderReference, FlavorId, name, address) with `Quantity: int?` ranging [1, 10].
- **Web + API naming**: Functions layer uses camelCase error messages (`"quantity must be between 1 and 10."`), Razor Pages layer uses PascalCase (`"Quantity must be between 1 and 10"`). Both consume same API — `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true`.
- **Razor Pages patterns**: Non-nullable properties (`int Quantity = 1`) with `[Required]` + `[Range]` prevent Required validation failures on initial GET while enforcing bounds on POST. `TempData` int round-trip uses `TempData["Key"] is int val ? val : fallback` pattern (direct cast fails).
- **FlavorId canonicalization**: `FlavorId` is now the 3-letter flavor code (`MNC`, `VNE`, etc.). `InventoryRepository.GetSkuForFlavorId()` bridges to SKU shape (`{FlavorId}-TUB`). `CheckInventoryTool` accepts canonical `FlavorId` values.
- **Email sending**: `SendCustomerEmailActivity` sends via `Azure Communication Services` using `EmailClient` (async). All emails route to settings address `RECIPIENT_EMAIL_ADDRESS` for dev/test safety. `FeedbackOrchestrator` passes `Subject` field to email input.
- **Service registration**: Both `IOrderQueueSender` (→ `ServiceBusOrderQueueSender`) and `IFeedbackQueueSender` follow identical singleton registration pattern in Program.cs.

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->




### 2026-03-14 — FlavorId migration to canonical 3-letter codes

- **Canonical contract**: `FlavorId` is now the 3-letter flavor code (`MNC`, `VNE`, etc.), while inventory keeps the derived SKU shape `{FlavorId}-TUB`. `FlavorRepository` is the canonical flavor catalog and now uses those 10 codes directly.
- **Single bridge point**: `InventoryRepository.GetSkuForFlavorId()` is the single code path that converts `FlavorId` → SKU, and `CheckInventoryTool` now accepts canonical `FlavorId` values instead of raw SKUs.
- **Catalog alignment**: Removed inventory-only `QCC-TUB` and `AAL-TUB` entries so inventory matches the real 10-flavor catalog instead of carrying orphan SKUs.
- **Prompt/schema cleanup**: Order-processing prompts now distinguish FlavorId from SKU; order intake canonical line items carry `flavorId`, while fulfillment output still reports inventory `sku` values.
- **Validation status**: Baseline `dotnet test` initially failed because `InboundOrderTriggerTests` lagged behind the trigger constructor. After updating that fixture and the FlavorId literals, the full suite passed: 176 tests green.
- **Romanoff handoff**: Updated the order/trigger tests and added `InventoryRepositoryTests` so Romanoff has coverage for the FlavorId→SKU bridge and the removed orphan inventory SKUs.

### 2026-03-14 — Real email sending via Azure Communication Services

- **`Subject` added to models**: Added `required string Subject { get; init; }` to both `EmailResult` (Core) and `SendCustomerEmailInput` (Functions), positioned after `RecipientEmail` and before `Body`.
- **`FeedbackOrchestrator` updated**: Mapped `emailResult.Subject` → `Subject` in the `SendCustomerEmailInput` object initializer passed to `CallActivityAsync`.
- **`SendCustomerEmailActivity` refactored**: `Run` (sync `string`) → `RunAsync` (async `Task<string>`). Resolves `EmailClient` and `IOptions<EmailSettings>` from `executionContext.InstanceServices.GetRequiredService<T>()`. Sends via `emailClient.SendAsync(WaitUntil.Completed, emailMessage, CancellationToken.None)`. Uses `input.RecipientEmail` (the actual customer) NOT `settings.RecipientEmailAddress`. Wraps send in try/catch: logs error + rethrows so Durable Functions retry policy applies.
- **Test updates** (compilation fix): `SendCustomerEmailActivityTests` converted from sync to async, `Run` → `RunAsync`. Faked `EmailClient` via `EmailModelFactory.EmailSendResult` + `A.Fake<EmailSendOperation>()` pattern (same as `InboundOrderTriggerTests`). Registered `EmailClient` and `IOptions<EmailSettings>` in fake service provider. `Subject` added to all test factory helpers in `SendCustomerEmailInputTests` and `EmailResultTests`.
- **Build**: 0 warnings, 0 errors after all 7 files changed.

### 2026-03-09 — SendCustomerEmailActivity now routes all emails to settings.RecipientEmailAddress

- **Routing change**: `SendCustomerEmailActivity` now uses `settings.RecipientEmailAddress` (from `RECEIPIENT_EMAIL_ADDRESS` env var via `IOptions<EmailSettings>`) as the actual send-to address for ALL emails, not `input.RecipientEmail`.
- **Traceability preserved**: `input.RecipientEmail` (the intended customer recipient) is still logged as part of the `LogInformation` call before the send, alongside `settings.RecipientEmailAddress` as the actual send-to address. This mirrors the behavior of `InboundOrderTrigger`.
- **Return string**: `$"Email sent to {settings.RecipientEmailAddress} for case {input.CaseId}"` — reflects the actual address used.
- **Test updates**: `WhenValidInput_ThenReturnsResultContainingEmailAndCaseId` now asserts on `"recipient@example.com"` (the `EmailSettings.RecipientEmailAddress` from faked `IOptions`). `WhenDifferentRecipient_ThenResultReflectsRecipientEmail` renamed to `WhenDifferentInputRecipient_ThenResultAlwaysUsesSettingsRecipientAddress` and updated to verify the settings address appears in the result and the input address does not. `WhenEmptyCaseId_ThenResultContainsEmptyCaseId` updated to assert on `"recipient@example.com"` instead of `"aidan@example.com"`.
- **Test count**: 176 total (125 Functions + 51 Core) — all passing.

### 2026-03-14 — README accuracy review (email/ACS additions)

- **Reviewed**: `README.md` against `SendCustomerEmailActivity.cs`, `EmailServiceExtensions.cs`, `EmailSettings.cs`, `EmailResult.cs`, `SendCustomerEmailInput.cs`, `AppHost.cs`, and `infra/main.bicep`.
- **Misspelled env var check**: README had no reference to `RECEIPIENT_EMAIL_ADDRESS` (old misspelling) — no fix needed there.
- **TODO/placeholder check**: README did not describe `SendCustomerEmailActivity` as a TODO or placeholder — correct.
- **Model shape check**: README does not document `EmailResult` or `SendCustomerEmailInput` properties — no update needed.
- **Changes made**:
  1. Added `Azure Communication Services` row to the Azure Resources table — ACS (email service + communication service) is fully deployed by `infra/main.bicep` and used by `SendCustomerEmailActivity` via `EmailClient`.
  2. Updated the Data Flow description for `SendCustomerEmailActivity` to say "via **Azure Communication Services** to the configured recipient address (`RECIPIENT_EMAIL_ADDRESS`)" — previously the description was vague and omitted ACS and the routing-to-settings address behavior.

### 2026-03-28 — README accuracy audit

- **Project structure missing dirs**: Added `Agents/`, `Extensions/`, `Workflows/` to the Functions project listing in README. These directories exist and contain agent config classes (`CustomerServiceAgentConfig`, `EmailAgentConfig`, `FulfillmentDecisionAgentConfig`, `OrderIntakeAgentConfig`, `CustomerMessagingAgentConfig`), extension helpers, and `OrderProcessingWorkflow`.
- **Services missing InventoryRepository**: Added `InventoryRepository.cs` to the Services listing (converts canonical FlavorId → inventory SKU).
- **Models incomplete**: Added `OrderRequest`, `OrderIntakeResult`, `FulfillmentDecisionResult`, `CustomerMessageResult`, `EmailSettings` to Functions/Models listing; added `OrderEmailResult`, `InventoryAnalysisResult`, `ContextBuilderOutput` to Core/Models listing.
- **Tool count wrong (5 → 7)**: README said CustomerServiceAgent had 5 tools — accurate for that agent, but project now has 7 total tools: `CheckInventoryTool` (used by FulfillmentDecisionAgent) and `RedactPiiTool` (general use) were omitted. Replaced the plain sentence with a full table mapping each tool to which agent uses it.
- **Order Queue Path stale**: README said `InboundOrderTrigger` "processes the order" — it actually runs a full multi-agent `order-processing-workflow` (OrderIntakeAgent → FulfillmentDecisionAgent → CustomerMessagingAgent) and sends an email via ACS. Updated the data flow description accordingly.
- **Key Decisions bullet updated**: Changed "agent has 5 tool functions" to "project exposes 7 tool functions across its agents".
- **Build**: `DurableAgent.Functions` project builds with 0 errors. Pre-existing `WorkerExtensions` MSBuild artifact issue and CodeCoverage file-lock error in test project are unrelated environment issues.
