# Pepper Potts — Design History

## Sessions

### 2026-02-20: Order Page Design

**Task:** Design the UI/UX spec for two new Razor Pages — `/Order` (froyo order form) and `/OrderConfirmation` (post-submission thank you page).

**Requested by:** Michael S. Collier

**Artifacts produced:**
- `.ai-team/decisions/inbox/pepper-potts-order-page-design.md` — full design spec for Shuri to implement

---

## Learnings

### Patterns Observed in the Existing UI

- **Page container:** All content lives inside a single `.froyo-card` div — white background, 15px border-radius, 3px strawberry border, `box-shadow`, 2rem padding, `margin-bottom: 2rem`. This is the canonical page shell.
- **Page header block:** Every page opens with a `text-center mb-4` div containing a `.decorative-scoop` emoji row, an `<h1 class="froyo-heading">`, a `<p class="lead">`, and optionally a `<p class="text-muted small">` in monospace style. The monospace small-text pattern carries a light tech/humor tone.
- **Section dividers:** `<hr class="my-4">` separates logical form sections. Section sub-headings use `<h3 class="froyo-heading mb-3">` with an optional `<small class="text-muted">` aside in monospace.
- **Form labels:** `class="form-label"` — rendered chocolate (`var(--froyo-chocolate)`), font-weight 600 via froyo.css. Required fields are marked with `*` inline in the label text (not a separate element).
- **Form controls:** `class="form-control"` for inputs/textareas, `class="form-select"` for dropdowns. Focus state is strawberry-tinted via froyo.css override.
- **Validation:** `<span asp-validation-for="..." class="text-danger">` immediately after every field. Validation scripts partial loaded in `@section Scripts`.
- **Two-column layout:** `<div class="row">` with `<div class="col-md-6 mb-3">` for paired fields. Full-width fields use `col-md-12 mb-3`.
- **CTA button:** `.froyo-btn-primary` — gradient pill, centered in `div class="text-center mt-4"`, hover lifts 2px.
- **Success state:** `alert alert-success` (mint/pistachio) inside `.froyo-card`, then a centered link back to home using `.froyo-btn-primary`.
- **Error/validation alerts:** `alert alert-danger` (pink/strawberry) at top of form section, before the `<form>` tag renders.
- **Antiforgery:** `<form method="post" asp-antiforgery="true">` on every form.
- **Toast notifications:** Fixed top-right `toast-container` used for non-blocking warnings (API load issues). Not used for form validation — that uses inline alerts.
- **Emoji use:** Decorative emojis used freely in headings, buttons, and decorative rows — part of the brand voice.

### Design System Conventions Established for the Order Flow

- **Flavor selector pattern:** Introduced **radio cards** as a new component — a responsive grid of styled `<label>` elements wrapping hidden `<input type="radio">`. Checked state uses strawberry border + mint background tint. Allergen info shown via small badge row. This is the first use of a visual selection grid on the site.
- **Required CSS additions to froyo.css:** `.flavor-card`, `.flavor-card input[type="radio"]`, `.flavor-card-label`, `.flavor-card input:checked + .flavor-card-label` — all specified in the design spec.
- **Gallon sizing:** Since we sell by the gallon only, quantity is displayed as a read-only badge/callout rather than a form input — eliminates a confusing dead field.
- **State dropdown:** US state selection uses a full `<select class="form-select">` with all 50 states + DC, ordered alphabetically. No territories (US shipping = 50 states + DC for simplicity).
- **Address Line 2:** Optional field — label explicitly says "(Apt, Suite, Unit — optional)" with no `*` marker and no `required` attribute.
- **Confirmation page echo:** Shows customer name, selected flavor name, full shipping address, and a generated order reference. No sensitive data echoed.
