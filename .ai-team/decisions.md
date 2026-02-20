# Team Decisions

This is the canonical decision ledger for the FroyoFoundry Squad. All architectural, scope, and process decisions are recorded here.

---

## 2026-02-20: Order page design spec

**By:** Pepper Potts
**Status:** Assigned to Shuri for implementation
**Summary:** Full design specification for Order and OrderConfirmation pages

### Scope

Two new Razor Pages:
1. `/Order` — froyo order form (`Order.cshtml` / `Order.cshtml.cs`)
2. `/OrderConfirmation` — post-submission thank you page (`OrderConfirmation.cshtml` / `OrderConfirmation.cshtml.cs`)

Navigation: Add "Order" link to `_Layout.cshtml` between "Home" and "Feedback" nav items.

### Order Page Design

**Structure:** Single `.froyo-card` with linear top-to-bottom flow (Flavor Selector → Your Information → Shipping Address → Contact Information).

**Flavor Selector:** Visual radio card grid (not dropdown) — one card per flavor with emoji, name, category badge, description, and allergen icons. Category-to-emoji mapping provided. Required field.

**Your Information:** First Name + Last Name (2-col grid on md+, required).

**Shipping Address:** Street Address + Address Line 2 (optional) + City/State/ZIP (3-col row on md+, required). State dropdown includes all 50 US states + DC. ZIP validation: `[0-9]{5}(-[0-9]{4})?` pattern.

**Contact Information:** Email + Phone (2-col grid on md+, required). Both `type="email"` and `type="tel"` respectively.

**Size Callout:** 1-gallon-only informational alert (not a form field) — uses brand-tinted `var(--froyo-blueberry)` background.

**Submit Button:** "Place My Order 🍦" centered, `mt-4`.

**Validation:** Server-side errors at top (matches Feedback pattern), inline field errors with `asp-validation-for`.

### Order Confirmation Page Design

**Structure:** Single `.froyo-card` with celebratory header, order reference banner, order summary block (using `<dl>`), and back-to-home CTA.

**Page Header:** Includes customer first name in lead: "Your froyo is on its way, **@Model.FirstName**!"

**Order Reference Banner:** Uses `var(--froyo-mint)` + `var(--froyo-pistachio)` styling, displays reference number (e.g., `FRY-20260220-A3F9`) and confirms email address.

**Order Summary:** Bootstrap `<dl class="row">` with Flavor, Size (1 Gallon 🪣), Shipping address (full), and Email.

**Back to Home CTA:** Link to `/Index` using `froyo-btn-primary` class, text "Back to Home 🍧".

### CSS Additions

Add flavor radio card component to `froyo.css`:
- `.flavor-card input[type="radio"]` — hidden
- `.flavor-card-label` — flex column, centered, 2px border, hover lift effect
- `:checked` state — `var(--froyo-strawberry)` border, pink tint background, box-shadow
- `:focus-visible` state — 3px `var(--froyo-strawberry)` outline
- `.flavor-emoji`, `.flavor-name`, `.flavor-category`, `.flavor-description`, `.flavor-allergens` — utility classes for styling card internals

**Accessibility note:** Remove `opacity: 0.75` from `.flavor-allergens`; use full opacity with `#6c757d` color for WCAG contrast.

### Responsive Behavior

| Viewport | Flavor Grid | Personal Info | Address | Contact |
|---|---|---|---|---|
| Mobile | 2 cols (`col-6`) | 1 col stacked | 1 col stacked | 1 col stacked |
| Tablet (≥768px) | 3 cols (`col-md-4`) | 2 cols (`col-md-6`) | 5/4/3 split | 2 cols |
| Desktop (≥992px) | 4 cols (`col-lg-3`) | 2 cols | 5/4/3 split | 2 cols |

### Open Questions

1. **Order reference generation:** Who creates the order reference string (`FRY-20260220-A3F9`)? Recommend server-side generation in POST handler, passed via TempData.
2. **Flavor list source:** Order page needs `IEnumerable<Flavor>`. Follow Feedback.cshtml pattern with flavor service injection.
3. **State dropdown:** Recommend static readonly list of (Code, Name) tuples in page model.
4. **Redirect pattern:** POST `/Order` → RedirectToPage `/OrderConfirmation` with TempData (Post/Redirect/Get pattern).

---
