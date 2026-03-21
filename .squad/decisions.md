# Squad Decisions

## Active Decisions

### Decision: OrderRequest Validation Fields

**Date:** 2026-03-08  
**Author:** Stark (.NET Developer)  
**Requested by:** Michael S. Collier

## Required Fields (validated with `string.IsNullOrWhiteSpace`)

| Field | Error message |
|---|---|
| `OrderReference` | `"orderReference is required."` |
| `FlavorId` | `"flavorId is required."` |
| `FirstName` | `"firstName is required."` |
| `LastName` | `"lastName is required."` |
| `StreetAddress` | `"streetAddress is required."` |
| `City` | `"city is required."` |
| `State` | `"state is required."` |
| `ZipCode` | `"zipCode is required."` |

## Optional Fields (no validation)

- `AddressLine2`
- `Email`
- `PhoneNumber`

## Error Response Contract

When validation fails, `SubmitOrderTrigger` returns HTTP 400 with:

```json
{ "errors": ["orderReference is required.", "..."] }
```

Note: `errors` is an **array** (not the singular `error` string used by the existing `CreateErrorResponseAsync` helper). This allows callers to surface all missing fields at once.

## Rationale

All shipping-address fields (`StreetAddress`, `City`, `State`, `ZipCode`) are required to fulfill an order. `OrderReference` is required for traceability. `FlavorId` is the core product selection. Contact fields (`Email`, `PhoneNumber`) are optional because the web form treats them as such and the trigger does not yet drive notifications.

### Decision: SubmitOrderTrigger Test Strategy

**Date:** 2026-03-08  
**Author:** Romanoff (Tester)  
**Requested by:** Michael S. Collier

#### Context

`SubmitOrderTrigger` is a stub endpoint (`POST /api/orders`) with no queue sender, no required-field validation, and all `OrderRequest` properties nullable. Tests needed to cover the trigger's only two failure modes (invalid JSON → 400, null body → 400) and the happy path.

#### Decision

**Test scope is intentionally narrow for this stub:**

- No queue sender fake — trigger has none
- No required-field tests — `OrderRequest` has no required fields
- 6 tests: 3 happy-path (full payload, reference-only, all-null fields) + 2 bad-input (invalid JSON, null literal) + 1 guard clause (null request)

**Key distinction documented in tests:**
- `"{}"` deserializes to a valid `OrderRequest` with all-null properties → **200 OK**
- `"null"` deserializes to a `null` reference → **400 Bad Request**

This asymmetry is non-obvious and worth explicitly testing so future contributors don't accidentally collapse these cases.

#### Rationale

Matching the `SubmitFeedbackTriggerTests.cs` pattern keeps test structure consistent. Tests cover exactly what the implementation guarantees — nothing more — so they remain valid when business logic is added later without needing to be rewritten from scratch.

### Decision: Order Endpoint Shape

**Date:** 2026-03-08  
**Author:** Stark (.NET Developer)  
**Requested by:** Michael S. Collier

#### Context

A new `POST /api/orders` endpoint was needed to receive order submissions from the Razor Pages web app (`Order.cshtml`). The endpoint is a stub — it receives the payload, logs it, and returns 200 OK. Business logic (e.g., queuing, orchestration) will be added later.

#### Decision

**Model — `OrderRequest`**

`sealed record` with all-nullable `string?` properties using `init` setters. No `Validate()` method. Properties mirror the order form fields exactly:

- `FlavorId`, `FirstName`, `LastName`, `StreetAddress`, `AddressLine2`, `City`, `State`, `ZipCode`, `Email`, `PhoneNumber`, `OrderReference`

> **⚠️ Amended 2026-03-08:** `Validate()` was subsequently added to `OrderRequest` — see "Decision: OrderRequest Validation Fields" below. The no-validation statement above is superseded.

**Rationale:** Keeping the model simple (nullable, no validation) makes the stub safe to evolve. Validation constraints can be added later when the endpoint drives real business logic.

**Trigger — `SubmitOrderTrigger`**

- Route: `orders`, Method: `post`, AuthorizationLevel: `Function`
- Primary constructor DI for `ILogger<SubmitOrderTrigger>` only (no queue sender yet)
- Deserializes with `PropertyNameCaseInsensitive = true` + `JsonStringEnumConverter`
- Returns 400 on null body or `JsonException`, 200 OK on success
- Logs `"Received order {OrderReference}."` on success

**Rationale:** Follows the established `SubmitFeedbackTrigger` pattern verbatim for consistency. Omitting the queue sender keeps the stub lightweight.

**Web App Integration**

`Order.cshtml.cs` `OnPostAsync()` now:
1. Generates `orderReference` first
2. POSTs an anonymous object (all bound form properties + `OrderReference`) via `JsonContent.Create(..., options: JsonOptions)` to `"api/orders"` on the `"func"` named `HttpClient`
3. On 2xx → proceeds with existing redirect flow
4. On failure (exception or non-2xx) → reloads flavors, adds a `ValidationErrors` entry, returns `Page()`

**Rationale:** Using an anonymous object avoids a web-project dependency on the Functions model. The `JsonOptions` in `Order.cshtml.cs` uses `JsonNamingPolicy.CamelCase`, which is compatible with `PropertyNameCaseInsensitive = true` on the Functions side.

#### Alternatives Considered

- **Shared DTO project**: Rejected — adds coupling between Web and Functions. The anonymous object approach is sufficient for a stub and can be revisited when the contract stabilises.
- **Fire-and-forget (no await)**: Rejected — we need to surface failures to the user before redirecting.

### Decision: Queue Environment Variable Naming Convention

**Date:** 2026-03-09  
**Author:** Michael S. Collier (via Copilot)

All Service Bus queue name environment variables follow the `{DOMAIN}_QUEUE_NAME` pattern:
- `FEEDBACK_QUEUE_NAME` = `inbound-feedback`
- `ORDER_QUEUE_NAME` = `inbound-orders`

The old `SERVICEBUS_QUEUE_NAME` was too generic once a second queue was introduced. The domain-prefixed pattern makes each queue's config explicit and independently overridable.

**Trigger binding pattern:** `[ServiceBusTrigger("%FEEDBACK_QUEUE_NAME%", Connection = "messaging")]`

### Decision: Add `inbound-orders` Service Bus Queue

**Date:** 2026-03-08  
**Author:** Rhodes (Azure/Infra Developer)  
**Requested by:** Michael S. Collier

Add an `inbound-orders` Service Bus queue to the infrastructure, configured identically to the existing `inbound-feedback` queue.

#### Bicep Settings

| Property | Value |
|---|---|
| `name` | `inbound-orders` |
| `maxDeliveryCount` | `10` |
| `deadLetteringOnMessageExpiration` | `true` |
| `lockDuration` | `PT1M` |

Both queues live in the `queues:` array of the AVM `serviceBusNamespace` module in `infra/main.bicep`.

#### Environment Variable

`ORDER_QUEUE_NAME: 'inbound-orders'` added to the Function App `appSettings` block in `infra/main.bicep`, alongside `FEEDBACK_QUEUE_NAME`.

#### AppHost Wiring

- `AppHost.cs` reads `Parameters:ORDER_QUEUE_NAME` from configuration and fails fast with `InvalidOperationException` if missing.
- The queue is registered with `sb.AddServiceBusQueue(orderQueueName)`.
- The value is injected into the `func` project via `.WithEnvironment("ORDER_QUEUE_NAME", orderQueueName)`.
- Both `appsettings.json` and `appsettings.Development.json` have `"ORDER_QUEUE_NAME": "inbound-orders"` in their `Parameters` objects.

#### Rationale

The `inbound-orders` queue mirrors the `inbound-feedback` pattern so that order submissions can be received and processed by an orchestrator in the same reliable, dead-lettered manner as feedback. Identical Bicep settings ensure consistent delivery guarantees across both queues.

### Decision: Order Queue Sender Interface & Inbound Trigger Pattern

**Date:** 2026-03-08  
**Author:** Stark (.NET Developer)  
**Requested by:** Michael S. Collier

Mirror the `IFeedbackQueueSender` / `ServiceBusFeedbackQueueSender` / `InboundFeedbackTrigger` pattern for the new `inbound-orders` Service Bus queue, using `ORDER_QUEUE_NAME` as the environment variable name.

#### What Was Created

| File | Purpose |
|---|---|
| `source/DurableAgent.Functions/Services/IOrderQueueSender.cs` | Interface typed to `OrderRequest` — single `SendAsync` method |
| `source/DurableAgent.Functions/Triggers/InboundOrderTrigger.cs` | Service Bus trigger — no-op stub, log-only, no orchestration |

#### Key Details

- **Interface**: `IOrderQueueSender` in `DurableAgent.Functions.Services` references `OrderRequest` from `DurableAgent.Functions.Models`.
- **Env var**: `ORDER_QUEUE_NAME` is bound via `[ServiceBusTrigger("%ORDER_QUEUE_NAME%", Connection = "messaging")]` — consistent with `FEEDBACK_QUEUE_NAME` for feedback.
- **No DurableClient**: `InboundOrderTrigger` intentionally omits the `[DurableClient]` binding — it is a no-op stub and does not start an orchestration.
- **Implementation deferred**: `ServiceBusOrderQueueSender` (concrete implementation of `IOrderQueueSender`) is not part of Wave 1 — to be added when the submit-order flow is wired up end-to-end.

#### Alternatives Considered

- **Combine order and feedback into one queue**: Rejected — separate queues keep concerns isolated and allow independent scaling and dead-lettering.
- **Use `SERVICEBUS_QUEUE_NAME` for both**: Rejected — would force a shared env var; `ORDER_QUEUE_NAME` makes each queue's config explicit and independently overridable.

#### Rationale

Strict mirroring of the feedback pattern keeps the codebase consistent and predictable. The no-op trigger establishes the binding and deserialization contract now so the concrete implementation can be layered in without structural changes.

### Decision: ServiceBusOrderQueueSender Implementation

**Date:** 2026-03-08  
**Author:** Stark (.NET Developer)  
**Requested by:** Michael S. Collier

`ServiceBusOrderQueueSender` mirrors `ServiceBusFeedbackQueueSender` exactly, with three substitutions:

| Aspect | Feedback | Order |
|---|---|---|
| Class name | `ServiceBusFeedbackQueueSender` | `ServiceBusOrderQueueSender` |
| Interface | `IFeedbackQueueSender` | `IOrderQueueSender` |
| Env var | `FEEDBACK_QUEUE_NAME` | `ORDER_QUEUE_NAME` |
| Generic type | `FeedbackMessage` (Core) | `OrderRequest` (Functions.Models) |
| MessageId | `message.FeedbackId` | `order.OrderReference` |

`SubmitOrderTrigger` mirrors `SubmitFeedbackTrigger` for error handling:
- `ServiceBusException` with `IsTransient` → 503 Service Unavailable
- Non-transient `ServiceBusException` → 500 Internal Server Error
- Unexpected `Exception` → 500 Internal Server Error

#### Rationale

Strict mirroring keeps the codebase consistent and predictable. Any developer familiar with the feedback pipeline can immediately understand the order pipeline. Pattern divergence is the primary source of accidental complexity in event-driven systems.

#### Alternatives Considered

- **Shared generic base class**: Rejected — `ServiceBusSender` is not generic; a base class would add abstraction overhead with no meaningful code reuse given the small size of these classes.
- **Single queue sender with type parameter**: Rejected — breaks interface segregation and complicates DI registration.

### Decision: Quantity Field Validation Contract

**Date:** 2026-03-09  
**Author:** Stark (.NET Developer)  
**Requested by:** Michael S. Collier

#### Context

The Order flow needed a Quantity field so users can select how many 1-gallon containers to order. This field must be validated on both the web layer (Razor Pages) and the API layer (Azure Functions).

#### Decision

##### `OrderRequest` (Functions model)

- Property type: `int?` (nullable) — distinguishes "not submitted" from 0.
- Validation in `Validate()`: `if (Quantity is null or < 1 or > 10)` → error `"quantity must be between 1 and 10."` (camelCase, period-terminated, consistent with all other `Validate()` errors).
- Placed after the `FlavorId` check in the error list — ordering mirrors field importance.

##### `Order.cshtml.cs` (Razor Pages model)

- Property type: `int` (non-nullable, default `= 1`).
- Attributes: `[BindProperty]`, `[Required(ErrorMessage = "Quantity is required")]`, `[Range(1, 10, ErrorMessage = "Quantity must be between 1 and 10")]`.
- Default of `1` avoids spurious `Required` failures on GET; `[Range]` enforces the contract on POST.
- Sent to Functions API in the `orderData` anonymous object alongside `FlavorId`.
- Stored in TempData as `TempData["Quantity"] = Quantity` (int) for the confirmation page.

##### `OrderConfirmation.cshtml.cs`

- Property type: `int` (non-nullable, default `= 1`).
- Read from TempData with `TempData["Quantity"] is int qty ? qty : 1` — avoids invalid cast.

##### `OrderConfirmation.cshtml`

- Replaces the hardcoded `"Size: 1 Gallon 🪣"` row with `"Quantity: @Model.Quantity × 1 Gallon 🪣"`.

#### Validation Error Messages

| Layer | Message |
|---|---|
| Functions (`Validate()`) | `"quantity must be between 1 and 10."` |
| Razor Pages (`[Required]`) | `"Quantity is required"` |
| Razor Pages (`[Range]`) | `"Quantity must be between 1 and 10"` |

#### Rationale

Using `int?` on the API model keeps it consistent with how other nullable types are handled (all properties on `OrderRequest` are nullable; validation is the single source of required-ness). The Razor Pages layer uses non-nullable `int` because the form select always posts a value; the default of 1 prevents accidental empty-state failures.

The range 1–10 was specified by the product owner and is enforced at both layers (defense in depth).

### Decision: Test Coverage — Wave 2 Order Triggers & Wave 3 Quantity Field

**Date:** 2026-03-08 (Wave 2), 2026-03-08 (Quantity)  
**Author:** Romanoff (Tester)  
**Requested by:** Michael S. Collier

#### Wave 2 Scope

Wave 2 adds `IOrderQueueSender` + `ServiceBusOrderQueueSender` (Stark) and wires `SubmitOrderTrigger` to enqueue. This decision records what edge cases are covered by the updated and new test files.

##### SubmitOrderTriggerTests.cs — Updates

**New test added: `WhenValidRequest_ThenEnqueuesOrder`**

Verify `IOrderQueueSender.SendAsync` is called exactly once with the correct `OrderRequest` payload on a valid, fully-populated request.

**Assertion strategy:** `A.CallTo(() => _orderQueueSender.SendAsync(A<OrderRequest>.That.Matches(o => o.OrderReference == "FRY-20260308-AB12" && o.FlavorId == "flavor-001" && o.FirstName == "Jane"), A<CancellationToken>._)).MustHaveHappenedOnceExactly()`

**Why:** Status-only (`Assert.Equal(200, ...)`) tests do not confirm the queue sender was invoked. A successful HTTP response without an enqueue would be a silent failure. This test makes the enqueue a first-class assertion.

**Service Bus error tests** (already present from Stark's concurrent work)

| Test | Failure Reason | Expected Status |
|------|---------------|-----------------|
| `WhenTransientServiceBusError_ThenReturns503` | `ServiceBusy` (IsTransient = true) | 503 Service Unavailable |
| `WhenNonTransientServiceBusError_ThenReturns500` | `MessageSizeExceeded` (IsTransient = false) | 500 Internal Server Error |
| `WhenUnexpectedError_ThenReturns500` | `InvalidOperationException` | 500 Internal Server Error |

**Decision:** Three distinct error paths are tested separately. The `ServiceBusy` reason is used for the transient case because it is a canonical example of a retriable error. `MessageSizeExceeded` is non-transient and unambiguous. The unexpected exception path verifies that non-`ServiceBusException` errors don't leak through as unhandled 500s at the platform level.

##### InboundOrderTriggerTests.cs — New file

**Edge cases covered**

| Test | Input | Expected |
|------|-------|----------|
| `WhenValidMessage_ThenLogsOrderReferenceAndReturns` | Valid `OrderRequest` JSON | `LogInformation` called once; no exception |
| `WhenNullBody_ThenLogsWarningAndReturns` | `BinaryData.FromString("null")` | `LogWarning` called once; no exception |
| `WhenMessageIsNull_ThenThrowsArgumentNullException` | `null` message reference | `ArgumentNullException` thrown |

**Why these three cases**

- **Valid message:** Confirms the happy path logs `"Received order {OrderReference}."` (the only observable behavior for a no-op stub).
- **Null body:** The `InboundOrderTrigger` guards against `order is null` after deserialization. This exercises the `LogWarning` branch without throwing. A future developer adding business logic should see a test that verifies graceful null handling.
- **Null message reference:** Guard clause test — `ArgumentNullException.ThrowIfNull(message)` is in the trigger. This ensures defensive coding is verified.

**What is NOT tested (and why)**

- **Malformed JSON body:** `ServiceBusReceivedMessage` in production always carries a valid serialized body — the Service Bus SDK itself enforces this. A corrupt byte sequence is an infrastructure concern, not a trigger concern. No test for this.
- **DurableTaskClient / orchestration scheduling:** `InboundOrderTrigger` is a no-op stub with no durable client. If orchestration is added later, a new test wave should cover it.
- **Log message content (exact string):** Log message strings are intentionally not asserted — only log level. This keeps tests resilient to message wording changes while still verifying the correct severity path was taken.

#### Wave 3 Scope — Quantity Field Tests

**Trigger Tests (HTTP layer)**

| Test | Input | Assertion |
|------|-------|-----------|
| `WhenQuantityIsNull_ThenReturns400` | omit `quantity` property | 400, error key contains "quantity" |
| `WhenQuantityIsZero_ThenReturns400` | `quantity = 0` | 400, same error |
| `WhenQuantityIsNegative_ThenReturns400` | `quantity = -1` | 400, same error |
| `WhenQuantityIsOne_ThenReturns200` | `quantity = 1` | 200 OK (boundary min) |
| `WhenQuantityIsTen_ThenReturns200` | `quantity = 10` | 200 OK (boundary max) |

**Model Tests (Validate() layer)**

| Test | Input | Assertion |
|------|-------|-----------|
| `WhenQuantityIsNull_ThenValidateReturnsError` | `Quantity = null` | Error message contains "quantity" |
| `WhenQuantityIsZero_ThenValidateReturnsError` | `Quantity = 0` | Same error |
| `WhenQuantityIsOne_ThenValidateReturnsNoErrors` | `Quantity = 1` | No errors (min boundary) |
| `WhenQuantityIsTen_ThenValidateReturnsNoErrors` | `Quantity = 10` | No errors (max boundary) |
| `WhenQuantityIsEleven_ThenValidateReturnsError` | `Quantity = 11` | Error message contains "quantity" |

**Edge Cases Worth Noting for Future Reference**

1. **Null vs. out-of-range collapse into one error message**: `Quantity is null or < 1 or > 10` means null, zero, and eleven all produce the same error: `"quantity must be between 1 and 10."`. This is intentional (callers shouldn't distinguish "missing" from "out of range" in the error UX), but it means there is no separate "quantity is required" error message.

2. **Omitting `quantity` in JSON vs. sending `"quantity": null`**: Both produce `null` for `int?` during deserialization. The trigger tests use omission (more realistic for a missing form field). Both are valid; omission is preferred for "field not provided" semantics.

3. **Boundary values 1 and 10 are inclusive**: Stark's implementation uses `< 1` and `> 10` (not `<= 1`, `>= 10`), confirming both boundaries are inclusive. Tests explicitly verify 1 → OK and 10 → OK. Future changes to the range (e.g., max raised to 12) will break these boundary tests as a signal.

4. **`WhenOptionalFieldsOmitted_ThenReturns200` semantics drift risk**: This test name implies "all optional fields can be absent". As new required fields are added, the body in this test must be kept in sync. Currently it includes `quantity = 1` explicitly. If a future required field is added without updating this test, it will start returning 400 — which is correct failure behavior but confusing if the test name isn't updated. **Recommendation:** Consider renaming to `WhenOnlyRequiredFieldsProvided_ThenReturns200` to make the invariant explicit.

5. **`CreateValidRequest()` / `CreateValidRequestBody()` are single points of update**: When a new required field is added:
   - `CreateValidRequest()` in `OrderRequestTests.cs` must be updated with a valid value.
   - `CreateValidRequestBody()` in `SubmitOrderTriggerTests.cs` must be updated with a valid value.
   - `WhenValidRequest_ThenEnqueuesOrder` assertion (which checks `o.OrderReference == ...`) may also need to assert the new field if it's semantically important.

   This pattern scales, but it's a single point of failure. Document this as a convention for future devs.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
# Decision: Flavor ID Alignment — SKU Codes as Primary Identity

**Date:** 2026-03-09  
**Author:** Fury (Lead)  
**Requested by:** Michael S. Collier  
**Status:** Approved (pending implementation)

---

## Decision

Replace sequential numeric Flavor IDs (`flv-001` through `flv-010`) with 3-letter SKU codes (`MNC`, `VNE`, `BBC`, etc.) as the canonical Flavor ID across the application.

### Mapping

| Old ID | SKU | Flavor Name |
|--------|-----|-------------|
| flv-001 | MNC | Mint Condition |
| flv-002 | BBC | Berry Blockchain Blast |
| flv-003 | CCN | Cookie Container |
| flv-004 | RRS | Recursive Raspberry |
| flv-005 | VNE | Vanilla Exception |
| flv-006 | NPP | Null Pointer Pistachio |
| flv-007 | JJT | Java Jolt |
| flv-008 | PBP | Peanut Butter Protocol |
| flv-009 | CCC | Cloud Caramel Cache |
| flv-010 | AIA | AIçaí Bowl |

---

## Rationale

1. **Single Source of Truth:** The SKU code (`VNE-TUB`) is the enterprise identifier used by `InventoryRepository`. Using it as the Flavor ID eliminates redundant mappings and manual cross-referencing.

2. **Alignment with Domain:** Product inventory is managed by SKU; flavors should use the same key. This is a domain-driven design principle: one entity, one identity.

3. **Reduced Bugs:** Fewer ID systems mean fewer opportunities for lookup mismatches (e.g., ordering flavor `flv-005` but checking inventory for SKU `VNE`).

4. **API Clarity:** Clients see the same ID in both the flavor list and inventory systems, making integration simpler.

---

## Scope

- **Files Modified:** 9 files (1 core model update, 1 repository update, 7 test updates).
- **Breaking Changes:** GET `/api/flavors` response JSON will use new IDs; POST `/api/orders` request payloads must use new IDs.
- **Backward Compatibility:** Not maintained (this is a demo app; no legacy clients are assumed to exist).

---

## Implementation Plan

See `/home/vscode/.copilot/session-state/f0c53eae-022b-4309-8569-4c18e2a1ce59/plan.md` for detailed steps, file-by-file changes, and test considerations.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| External clients depend on `flv-XXX` IDs | Communicate breaking change; provide migration notes if needed. |
| Test assertions hardcoded to old IDs | Systematic grep for `flv-` before commit; review test files carefully. |
| UI form binding breaks | Verify Razor Pages model binding still works post-change. |

---

## Approval

- **Lead (Fury):** ✓ Approved for planning and implementation.
- **Project Owner (Michael S. Collier):** Pending review of plan.

---

## Next Steps

1. Implement changes per plan (8 todo items, phased approach).
2. Run full xUnit test suite to validate.
3. Manual E2E testing: web form → inventory → confirmation.
4. Commit with conventional commit message.
5. Update release notes / API documentation.


### Decision: Flavor ID Migration — Canonical 3-Letter Codes

**Date:** 2026-03-14  
**Author:** Stark (.NET Developer)  
**Requestor:** Michael S. Collier  
**Status:** ✅ Implemented & Validated

#### Executive Summary

Migrated from split-brain identifier anti-pattern (legacy numeric `flv-###`, payload format `flavor-001`, inventory SKU `*-TUB`) to a single canonical format: **3-letter flavor codes** (`MNC`, `VNE`, `BBC`, etc.) used universally in APIs, tests, and AI prompts.

#### Decision

Use the 10 approved 3-letter flavor codes as the canonical `FlavorId` everywhere:
- **Customer-facing APIs:** `/api/flavors`, `/api/orders` accept and return `MNC`, `VNE`, `BBC`, etc.
- **AI tool calls:** `CheckInventoryTool` receives canonical codes and internally converts to SKU format
- **Test data & prompts:** All fixtures and examples use the same 3-letter codes

Inventory keyed by SKU in `{FlavorId}-TUB` format (`MNC-TUB`, `VNE-TUB`, etc.) — **single conversion point** in `InventoryRepository.GetSkuForFlavorId()`.

#### Why

The split-brain pattern guaranteed bugs:
- Code didn't know which format to expect in different layers
- Tests mixed `"flv-001"` (repo key), `"flavor-001"` (HTTP payload), `"VNE-TUB"` (inventory SKU)
- AI prompts treated FlavorId and SKU as interchangeable, creating ambiguity for tool calls

Canonicalizing on 3-letter codes eliminates the fake distinction and centralizes the inventory mapping.

#### Implementation

1. **FlavorRepository:** Updated all 10 flavors from `flv-001`, `flv-002`, ..., `flv-010` to `MNC`, `BBC`, `CCN`, `RRS`, `VNE`, `NPP`, `JJT`, `PBP`, `CCC`, `AIA`
2. **InventoryRepository:** Added `GetSkuForFlavorId()` and `GetAvailableQuantityForFlavorId()` — single bridge point for `FlavorId` → `{FlavorId}-TUB` conversion
3. **CheckInventoryTool:** Refactored to accept canonical codes and internally call `InventoryRepository.GetSkuForFlavorId()`
4. **Test updates:** Fixed 50+ hardcoded literals across 5 test files (`FlavorTests.cs`, `GetFlavorsTriggerTests.cs`, `OrderRequestTests.cs`, `SubmitOrderTriggerTests.cs`, `InboundOrderTriggerTests.cs`)
5. **HTTP examples & prompts:** Normalized all examples and tool descriptions to avoid mixing formats
6. **Orphan removal:** Removed inventory-only codes `QCC-TUB` and `AAL-TUB` (no matching flavor records) to align inventory with active flavor catalog

#### Validation

- Full test suite: ✅ 176 tests passed, 0 failed
- API contract: `/api/flavors` returns canonical 3-letter codes
- Order flow: Orders round-trip canonical codes without format conversion in multiple places
- Inventory lookup: Single bridge method centralizes `FlavorId` → SKU conversion

#### Consequences

- **Breaking change:** Old flavor ID formats (`flv-001`, `flavor-001`) no longer accepted by APIs — clients must migrate to 3-letter codes
- **Simplified code:** No ad hoc SKU rebuilding in multiple files; all conversions go through `InventoryRepository`
- **Aligned tests:** Test fixtures and examples now use the same format as production APIs
- **Future-proof:** When new flavors are added, they use a single canonical format throughout the system

#### Reference: Flavor Code Mapping

| Old ID | New ID | Flavor Name |
|---|---|---|
| `flv-001` | `MNC` | Mint Condition |
| `flv-002` | `BBC` | Berry Blockchain Blast |
| `flv-003` | `CCN` | Cookie Container |
| `flv-004` | `RRS` | Recursive Raspberry |
| `flv-005` | `VNE` | Vanilla Exception |
| `flv-006` | `NPP` | Null Pointer Pistachio |
| `flv-007` | `JJT` | Java Jolt |
| `flv-008` | `PBP` | Peanut Butter Protocol |
| `flv-009` | `CCC` | Cloud Caramel Cache |
| `flv-010` | `AIA` | AIçaí Bowl |

---

### Decision: Flavor ID Migration — Test Audit & Risk Report

**Date:** 2026-03-14  
**Author:** Romanoff (Tester)  
**Requestor:** Michael S. Collier  
**Status:** ✅ Complete (Pre-implementation audit; implementation by Stark validated)

#### Context

Pre-implementation audit to identify test surface risks, hardcoded literals, edge cases, and missing coverage for the planned Flavor ID migration. Audit goal: prevent discovery-time rework and ensure Stark could make independent decisions.

#### Key Findings

**High-Risk Test Files:**
1. `FlavorTests.cs` — 3 literals using `"flv-001"` format
2. `GetFlavorsTriggerTests.cs` — 3 API response assertions with old numeric codes
3. `OrderRequestTests.cs` — 1 helper using `"flavor-001"` format (ambiguous vs repository key)
4. `SubmitOrderTriggerTests.cs` — **10 occurrences** of `"flavor-001"` across happy-path, enqueue, and validation tests
5. `InboundOrderTriggerTests.cs` — 1 queue message fixture using `"flavor-001"`
6. `test.http` — Mixed formats: `"flavor-001"` (HTTP payload) vs `"VNE-TUB"`, `"MNC-TUB"` (inventory SKU)

**Edge Cases Documented:**
- **FlavorId vs SKU conversion:** No single point in code converted `VNE` → `VNE-TUB`; conversions happened ad hoc in `Order.cshtml.cs`, `Program.cs`, and tool implementations
- **Inventory-only codes:** `QCC-TUB` and `AAL-TUB` existed in `InventoryRepository` but had no matching flavor records → dead code or incomplete migration candidate
- **API contract breakage:** Old format IDs would stop working; no backward compatibility planned
- **Model binding safety:** Verified Razor Pages dropdown binding would automatically use new format if repository changed
- **Deserialization edge case:** `"INVALID"` flavor ID deserializes successfully but would fail at lookup; no format validation in `OrderRequest.Validate()`

**Missing Test Coverage:**
- ❌ Invalid flavor ID handling (e.g., ordering a non-existent flavor)
- ❌ Inventory SKU bridge verification (e.g., `VNE` correctly converts to `VNE-TUB`)
- ❌ E2E parameterized test with all 10 flavors flowing through API → queue → orchestration

#### Phased Test Update Strategy (Provided to Stark)

**Phase 1 (1–2 hrs):** Fix 18 literal references across 5 test files using mapping reference  
**Phase 2 (30 mins):** Normalize `test.http` examples to drop `-TUB` suffix from flavor IDs  
**Phase 3 (1–2 hrs):** Add integration test that submits orders for all 10 flavors and verifies inventory lookup  

#### Decisions Required from Implementation Team

1. **Inventory-only codes (`QCC`, `AAL`):** Remove from inventory (align with flavor catalog) or add as real flavors?  
   → Stark decided: Remove (simpler alignment)
2. **SKU bridge location:** Which layer owns `FlavorId` → `{FlavorId}-TUB` conversion?  
   → Stark decided: `InventoryRepository` (single point, explicit)
3. **Backward compatibility:** Accept old formats during transition or hard break?  
   → Stark decided: Hard break (cleanest for demo project)

#### Post-Implementation Validation

- **Test suite execution:** ✅ 176 tests passed, 0 failed (Stark's implementation resolved all identified hotspots)
- **Format consistency:** ✅ All test literals now use canonical 3-letter codes
- **Conversion centralization:** ✅ Single method in `InventoryRepository` bridges `FlavorId` → SKU
- **Orphan removal:** ✅ `QCC-TUB` and `AAL-TUB` removed; inventory aligned with 10 active flavors

#### Learnings for Future Test Work

1. **HTTP payload formats must be explicit in test helpers** — avoid accidental format mixing
2. **When ID formats are in flux, constants beat literals** — single update fixes all tests
3. **Audit trails matter** — if the system logs old and new formats, document which tests cover the transition
4. **Integration testing is underrated** — Phase 3 (E2E with all 10 flavors) would catch missed conversions immediately

#### Outstanding Opportunity

Consider adding Phase 3 test: parameterized test that submits orders for all 10 flavors and verifies inventory lookup succeeds or fails predictably. This would provide safety margin for the flavor → inventory pipeline.

---

### Decision: Real Email Sending in SendCustomerEmailActivity

**Author:** Stark  
**Date:** 2026-03-20  
**Status:** Implemented

#### Context

`SendCustomerEmailActivity` was a stub that logged and returned a string. The infrastructure (Azure Communication Services `EmailClient`, `EmailSettings` DI registration) was already in place via `builder.AddEmailService()` in Program.cs.

#### Decision

Implement real email sending using `EmailClient` from `Azure.Communication.Email` SDK, resolving it from DI in the static activity class.

#### Implementation Details

**Model Changes:**
- Added `required string Subject { get; init; }` to `EmailResult` (Core) — after `RecipientEmail`, before `Body`.
- Added `required string Subject { get; init; }` to `SendCustomerEmailInput` (Functions) — same position.

**Orchestrator:**
- Mapped `emailResult.Subject` in the `SendCustomerEmailInput` object initializer inside `FeedbackOrchestrator`.

**Activity Signature:**
- **Before:** `public static string Run([ActivityTrigger] SendCustomerEmailInput input, FunctionContext executionContext)`
- **After:** `public static async Task<string> RunAsync([ActivityTrigger] SendCustomerEmailInput input, FunctionContext executionContext)`

**DI Resolution in Static Class:**
```csharp
var emailClient = executionContext.InstanceServices.GetRequiredService<EmailClient>();
var settings = executionContext.InstanceServices.GetRequiredService<IOptions<EmailSettings>>().Value;
```

**Email Construction:**
- `senderAddress`: `settings.SenderEmailAddress` (configured ACS sender)
- `recipientAddress`: `input.RecipientEmail` (the actual customer — NOT `settings.RecipientEmailAddress`)
- `content`: `new EmailContent(input.Subject) { PlainText = input.Body, Html = input.Body }`

**Error Handling:**
- Wrapped `emailClient.SendAsync(WaitUntil.Completed, ...)` in try/catch
- On error: `LogError` + rethrow (preserves Durable Functions retry policy)

#### Test Pattern

Faked `EmailClient` using `EmailModelFactory.EmailSendResult` + `A.Fake<EmailSendOperation>()`, registered in a `ServiceCollection` injected into the fake `FunctionContext.InstanceServices`. Mirrors the existing pattern in `InboundOrderTriggerTests`.

#### Alternatives Considered

- **SendGrid / SMTP:** Rejected — ACS is already provisioned and follows zero-secrets managed identity pattern.
- **Class-based activity with constructor DI:** Rejected — project convention is static activities; DI resolved via `InstanceServices`.

#### Validation

- ✅ Project builds cleanly with no compilation errors
- ✅ 176 tests pass (no regressions)
- ✅ New test pattern mirrors existing conventions
- ✅ Email model construction follows ACS best practices (PlainText + Html body)

---

### Decision: SendCustomerEmailActivity Routes All Emails to settings.RecipientEmailAddress

**Author:** Stark (.NET Dev)  
**Date:** 2026-03-09  
**Requested by:** Michael S. Collier  
**Status:** Implemented

#### Decision

`SendCustomerEmailActivity` sends all emails to `settings.RecipientEmailAddress` (the value of the `RECIPIENT_EMAIL_ADDRESS` environment variable, resolved via `IOptions<EmailSettings>`) rather than `input.RecipientEmail` (the customer's actual email address from the feedback input).

#### Rationale

- **Consistent with `InboundOrderTrigger` behavior**: The order trigger already routes all outbound emails to the settings recipient address — `SendCustomerEmailActivity` should match this pattern.
- **Safe for development/testing**: Prevents accidental emails to real customer addresses in non-production environments.
- **Traceability maintained**: `input.RecipientEmail` (the intended recipient) is still captured in the log line alongside `settings.RecipientEmailAddress` (the actual send-to address), so the full intent is observable without triggering unwanted delivery.

#### Impact

- **`SendCustomerEmailActivity.cs`**: `recipientAddress` in `EmailMessage` constructor changed from `input.RecipientEmail` to `settings.RecipientEmailAddress`. Log updated to include both addresses. Return string uses `settings.RecipientEmailAddress`.
- **`SendCustomerEmailActivityTests.cs`**: Three tests updated to assert on `"recipient@example.com"` (the `RecipientEmailAddress` from the faked `IOptions<EmailSettings>`). `WhenDifferentRecipient_ThenResultReflectsRecipientEmail` renamed to `WhenDifferentInputRecipient_ThenResultAlwaysUsesSettingsRecipientAddress` with updated assertions verifying settings address is used and input address is not in the result.

#### Validation

✅ Implemented — 176 tests passing (125 Functions + 51 Core).

---

### Decision: README Accuracy Corrections — ACS Email

**Author:** Stark (.NET Developer)  
**Date:** 2026-03-20  
**Requested by:** Michael S. Collier  
**Status:** Complete

#### What Was Reviewed

Full README.md audit against:
- `SendCustomerEmailActivity.cs` — now sends real emails via `EmailClient` (Azure Communication Services)
- `EmailServiceExtensions.cs` — reads `RECIPIENT_EMAIL_ADDRESS`, `SENDER_EMAIL_ADDRESS`, `EMAIL_SERVICE_ENDPOINT`
- `EmailSettings.cs` — correct spelling `RecipientEmailAddress`
- `EmailResult.cs` + `SendCustomerEmailInput.cs` — both have `Subject` property
- `AppHost.cs` — passes `RECIPIENT_EMAIL_ADDRESS` (correct spelling) to Functions env
- `infra/main.bicep` — deploys both `emailService` (AVM) and `communicationService` (AVM)

#### Findings

| Check | Result |
|---|---|
| Misspelled `RECEIPIENT_EMAIL_ADDRESS` in README | ✅ Not present — no fix needed |
| `SendCustomerEmailActivity` described as TODO/placeholder | ✅ Not present — no fix needed |
| Model shapes (`EmailResult`, `SendCustomerEmailInput`) documented | ✅ Not documented in README — no inconsistency |
| Azure Communication Services in resource table | ❌ Missing — fixed |
| Email activity description mentions ACS | ❌ Vague ("send the follow-up email") — fixed |

#### Changes Made

1. **Azure Resources table**: Added `Azure Communication Services` row (ACS + Email Service are fully deployed by `infra/main.bicep` and required at runtime).
2. **Data Flow section**: Updated `SendCustomerEmailActivity` line to say "via **Azure Communication Services** to the configured recipient address (`RECIPIENT_EMAIL_ADDRESS`)" — makes clear the mechanism and that all emails route to the settings address, not the customer's email directly.

---
