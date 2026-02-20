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

### 2026-02-20: Homepage Order Button Addition

**Task:** Add second CTA button to Index.cshtml for Order page navigation.

**What I changed:**
- Added `<a asp-page="/Order" class="froyo-btn-primary mt-3">npm install froyo</a>` button immediately after the existing "git commit --feedback" button in the hero section
- Button uses identical styling: same `.froyo-btn-primary` class, same `.mt-3` spacing, preserving the CLI/terminal theme established by the git-style command label
- Both buttons now appear side-by-side in the hero section, offering dual CTAs (feedback vs. order)

**Key file paths:**
- `source/DurableAgent.Web/Pages/Index.cshtml` — added line 13 with new Order page CTA button

### 2026-02-20: Phone Number Format Enforcement

**Task:** Enforce US phone number format `(xxx) xxx-xxxx` on both Feedback and Order forms.

**What I changed:**
- Replaced `[Phone]` validation attribute with `[RegularExpression(@"^\(\d{3}\) \d{3}-\d{4}$", ErrorMessage = "Phone number must be in the format (xxx) xxx-xxxx")]` in:
  - `source/DurableAgent.Web/Pages/Feedback.cshtml.cs` — PhoneNumber property (line 56)
  - `source/DurableAgent.Web/Pages/Order.cshtml.cs` — PhoneNumber property (line 82)
- Updated placeholder text to `"(xxx) xxx-xxxx"` in:
  - `source/DurableAgent.Web/Pages/Feedback.cshtml` — phone input (line 151)
  - `source/DurableAgent.Web/Pages/Order.cshtml` — phone input (line 172)

**Pattern used:** `^\(\d{3}\) \d{3}-\d{4}$`
- `^` — start of string
- `\(` — literal opening parenthesis (escaped)
- `\d{3}` — exactly 3 digits
- `\) ` — literal closing parenthesis + space
- `\d{3}` — exactly 3 digits
- `-` — literal hyphen
- `\d{4}` — exactly 4 digits
- `$` — end of string

This enforces server-side validation via the model attribute, and client-side validation via unobtrusive jQuery validation (no JavaScript masking required).

### 2026-02-20: Real-Time Phone Number Formatting

**Task:** Add real-time phone number formatting so users see `(xxx) xxx-xxxx` as they type digits.

**What I changed:**
- Added `formatPhoneNumber()` and `applyPhoneFormatting()` functions to `source/DurableAgent.Web/wwwroot/js/site.js` (shared across all pages)
- Wired up phone formatting in both:
  - `source/DurableAgent.Web/Pages/Feedback.cshtml` — added DOMContentLoaded script in @section Scripts to call `applyPhoneFormatting()`
  - `source/DurableAgent.Web/Pages/Order.cshtml` — added DOMContentLoaded script in @section Scripts to call `applyPhoneFormatting()`

**How it works:**
- `formatPhoneNumber(value)` strips non-digits, caps at 10 digits, and applies progressive formatting:
  - 1-3 digits: `(xxx`
  - 4-6 digits: `(xxx) xxx`
  - 7-10 digits: `(xxx) xxx-xxxx`
- `applyPhoneFormatting(inputElement)` attaches `input` and `paste` event listeners with cursor position preservation
- Each page's Scripts section calls `applyPhoneFormatting(document.getElementById('PhoneNumber'))` on DOMContentLoaded
- Vanilla JavaScript only (no external libraries)
- Formatted value `(555) 123-4567` matches the server-side regex, so validation passes automatically

