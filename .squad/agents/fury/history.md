# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Created:** 2026-03-08

## Learnings

### 2026-03-08 — Service Bus Queue → Trigger → Activity Endpoint Pattern

When adding a new externally-facing HTTP endpoint that enqueues work, the team follows a three-layer pattern: (1) **Submit Trigger** (`SubmitOrderTrigger`, route `POST /api/orders`) receives the HTTP request, deserializes and validates the payload using a model-level `.Validate()` method, then invokes an injected queue sender to enqueue the payload to a Service Bus queue; (2) **Inbound Trigger** (`InboundOrderTrigger`, bound to the same queue) is automatically invoked when messages arrive and can either be a no-op stub (log-only, deferred orchestration) or invoke an orchestrator with a `[DurableClient]` binding; (3) **Activity/Orchestration** (future) processes the queued work reliably and persistently. This pattern isolates HTTP concerns (deserialization, error reporting to clients) from async backend concerns (durability, retry, dead-lettering). It mirrors the established `SubmitFeedbackTrigger` → `inbound-feedback` queue → `InboundFeedbackTrigger` → `FeedbackOrchestrator` flow.

### 2026-03-08 — Dual-Layer Validation: Functions + Razor Pages

Validation is split across two independent layers with identical constraints: the Functions API model (`OrderRequest.Validate()`) enforces business rules at the backend with human-readable error messages returned in HTTP 400 responses, and the Razor Pages form (`OrderModel.cs` properties) enforces the same constraints using `[Required]` + `[Range]` + `[RegularExpression]` attributes for client-side guidance. This dual validation is NOT redundant—it allows the web form to provide immediate feedback without a round-trip, while the API layer ensures security (a malicious client bypassing Razor validation will still fail at the trigger). Optional fields (e.g., `AddressLine2`, `Email`, `PhoneNumber`) are explicitly marked nullable (`string?`) on the API model and nullable or have sensible defaults on the form—this signals intent to both developers and contract consumers.

### 2026-03-08 — Queue Sender Abstraction via Typed Interfaces

The project abstracts Service Bus queue sending behind a simple, domain-typed interface: `IOrderQueueSender` has a single method `SendAsync(OrderRequest order, CancellationToken cancellationToken)`, implemented by `ServiceBusOrderQueueSender` (concrete `ServiceBusClient` usage). This interface is registered as a singleton in `Program.cs` and injected into triggers via primary constructor DI. The abstraction decouples trigger code from Service Bus details (connection strings, client management, transient retry logic) and makes testing straightforward (mock the interface, no need to fake the entire Service Bus runtime). The pattern mirrors the existing `IFeedbackQueueSender` exactly, making it a reusable template for adding future queues.

### 2026-03-08 — Test Coverage Parallel to Implementation

Tests are written in parallel with implementation, not after. The test file (`SubmitOrderTriggerTests.cs`) covers the happy path (valid payload → 200 OK, logged), deserialization failures (malformed JSON → 400), null request (→ 400), and validation failures. Tests use the `When{Condition}_Then{Outcome}` naming convention and leverage `FakeItEasy` mocks for logger and queue sender fakes. Critically, tests document contract assumptions: e.g., `"{}"` deserializes to a valid request with all-null properties (→ 200 OK, no validation errors yet), while `"null"` deserializes to null (→ 400 Bad Request). This clarity ensures future changes to validation or error handling don't accidentally collapse subtle cases.

### 2026-03-08 — Scope: Required vs. Optional Fields

The `OrderRequest` model explicitly marks which fields are required (via `OrderRequest.Validate()` checks) and which are optional (no validation). Shipping fields (`StreetAddress`, `City`, `State`, `ZipCode`) are all required because fulfillment depends on them; `OrderReference` is required for traceability; `FlavorId` is required for product selection. Contact fields (`Email`, `PhoneNumber`) are optional because the web form treats them as optional and the submit trigger does not yet drive notifications; `AddressLine2` is optional for customers without apartment-style addresses. This explicit scoping is reflected in the model's nullable properties (`string?` for optional, string without `?` is not present) and the validation rules. The scope decision is documented in `.squad/decisions.md` to prevent future confusion about why certain fields are optional.

### 2026-03-09 — Quantity Field Design: Model Nullable, Form Typed

The `Quantity` field is nullable (`int?`) on the Functions API model (`OrderRequest`) because the HTTP client might omit it or send `null`, but the Razor Pages form binding (`OrderModel.Quantity`) is a non-nullable `int` with a default of `1` so the form always has a valid value (matching Razor's model binding behavior). Validation is identical in both layers: 1–10 inclusive, enforced by `Quantity is null or < 1 or > 10` on the API and `[Range(1, 10)]` on the form. This asymmetry (nullable model, non-nullable form) is deliberate: it allows the API to fail gracefully on missing quantity data while ensuring the web UX never submits invalid data. The test suite covers both the null case (API rejects it) and boundary cases (1 and 10 are valid, 0 and 11 are not).
