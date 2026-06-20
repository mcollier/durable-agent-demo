# Plan: About Us Page (Issue #49)

## Problem Statement

The Froyo Foundry website is missing an **About Us** page. We need to create a new Razor Page that introduces the company, its mission, and the founder's story — building brand trust and connecting with customers.

## Approach

Create a static Razor Page (`About.cshtml`) following the established patterns in `DurableAgent.Web` (see `Index.cshtml`, `Feedback.cshtml`). No backend logic is needed — this is a static content page. Use CSS isolation (`About.cshtml.css`) for any page-specific styling, and wire up the nav link in the shared layout.

## Files to Create / Modify

| Action   | File                                                                 | Notes                                              |
|----------|----------------------------------------------------------------------|----------------------------------------------------|
| Create   | `source/DurableAgent.Web/Pages/About.cshtml`                        | Razor Page — must include `@page`, `@model AboutModel`, `ViewData["Title"]` |
| Create   | `source/DurableAgent.Web/Pages/About.cshtml.cs`                     | Minimal `AboutModel : PageModel` (static page)                              |
| Create   | `source/DurableAgent.Web/Pages/About.cshtml.css`                    | CSS isolation — only if page-specific styles are needed beyond `froyo.css`  |
| Modify   | `source/DurableAgent.Web/Pages/Shared/_Layout.cshtml`               | Add "About" nav item between Home and Order        |

## Content Sections (matching issue requirements)

1. **Hero** — Page headline with froyo emoji decorations and a short brand tagline
2. **Mission Card** — Company overview, mission statement, and values
3. **Founder Section** — Michael's story: Ohio State Buckeyes enthusiast, BBQ expert, avid golfer, family man
4. **Brand Narrative** — How Michael's personal passions shape the Froyo Foundry experience
5. **CTA Section** — "Place an Order" call-to-action linking to the Order page

## CSS Isolation Strategy

- Create `About.cshtml.css` **only if** the page requires styles not already covered by `froyo.css` (which includes `hero-section`, `froyo-card`, `froyo-heading`, `froyo-btn-primary`, etc.)
- Reuse existing CSS variables and utility classes already defined in `froyo.css` and `_Layout.cshtml.css` (e.g., `--froyo-pink`, `--froyo-strawberry`, `--froyo-chocolate`)
- Do NOT duplicate token/variable definitions already declared in `froyo.css`

## Navigation

Add an "About" nav item to `_Layout.cshtml` between "Home" and "Order":

```html
<li class="nav-item">
    <a class="nav-link" asp-area="" asp-page="/About">About</a>
</li>
```

## Acceptance Criteria (from issue)

- [ ] About Us page renders at route `/about`
- [ ] Page includes all content sections: company overview, founder story, passions, CTA
- [ ] Navigation link appears in site header alongside Home, Order, and Feedback
- [ ] Page follows existing visual style and branding (CSS variables, Bootstrap grid)
- [ ] No console errors or broken links (`asp-page="/Order"` CTA link)
- [ ] Page is responsive on mobile/tablet/desktop (Bootstrap grid already handles this)
- [ ] Semantic HTML: `<section>` with `aria-labelledby`, heading hierarchy, no empty alt text

## Implementation Notes

- The `AboutModel` page model is trivially simple — no DI, no `OnGet` logic (like `IndexModel`)
- The route `/about` is automatic from the filename `About.cshtml`
- `About.cshtml` must begin with `@page`, `@model AboutModel`, and set `ViewData["Title"] = "About Us"`
- Use semantic HTML: `<section>` with `aria-labelledby`, proper heading hierarchy, Bootstrap grid for responsiveness
- The CTA should link to `/Order` using the `asp-page` tag helper
- **Accessibility**: Decorative emoji must use `aria-hidden="true"` to avoid screen-reader noise (see existing `Index.cshtml` for reference)

## Tasks

1. Create `About.cshtml.cs` — minimal PageModel
2. Create `About.cshtml` — Razor Page view with `@page`, `@model`, all content sections, and aria-hidden emoji
3. Create `About.cshtml.css` — only if page-specific styles are necessary beyond `froyo.css`
4. Update `_Layout.cshtml` — add About nav link between Home and Order
5. Build and verify: `cd source && dotnet build DurableAgent.slnx`
6. Smoke test: confirm `/about` route renders, About nav link is present, CTA resolves to `/order`, headings and sections are semantically correct
