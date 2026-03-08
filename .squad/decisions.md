# Squad Decisions

## Active Decisions

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
