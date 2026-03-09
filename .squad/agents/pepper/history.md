# Project Context

- **Owner:** Michael S. Collier
- **Project:** durable-agent-demo — .NET 10 demo of Azure Durable Functions + Durable Agents (Microsoft Agent Framework). AI analyzes customer feedback for Froyo Foundry using stateful orchestrations, tool calling, and Azure OpenAI.
- **Stack:** C# / .NET 10, Azure Functions (Flex Consumption, isolated worker), Durable Task Scheduler, Microsoft Agent Framework, Azure OpenAI, Blazor/Razor Pages, .NET Aspire, Azure Bicep, xUnit, FakeItEasy, Azure Service Bus, managed identity
- **Frontend:** ASP.NET Core Razor Pages in `source/DurableAgent.Web/`. Named HttpClient "func" → Aspire service discovery. API config: BaseUrl + path settings (FeedbackPath, StoresPath, FlavorsPath).
- **Created:** 2026-03-08

## Learnings

### 2026-03-09 — Order Page & OrderConfirmation Page Patterns

#### TempData for Page-to-Page Data Flow
- **Keys used:** `OrderReference`, `FirstName`, `Quantity`, `FlavorName`, `StreetAddress`, `AddressLine2`, `City`, `State`, `ZipCode`, `Email`
- **Types:** All keys are stored as objects (strings or ints) in TempData dictionary
- **Flow:** Order.cshtml.cs sets TempData values after successful API submission, then redirects to OrderConfirmation with `RedirectToPage("/OrderConfirmation")`
- **Recovery pattern:** OrderConfirmationModel.OnGet() reads TempData with defensive casts and null-coalescing fallbacks (e.g., `TempData["FirstName"] as string ?? string.Empty`, `TempData["Quantity"] is int qty ? qty : 1`)
- **Quantity fallback:** Default to `1` if TempData["Quantity"] is missing or not an int (line 26 of OrderConfirmation.cs)
- **Order reference guard:** If `TempData["OrderReference"]` is missing or not a string, redirect back to /Order (line 37)

#### BindProperty Pattern on Order PageModel
- `[BindProperty]` decorator on public properties (e.g., `FlavorId`, `Quantity`, `FirstName`) binds form input to PageModel automatically
- **Default values:** Non-nullable types (e.g., `public int Quantity`) must have `= 1` default; nullable types (e.g., `public string? AddressLine2`) can remain null
- **Data flow:** Form `<input>` and `<select>` elements with `asp-for="PropertyName"` automatically populate PageModel properties on form submit
- **Case sensitivity:** HTML form names are case-insensitive, but property names in PageModel must match exactly

#### Data Annotations for Validation
- **Required:** `[Required(ErrorMessage = "...")]` marks a field mandatory; applies both server-side and HTML5 `required` attribute via tag helpers
- **Range:** `[Range(min, max, ErrorMessage = "...")]` enforces numeric boundaries (e.g., Quantity 1–10); message displays in `<span asp-validation-for="Quantity">`
- **StringLength:** `[StringLength(maxLength, ErrorMessage = "...")]` limits text input length
- **RegularExpression:** `[RegularExpression(@"pattern", ErrorMessage = "...")]` validates format (e.g., phone `(xxx) xxx-xxxx`, ZIP code `[0-9]{5}(-[0-9]{4})?`)
- **EmailAddress:** `[EmailAddress(ErrorMessage = "...")]` validates email format
- **Validation messages:** Rendered server-side by `<span asp-validation-for="PropertyName" class="text-danger"></span>` tag helper after form submission if ModelState is invalid

#### Quantity Dropdown Pattern
- **HTML structure:** `<select asp-for="Quantity" class="form-select">`
- **Placeholder option:** `<option value="">-- Select quantity --</option>` (value must be empty string to not bind to model)
- **Options loop:** `@for (int i = 1; i <= 10; i++) { <option value="@i">@i</option> }`
- **Display:** Both value and display text are the number (1–10)
- **Validation span:** Placed directly below with `<span asp-validation-for="Quantity" class="text-danger d-block mt-1"></span>`

#### OrderConfirmation Display Pattern
- **Quantity display:** Rendered as `@Model.Quantity × 1 Gallon 🪣` in the Order Summary section (line 32)
- **Conditional field:** `@if (!string.IsNullOrEmpty(Model.AddressLine2))` wraps optional AddressLine2 rendering to avoid extra blank lines
- **Property defaults:** PageModel properties have default values (e.g., `public int Quantity = 1`) to ensure safe rendering even if TempData is missing

#### Non-Nullable int with Defaults
- **On PageModel (Razor Pages):** `public int Quantity { get; set; } = 1` — must have default value, non-nullable
- **On API Models (Functions):** `public int? Quantity { get; set; }` — nullable to represent "not provided" state during deserialization, validated server-side in Functions
- **Rationale:** Razor Pages form binding and display logic assumes non-nullable int; API layer handles null to communicate validation errors back to caller
- **Conversion:** When passing from OrderModel to API (line 156), use non-nullable `Quantity` property directly; API receives it and validates range 1–10

---
