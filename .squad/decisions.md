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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
