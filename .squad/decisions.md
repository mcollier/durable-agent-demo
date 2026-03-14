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

