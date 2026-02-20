# History

## Project Learnings (from import)

**Project:** FroyoFoundry.com — AI Agent Demo
**Stack:** .NET 10, C# 14, Razor Pages (DurableAgent.Web), Azure Functions Isolated Worker (DurableAgent.Functions), Azure Durable Task Scheduler, Azure OpenAI, Service Bus, Bicep IaC
**Owner:** Michael S. Collier
**Goal:** Showcase Microsoft Agent Framework + Azure Durable Functions for stateful AI agents

### Key Architecture

- `DurableAgent.Core/` — domain models (sealed records), zero cloud SDK deps
- `DurableAgent.Functions/` — Azure Functions isolated worker with Durable orchestrations + AI agents
- `DurableAgent.Web/` — Razor Pages frontend (if present)
- `infra/bicep/` — subscription-scoped Bicep deployment (4-phase, managed identity, zero secrets)
- AI data flow: HTTP POST /api/feedback → Service Bus → InboundFeedbackTrigger → FeedbackOrchestrator (CustomerServiceAgent + EmailAgent) → Activities

### Key Conventions

- Durable Functions: function-based static method pattern (NOT class-based TaskOrchestrator<>)
- DTOs: `sealed record` with `required` properties
- AI agent tools: static classes with `[FunctionInvocation]` attribute
- DurableAIAgent pattern: `context.GetAgent(name) → CreateSessionAsync() → RunAsync<TResult>()`
- No secrets — all auth via managed identity + RBAC
- Tests: xUnit + FakeItEasy, naming `When{Condition}_Then{Outcome}`
- Build: `cd source && dotnet test DurableAgent.slnx`

## Learnings

### 2026-02-20: Order Page Implementation Assignment

**Task:** Implement Order and OrderConfirmation Razor Pages per Pepper Potts' design spec.

**Assigned deliverables:**
- `/Order` page (`Order.cshtml` / `Order.cshtml.cs`) with:
  - Flavor selector (visual radio card grid with emoji, category, description, allergens)
  - Your Information section (First Name + Last Name)
  - Shipping Address section (Street, Address Line 2, City, State dropdown, ZIP)
  - Contact Information section (Email + Phone)
  - Size callout (1-gallon static display, not form field)
  - Validation (server-side errors + inline field errors)

- `/OrderConfirmation` page (`OrderConfirmation.cshtml` / `OrderConfirmation.cshtml.cs`) with:
  - Celebratory header with customer first name
  - Order reference banner (mint/pistachio branding)
  - Order Summary block (using `<dl>` for key-value display)
  - Back to Home CTA

- Navigation update: Add "Order" link to `_Layout.cshtml` between "Home" and "Feedback"

- CSS additions to `froyo.css`:
  - `.flavor-card` radio button styling with hidden input + label pattern
  - Focus, hover, and checked states with brand color tokens
  - Allergen text accessibility fix (full opacity, not 0.75)

**Key design notes:**
- Flavor is primary purchase decision → visual cards (not dropdown)
- Gallon-only size → read-only callout (not form field)
- Post/Redirect/Get pattern for order submission → prevents duplicate-submit on refresh
- Confirmation page separate URL (`/OrderConfirmation`) — supports bookmarking, sharing, back-button safety

**Open questions for Maria Hill:**
- Order reference generation (server-side in POST handler, passed via TempData)
- Flavor list source (follow Feedback.cshtml pattern with service injection)
- State dropdown values (recommend static list in page model)

