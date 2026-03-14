# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Created:** 2026-03-08

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-03-09 — Quantity field: int? on API, non-nullable int on Razor Pages

- **`int? Quantity` on `OrderRequest`**: API model uses nullable int to distinguish "not provided" from 0 in JSON deserialization; validation rule `if (Quantity is null or < 1 or > 10)` produces error message `"quantity must be between 1 and 10."` (camelCase, matches API layer conventions).
- **Razor Pages non-nullable pattern**: `Order.cshtml.cs` declares `[BindProperty] int Quantity = 1` with `[Required]` and `[Range(1, 10)]` — the default value (1) prevents accidental Required validation failure on initial GET, while `[Range]` enforces bounds on POST via `ModelState.IsValid`.
- **TempData int round-trip**: Storing `int` in `TempData["Quantity"] = Quantity` and reading back requires `TempData["Quantity"] is int qty ? qty : 1` pattern; direct `as int` cast fails because TempData deserializes stored ints as `int` type, not boxed `object`.
- **HTML5 `<select>` validation**: Using `<select required>` with a blank default `<option value="">-- Select quantity --</option>` triggers browser-side HTML5 validation without JavaScript. Dropdown offers 10 natural-language options: "1 container", "2 containers", ..., "10 containers".
- **Validation error messaging**: Functions layer uses camelCase `"quantity must be between 1 and 10."` (API style); Razor Pages layer uses PascalCase via `[Range]` attribute message `"Quantity must be between 1 and 10"` (UI style). Both enforce the same 1–10 range.
- **Confirmation display**: Updated `OrderConfirmation.cshtml.cs` to read Quantity from TempData with fallback, then display as `"Quantity: @Model.Quantity × 1 Gallon 🪣"` in the order summary (replaces hardcoded "Size: 1 Gallon 🪣" row).
- **5 files, zero conflicts**: Edited 2 Functions files (`OrderRequest.cs`, `SubmitOrderTrigger.cs` references) and 3 Web files (`Order.cshtml.cs`, `Order.cshtml`, `OrderConfirmation.cshtml.cs`, `OrderConfirmation.cshtml`) in single response — no merge conflicts. Build: 0 warnings, 0 errors. Final test count: 163 passing (108 Functions + 55 Core).

### 2026-03-09 — Quantity field end-to-end (Order flow)

- **`OrderRequest` nullable int pattern**: Numeric fields on `OrderRequest` use `int?` (nullable) to distinguish "not provided" from 0; validation uses `Quantity is null or < 1 or > 10` pattern, consistent with `string.IsNullOrWhiteSpace` approach for strings.
- **Razor Pages `[Range]` + default**: Bound integer properties should carry a sensible default (e.g., `= 1`) so the model doesn't fail `[Required]` on initial GET — `[Range(1, 10)]` is still enforced on POST via `ModelState.IsValid`.
- **TempData int round-trip**: Storing `int` in TempData and reading it back requires `TempData["Key"] is int val ? val : fallback` — a direct cast via `as int` fails because TempData deserializes ints as `int` not `object`. 
- **`select` default option**: Using `<option value="">-- Select quantity --</option>` with `required` on the `<select>` triggers HTML5 client-side validation without needing JS.
- **Parallel edits safe**: All 5 files (2 Functions, 3 Web) were edited in the same response with no conflicts; build + 163 tests pass on first run.

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

### 2026-03-09 — Quantity field end-to-end (Wave 3)

- **`OrderRequest` + `Quantity` property**: Added `int? Quantity`, validation rule `is null or < 1 or > 10` → `"quantity must be between 1 and 10."`. Placed after FlavorId in validation order.
- **Razor Pages two-layer pattern**: `Order.cshtml.cs` uses non-nullable `int Quantity = 1` with `[Required]` + `[Range(1, 10)]`; this pattern prevents Required validation failure on initial GET (default = 1) while still enforcing the range on POST. Web layer uses PascalCase error messages (`"Quantity must be between 1 and 10"`), API layer uses camelCase (`"quantity must be between 1 and 10."`).
- **TempData int round-trip**: `Order.cshtml` stores `TempData["Quantity"] = Quantity`, then `OrderConfirmation.cshtml.cs` reads it with `TempData["Quantity"] is int qty ? qty : 1` (direct cast fails; the `is` pattern is required).
- **HTML5 select validation**: `<select required>` with a blank `<option value="">` default option triggers browser-side validation without JavaScript. 10 options (1–10) with natural text labels ("1 container", "2 containers", ..., "10 containers").
- **Confirmation display**: Updated hardcoded `"Size: 1 Gallon 🪣"` row to dynamic `"Quantity: @Model.Quantity × 1 Gallon 🪣"`.
- **All 5 files edited in same response**: No conflicts, build 0 warnings/errors, 163 tests passing immediately.

### 2026-03-09 — Decision merging into decisions.md

- **Inbox cleared**: 7 inbox decision files merged into main `decisions.md`, deduplicated by author+date, organized by logical area (queue naming, OrderRequest validation, Quantity field, Service Bus queue creation, Queue Sender pattern, test coverage strategy).
- **No duplication**: "Order Queue Sender Interface", "ServiceBusOrderQueueSender", and "Quantity Field Validation" were each written once and appear once in the merged document.
- **History consolidation**: Organized test decisions (Wave 2 + Wave 3) into a single compound decision with separate subsections per wave to keep test strategy coherent.


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

### 2026-03-14 — Flavor IDs vs inventory SKU prefixes

- **Two ID systems exist today**: `FlavorRepository` is the source of truth for `/api/flavors` and AI flavor listings, but it uses `flv-001`…`flv-010` while order-focused tests use `flavor-001` and `InventoryRepository` uses SKU prefixes like `VNE-TUB` and `MNC-TUB`.
- **Web + Functions are format-agnostic but contract-coupled**: Order and feedback pages simply round-trip whatever `FlavorId` `/api/flavors` returns into `/api/orders` and `/api/feedback`, so changing canonical IDs mainly impacts returned payloads, posted payload examples, and assertions rather than validation logic.
- **Inventory catalog is larger than flavor catalog**: Inventory currently includes `QCC-TUB` and `AAL-TUB`, but `FlavorRepository` has no matching `QCC` or `AAL` flavor records; any migration to 3-letter canonical flavor IDs must decide whether to add those flavors or remove the extra SKUs.


### 2026-03-14 — FlavorId migration to canonical 3-letter codes

- **Canonical contract**: `FlavorId` is now the 3-letter flavor code (`MNC`, `VNE`, etc.), while inventory keeps the derived SKU shape `{FlavorId}-TUB`. `FlavorRepository` is the canonical flavor catalog and now uses those 10 codes directly.
- **Single bridge point**: `InventoryRepository.GetSkuForFlavorId()` is the single code path that converts `FlavorId` → SKU, and `CheckInventoryTool` now accepts canonical `FlavorId` values instead of raw SKUs.
- **Catalog alignment**: Removed inventory-only `QCC-TUB` and `AAL-TUB` entries so inventory matches the real 10-flavor catalog instead of carrying orphan SKUs.
- **Prompt/schema cleanup**: Order-processing prompts now distinguish FlavorId from SKU; order intake canonical line items carry `flavorId`, while fulfillment output still reports inventory `sku` values.
- **Validation status**: Baseline `dotnet test` initially failed because `InboundOrderTriggerTests` lagged behind the trigger constructor. After updating that fixture and the FlavorId literals, the full suite passed: 176 tests green.
- **Romanoff handoff**: Updated the order/trigger tests and added `InventoryRepositoryTests` so Romanoff has coverage for the FlavorId→SKU bridge and the removed orphan inventory SKUs.
