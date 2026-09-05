# Real Estate CRM — Build Log

**Multi-company, multi-branch SaaS for Bull Realty Global**

| | |
|---|---|
| Prepared for | Bull Realty Global |
| Document date | 23 Aug 2026 |
| Handbook baseline | 19 Jul 2026 |
| Modules shipped | 14 of 25 |
| Status | 🟡 In Progress |

This register tracks every module against the original handbook: what shipped, what's underway, and what's still on the drawing board.

---

## Summary

| Done | In Progress | Planned |
|---|---|---|
| **14** — built & verified | **1** — reporting & analytics | **10** — not started |

---

## Phase ruler

| Phase | Name | Weeks | Status |
|---|---|---|---|
| 1 | Foundation | Wk 1–3 | 🟢 Done |
| 2 | Core CRM | Wk 4–6 | 🟡 In Progress |
| 3 | Inventory & Deals | Wk 7–9 | ⚪ Planned |
| 4 | Partners & Commission | Wk 10–11 | ⚪ Planned |
| 5 | Automation & Reporting | Wk 12–13 | ⚪ Planned |
| 6 | Commercialization | Wk 14–15 | ⚪ Planned |
| 7 | Mobile & Hardening | Wk 16+ | ⚪ Planned |

---

## ⚠️ Amendment — Deviations from the 19 Jul handbook

1. **Backend swapped.** The handbook specified `Next.js + Supabase (Postgres/Auth/Storage/Realtime)`. On client instruction the backend is now a standalone **C# / .NET 10 Web API** with **MySQL 8.4** via EF Core (Pomelo provider) — a separate service, not a Next.js API route.

2. **RLS replaced.** Postgres Row-Level Security had no direct MySQL equivalent, so tenant isolation is enforced in the API layer instead: every query is scoped by a `company_id` claim read off the JWT, and write endpoints carry `[Authorize(Roles = …)]` guards.

3. **Login went further than spec'd.** Beyond the planned email + password + 2FA flow, the sign-in screen picked up a themed brand panel with a typewriter-animated headline, a date-rotating "real estate fact" line, full dark/light theming, and dropped the workspace-slug field and Google SSO after review.

4. **UI shell rebuilt on a premium open-source component set.** The first pass at the sidebar/table chrome was custom Tailwind styling and was rejected as still looking dated. The list views now run on TanStack Table v8 — sortable column headers, per-column faceted filters with live counts, a column-visibility toggle and paginated data grid — ported from the `Kiranism/next-shadcn-dashboard-starter` (shadcn/ui + TanStack Table) reference and adapted to this project's Radix-based `asChild` component pattern.

5. **Navigation re-architected top-down, then rebuilt to Salesforce.** The left sidebar shell was dropped after review. A first pass followed Wukong CRM's console; the final shell follows the Salesforce Lightning Design System, read straight from the SLDS Global Navigation blueprint: a white global header with the wide pill search and the favourites / setup / help / notifications / avatar cluster, and beneath it the SLDS "context bar" — App Launcher waffle, bold app name, object tabs with recent-record menus, a "More" overflow, and the 3px `#1b96ff` brand band along the bottom with the active tab's indicator stacked on top of it.

6. **Design tokens moved onto the SLDS palette.** Brand `#0176d3`, page ground `#f3f3f3` against white surfaces, SLDS's neutral text ramp (`#181818` / `#5c5c5c`), `0.25rem` radii and flat one-pixel shadows, with a matching dark theme. Records use Lightning's own layout language: a highlights panel, the chevron Path, and a dense two-column Details grid with hover-to-edit affordances.

7. **Open-source libraries carrying the UI.** shadcn/ui (Radix) for primitives, TanStack Table v8 for the admin list views, Recharts (via shadcn's chart wrapper) for dashboard visuals, dnd-kit for the dashboard builder and the pipeline board, and cmdk for the global search palette. The CRM list views run on a purpose-built server-paged grid rather than TanStack Table, because filtering, sorting and paging all resolve in SQL.

8. **Navigation regrouped around the job, not the table.** The context bar now carries Home, Leads, Engagement, Lead Automation, Calls, Sales and Reports, each with a dropdown listing its destinations; Companies, Branches and Users moved into the "More" overflow so the working tabs stay short. Routes follow the same shape (`/dashboard/engagement/*`, `/dashboard/automation/*`, `/dashboard/calls/*`, `/dashboard/sales/*`).

9. **Left rail removed.** Every module's destinations already live in its tab dropdown, so the contextual rail repeated them, cost ~224px of horizontal space on the densest screens, and gave the workspace two places to disagree about what was active. The grid columns it was crowding out (Owner, Budget) now fit without scrolling.

10. **Columns resize like a spreadsheet.** Every list grid lays out on `table-layout: fixed` with a `<colgroup>`; each header carries a drag handle, double-click resets a column to its declared width, and widths persist per user per list alongside density and column visibility. The drag writes to the `<col>` and table elements through refs and commits to state once on release — putting it in React state would re-render fifty rows on every mouse move.

11. **Lead record page: fields and behaviour, same layout.** The record page keeps its highlights panel, path and Details / Preferences / Related tabs. What changed is underneath — the base field set grew to cover a real estate enquiry end to end (personal, contact, lead, requirement, attribution), the hover pencil now edits that one field in place instead of opening a form, Preferences carries the actual requirement fields, and Related lists the calls, visits, follow-ups, quotations and opportunities attached to the lead plus a field-level change history read straight off the audit trail. The activity panel gained OBM visit, Transfer and Convert alongside the existing six, and Call / Site visit / Task now write a real call-log, visit and follow-up record rather than only a timeline note.

12. **Scheduling is slot-based, and refuses to double-book.** A person's availability is computed across site visits, field meetings and appointment-type follow-ups together, since they all take the same calendar. `GET /api/scheduling/availability` returns the day as 30-minute slots marked Available, Busy, Past or Outside hours — a slot only reads as available if the *whole* meeting fits, so a 90-minute visit cannot be dropped into the last half hour. Busy slots name what is in them. The create paths re-check on write rather than trusting the client's grid, and return a 409 naming the clash; booking on top of it needs an explicit `allowOverlap`.

13. **Email, Site visit, OBM visit and Task became full windows.** Those four never fitted the inline strip — scheduling needs a slot grid, an email needs templates and room to write. Each now opens a dialog: Email with templates that fill subject and body from the lead's own project and configuration; Site visit with project, visit type, party size, host, duration, transport, pickup and the slot grid; OBM with partner, contact, agent, travel distance, expense estimate, purpose and the slot grid; Task with channel, priority, owner, reminder offset, SLA — switching its channel to Meeting or Site visit swaps the plain due-date field for the slot grid, because those block a calendar and the others don't. Note, Call, WhatsApp, Transfer and Convert keep the inline composer, where a dialog would only be in the way.

14. **Timestamps carry their timezone.** DateTimes were serialised without an offset, so a browser in IST read every "3 hours ago" as five and a half hours off. A converter now writes them as UTC with a trailing Z.

15. **Filtering moved server-side.** The client-side filter engine was replaced by a query engine in the API that compiles a nested condition tree into EF Core expression trees. Facet counts, aggregates and paging are computed in MySQL over the whole result set, so the numbers above a list describe every matching row rather than the visible page.

16. **The dashboard became a query builder.** The Home dashboard was the last screen running on hardcoded fixture arrays, and the numbers on it contradicted the Reports page. Rather than wire each existing chart to an endpoint of its own, the widget model was inverted: a widget stores a *query* — object, group-by, optional breakdown, measure, aggregate, filter tree, period, sort, limit — and `/api/analytics/aggregate` compiles that into grouped SQL. `AnalyticsEngine` projects the filtered set into a fixed `{dimension, series, measure}` row so only the projection needs an expression tree and the grouping that follows is ordinary typed LINQ; `AnalyticsCatalog` registers eight objects (leads, deals, calls, site visits, quotations, follow-ups, contacts, OBM visits) against the *same* `FieldMap` the list views are filtered by, so a field added to a list view becomes groupable on a dashboard with no second declaration. This is what makes combinations nobody wrote an endpoint for — average AI score by source, call outcomes by branch, lead stage mix per channel — a dropdown change rather than a ticket.

17. **Chart style separated from chart data.** The same result renders as any of fifteen styles, so finding the readable shape is a menu click instead of a different widget. Styles that only read with one series (pie, donut, funnel, treemap, radial, metric) are hidden once a breakdown is set, and ones that need a breakdown (stacked, grouped, heatmap) are hidden until there is one — the editor cannot get into a state where the chart it is asking for cannot be drawn.

18. **Ordered vocabularies come from the domain.** Sorting a funnel by value puts Closed Won above Proposal and makes the drop-off column between them meaningless. Datasets now declare a canonical sequence for the fields that have one (`OpportunityStages.All`, `LeadStages.All`, `QuotationStatuses.All` …), the server sorts by it, and the funnel reads Qualification → Closed. Stage counts also do not only shrink, so the funnel prints `+56%` for growth rather than negating a negative into `−-56%`.

19. **The calendar is a read across objects, not a fifth object.** Nothing new is stored: `CalendarController` unions the three tables that already carry a date and flattens them to one event shape. It is deliberately separate from `SchedulingController`, which answers "is this person free at 3pm" and therefore only counts what blocks a slot — a calendar has the opposite job and must show the task due on Thursday even though a task blocks nothing. Follow-ups whose channel is Meeting or Site visit are drawn as timed blocks; the rest are drawn as due-time markers in an all-day strip, because giving a task an invented half-hour height would claim a duration the record does not have.

20. **A goal stores a query, not a metric.** The obvious build is an enum of trackable metrics and a number beside each. That version needs a code change every time sales want to track something new, and its numbers drift from the dashboards because they come from a different code path. Instead a goal carries the same query shape a widget does and is measured through the same engine, which is what makes "₹15 Cr booked", "40 site visits" and "12% conversion" one feature. Progress is never stored — a cached figure would be wrong within the hour.

21. **Every list view got a date window, and it picks its own column.** A quick date filter now sits on the toolbar of all nine list views — leads, site visits, OBM visits, tasks, follow-ups, calls, opportunities, quotations, contacts and inventory — offering Today, Yesterday, Last 7 / 14 / 30 / 90 days, this and last month, this quarter, this year, the next 7 and 30 days, and a custom range. Two decisions rather than one: *which* date and *what window*. A site visit has a scheduled date and a created date and "last 7 days" means something different against each, so each list declares the columns worth filtering on and defaults to the one that reads naturally — Scheduled for visits, Due for tasks, Issued for quotations, Started for calls. The window ANDs with the page's fixed scope and the user's own filter tree, so neither can be widened by it.

22. **Presets resolve to explicit dates on the client, not to the server's relative operators.** `lastNDays` in the query engine compiles to an open-ended `>= cutoff`, which is right for a created-at column and wrong for a scheduled-at one — "last 7 days" would have quietly swept in every visit booked for next month. Every preset is resolved to a concrete `between` before it is sent, so a window means the same thing whichever date column it is pointed at. It is also resolved at query time rather than at click time, so a list left open overnight on "Today" means today, not yesterday.

23. **Pace is stated, not forecast.** A goal reports what it should be at today assuming even progress across the window, and how far off that it is. Even pace is a crude model for a business that closes in bursts, so the field is labelled "expected by now" rather than presented as a prediction, and the separate "projected" figure says plainly that it extrapolates the current rate.

---

## Module register

### Phase 1 — Foundation (Wk 1–3)

| Module | Status | Notes |
|---|---|---|
| Repo & stack scaffold | 🟢 Done | Next.js 16 (Turbopack) + Tailwind v4 + shadcn/ui frontend; .NET 10 Web API backend |
| Database & migrations | 🟢 Done | MySQL 8.4 initialized locally, EF Core migration applied — companies, branches, users, user_branches |
| Auth + custom JWT claims | 🟢 Done | Two-step login → OTP verify → JWT carrying `company_id`, `branch_id`, `role`. OTP is a fixed demo code — no email/SMS provider wired yet |
| Company & branch management | 🟢 Done | List, create and edit — backend CRUD plus dialog forms, tested end to end. Both list views now run on the shared data grid with search, sorting, column visibility and pagination |
| User & role management | 🟢 Done | Invite with role + branch assignment, edit role/branches/active status. Seven-role matrix enforced via `[Authorize]`. List view has search plus a faceted role filter |
| Dashboard layout shell | 🟢 Done | Salesforce Lightning shell: white global header with pill search (Ctrl K), favourites, setup, help, notifications and avatar; SLDS context bar underneath with App Launcher, bold app name, object tabs carrying recent-record menus, "More" overflow and the blue brand band; a contextual left rail per module; and a shared page panel every screen sits on (title, actions, filter toolbar, body). Chrome is real — the dashboard numbers inside it are still demo fixtures |
| Dashboard & report builder | 🟢 Done | Rebuilt on a generic analytics endpoint — every widget is now a query, not a hardcoded chart. A widget declares object, group-by, optional second dimension, measure, aggregate, filter tree, period, sort and limit; the server compiles that into grouped SQL and returns rows already pivoted for a chart. **Widget editor**: a side panel with a live preview that runs the real query, plus per-widget filters (the same Salesforce-style builder the list views use, with value dropdowns read off the data), a chart-type switcher across 15 styles, and display toggles (legend, grid, data labels, 100% stacking, curve shape). **Card menu**: configure, switch chart type, expand, refresh, export CSV, duplicate, remove. **Charts**: column, grouped/stacked column, bar, line, area, stacked area, pie, donut, radial, funnel, treemap, heatmap, metric tile and table, with click-to-filter drill-down and click-to-hide legend entries. Four shipped dashboards, each built around a different question and reading different objects. Layouts persist per browser |
| Calendar | 🟢 Done | One grid over every dated object. `GET /api/calendar/events` unions site visits, OBM visits and follow-ups (tasks included) into a single event shape, so the four screens that each held part of the week now have one place that holds all of it. Four views — month, week, day and agenda — with a time grid that lays overlapping meetings out side by side, an all-day strip for due dates that block no slot, filter chips per source that double as the legend, and owner and channel filters. Overdue is computed, not stored, and colours ahead of the source so what slipped is what you see first. Clicking anything opens its detail and routes through to the record. Date maths is hand-rolled — no date library added |
| Goals | 🟢 Done | Targets on anything the CRM measures. A goal stores a query, not a metric name — object, measure, aggregate, filter tree, date field — so revenue, activity counts and conversion rates are the same feature rather than three. Assignable to a person, a branch or the whole company, over a month, quarter, year or custom window. Progress is recomputed on read through the same engine the dashboards use, and reports actual, percent complete, expected-by-now, pace, projected finish and days left, with health derived (On track / Behind / Achieved / Missed). Ratio goals divide one query by another for a real conversion rate. The card's progress bar carries a tick showing where the goal should be today |
| Multi-company setup & tenant switching | 🟢 Done | A super admin now signs in, is handed the company picker before the CRM will load, and picks the tenant to work in. Choosing is a token swap, not a client-side filter: `POST /api/auth/select-company` re-issues the JWT against the chosen company, and every global query filter reads the tenant off that token — so the isolation is the same one the rest of the API already relies on. `POST /api/companies` provisions a whole tenant in one call (company + head-office branch + its company admin), because a company row nobody can sign into is worse than no company. `GET /api/companies` returns every tenant to a platform admin and only its own to everyone else. A company admin is refused both the create endpoint and any company but its own (verified: 403/403/200). The header names the tenant you are inside at all times, and the account menu carries "Switch company". Seeded: `superadmin@bullrealtyglobal.com` (password from `Seed:AdminPassword`), plus two extra demo tenants |
| App launcher & per-app navigation | 🟢 Done | The product is now six apps — Lead Management, Post Sales, HR, Customer Care, System Firmware, Constructions — and the launcher picks one. The navigation bar then carries only that app's modules, so a leasing rep no longer sees payroll next to their pipeline. The active app is derived from the URL rather than stored state, so a deep link opens the right app; the remembered app only fills the gap on the administration screens (Companies, Branches, Users), which belong to every app and therefore change nothing when you open them. Lead Management is the built one; the other five ship as scaffolds — real routes, real tenant scoping, and each module stating what it is being built to hold rather than showing an empty screen |
| Super-admin platform console | 🟡 Partial | Tenant onboarding and the company picker are done (above). Plan and status are editable per tenant. Still open: feature flags, impersonation, usage metering, and removing a tenant |
| CRM loading animation | 🟢 Done | One mark in three sizes — a counter-rotating double ring with a pulsing core — drawn in SVG and CSS rather than pulled from a Lottie player: a few hundred bytes instead of a runtime plus a JSON payload, it inherits the theme through `currentColor`, and it keeps rendering when the network is exactly what is being waited on. Used as the full-screen splash while the session restores and while a tenant is being swapped, and as the in-page state on the list grids, the dashboard widgets and the admin screens. Flattens to a static mark under `prefers-reduced-motion` |
| Quotation cost sheet & PDF | 🟢 Done | Rebuilt against the pricing workbook (`Jeet Detail Studio`, CLP / One Time / Flexi / Studio sheets) rather than against a guess at what a quotation looks like. **Charge heads**: the cost sheet's Group / Revenue Head table is now a real model — parking, club membership, EDC/IDC, power backup, legal and the IFMS deposit sit under the unit cost, each with its own basis (per sq ft, lump sum, per slot, percent of unit cost), its own GST rate, and its own answer to whether the payment plan spreads it. The maintenance deposit and the registration fee do not: they fall due in one lump whatever the flat is being paid for, so putting them through the milestone percentages would misstate every instalment. They print under "Payable outside the schedule" instead. **Investor annexure**: the return the up-front plans are sold on — 9% assured for five years, 6% buy-back per year, exercisable after 2.5 — computed from plan configuration and printed on its own page. A construction-linked plan carries none and prints no annexure. **The document**: rupee sign rather than "Rs.", Indian digit grouping throughout, all three areas stated (saleable, built-up, carpet — the buyer is charged on one, lives in another, and RERA requires the third), a rate derivation that shows the discount coming off the base rate before the PLC goes on, a repeating schedule header, and the schedule total rendered once at the end rather than in a table footer that repeated the grand total under every partial page of a nineteen-row plan. Verified against the workbook to the paise: 23,400 − 20% + 3,000 PLC = 21,720/sq ft, basic 1,41,18,000, GST 16,94,160, total 1,58,12,160; annexure 63,53,100 + 42,35,400 = 1,05,88,500 |
| Payment plan fidelity | 🟡 Fixed | The Flexi plan carried a 10% standard discount against the workbook's 15%; corrected, with a backfill that leaves any desk-tuned value alone. The workbook's indicative-rent cell does not reconcile to a per-sq-ft rate, so it is left unset and configured per plan rather than guessed — the annexure simply omits the rent lines until it is filled in |
| Quotation lifecycle | 🟢 Done | The quotations screen is a working desk rather than a read-only list. **New quotation** picks a unit from any project (only sellable stock is offered) and opens the same builder the inventory board uses. **Edit** re-runs the pricing engine over an existing quotation — a quotation has no independently editable total, so changing the plan, the discount or the heads replaces the schedule, the charges and the approval verdict together, and the old approval request is cancelled so nobody rules on numbers that have since moved. **Approval**: send for approval on demand as well as automatically on an over-discount, withdraw before a decision, and the queue refuses a requester deciding their own. **Outcome**: issue, accept, decline, delete — each with the right refusals (issue and accept blocked while pending; an accepted quotation cannot be re-priced or deleted). **PDF** downloads from the row menu. **Revision** copies the whole priced basis, not just the lines |
| Lead / inventory / quotation sync | 🟢 Done | The three records move together. Drafting a quotation parks the unit on a three-day hold naming the quote, and writes it onto the lead timeline. Issuing extends the hold to the quotation's validity and moves the lead to Negotiation. Accepting books the unit against the quotation and moves the lead to Booked. Declining or deleting hands the unit back. Stage moves are forward-only and never past what a lead has already reached, so a revised quote to a booked lead does not drag it back to Negotiation; the release checks the hold reason first, so a document dying never drops a hold another quotation has since taken |
| Quotation commercial options | 🟢 Done | Discount, assured return, buy-back and rental yield are now four independent switches on the quotation rather than four properties of the payment plan. The same Flexi unit is sold to an investor with a nine-percent assured return and a buy-back, and to an end user with neither, and until now the plan could not tell them apart — the annexure printed on both. **The switches**: null takes the plan's terms, which keeps every older client printing what it always did; an explicit false is the rep deciding this buyer is not being offered it. Holding the list price is one of those decisions, so a quotation with the discount off prints "no discount applied — quoted at list price" rather than a nil concession line, and a rep can quote at the card rate on a plan whose standard discount is twenty percent. Each return carries its own rate, horizon and eligibility window, overridable per deal. **Snapshotted, not derived**: the offer is written onto the quotation when it is struck and rebuilt from those columns on every read, so repricing the plan next quarter cannot change what a customer was already sent — the previous build recomputed the annexure from the live plan on every PDF request. A re-price that sends no options replays the quotation's own switches rather than the plan's defaults, so nudging a discount cannot resurrect a return the desk had taken off. **The engine** (`InvestorReturns`) states the three rules the workbook follows: everything accrues on the basic sale price and never on the consideration; the assured return and the indicative rent are alternatives rather than a sum; and buy-back is appreciation on top of capital returned, so the quoted figure is BSP plus the accrual. A part year is pro-rated — a 4.5-year horizon pays five instalments, the last of them half |
| Quotation builder (enterprise form) | 🟢 Done | The dialog is a five-step record form — Customer, Pricing, Charges, Returns, Terms — with a summary rail that stays on screen throughout. Steps rather than one scroll because a cost sheet holds five unrelated decisions and a rep working down a single column reliably misses the last two; the step nav carries a completion tick, so it doubles as the checklist a manager would otherwise ask for. The rail is permanent rather than final because most of the decisions in the form are only meaningful against their effect on the consideration. **New**: a lead and contact search that links the quotation to the record it belongs to and pre-fills the buyer's details from it — a quotation nobody can trace back to a lead is a document with no pipeline behind it, since issuing it moves nothing to Negotiation and accepting it books nothing. Also the rate override and booking date (both exceptions, both gated by the same approval an over-discount is), validity presets, a discount label that prints on the document, per-group charge heads, and templates that now remember which returns they switch on |
| Warnings cleared | 🟡 Fixed | The API logged eight warnings on every start and more on request traffic; three of them were real defects wearing a warning's clothes. **Missing query filters**: `QuotationActivity`, `QuotationShareLink` and `QuotationTemplateCharge` hang off a soft-deletable parent and carried none, so a deleted quotation kept a visible timeline and — the one that mattered — a live share link. **Unordered row limiting**: the sentiment backfill read `Take(5000)` with no order, so a backlog larger than the batch could hand back the same rows every pass and never drain; the campaign and locality facets did `Distinct().Take(100)` unordered while every sibling facet ordered first. **Unordered First**: three seeders picked the company with a bare `FirstOrDefaultAsync()`, which on a multi-tenant database is a coin toss about which tenant a project lands in. Also split the quotation queries that load two or three collections at once — joined, EF returns the quotation once per milestone per charge, so the document reading the most rows read them most redundantly. HTTPS redirection now runs outside development only, where a host bound to HTTP alone made it warn on every request. The API log is silent through a full quotation lifecycle, PDF included |
| Duplicate React keys | 🟡 Fixed | Global search listed every module keyed by title, and five apps each register one called Overview — React was dropping four of them, and the five identical rows that did render were indistinguishable anyway. Keyed on the href now, with the owning app printed beside each. Also removed a `minDuration` prop passed to `CrmSplash`, which never accepted one: the minimum is held by `useMinimumDelay` at the call site, so the prop only made the splash look as though it timed itself. Frontend typecheck and production build are both clean |
| Floor plate from the architect's drawing | 🟢 Done | The unit panel's floor plate was a generic schematic — a core on the left, six units along the bottom and four up the right, which happened to be the right *shape* but was nobody's actual building. It is now traced from the issued drawing ("Jeet Homes at Varanasi — Hotel Floor, Option 2", Hafeez Contractor, 28.07.2026, 1:100, 660 sq m): ten guestrooms a floor, six along the bottom at 3.65 × 6.73 m and four up the right at 9.33 × 3.92 m, the two-metre lobby they open onto, and the service core drawn out — four lifts, two service lifts, two staircases, pantry, electrical, LV, fire lobby, fire lift and linen store. Laid out in **metres** and scaled by the viewBox, so a room's size on screen is its size on the plan rather than a designer's guess. One correction the drawing forced: the right-hand rooms were numbered the wrong way up — the plan puts guestroom 7 at the bottom and 10 at the top, and the old plate had them reversed, which put every one of those four rooms in the wrong place. Status colour, the sold stock and the highlight on the unit being viewed come from the CRM; the highlight carries a second inset outline because a colour change alone disappears on a photocopy |
| Area figures to reconcile | 🟡 Open | The drawing and the pricing workbook do not agree about room sizes, and the plate makes it visible. The drawing has all six bottom rooms at 36 sq m and all four right-hand rooms at 35 sq m — uniform within each band. The CRM has positions 1–4 at 650 sq ft saleable, 5–7 at 585 and 8–10 at 735. The bottom band reconciles: the CRM's 374 sq ft carpet is about 35 sq m, near the drawing's 36. The right band does not — the CRM's 423 sq ft carpet is 39 sq m against the drawing's 35. Saleable and carpet measure different things so they need not match, but a four-square-metre gap on the rooms being sold at the highest rate is worth the pricing desk confirming before it turns up at registration. The plate draws the drawing's geometry and prints the CRM's areas, and says so |
| Local Supabase | 🟢 Done | The CRM now runs against a local Supabase stack — twenty-one containers via the Supabase CLI, with Postgres 17.6 on `127.0.0.1:54322`, Studio on `:54323` and the REST/GraphQL gateway on `:54321`. The CRM's own tables live in the `postgres` database's `public` schema, which is where Supabase expects application tables and what makes them visible to Studio and the auto-generated API; Supabase keeps its own machinery in the `auth`, `storage` and `realtime` schemas, so nothing collides. **pgvector 0.8.2 ships in the image** and is enabled — no build required, which is what the AI work needs and what made the earlier plan to compile it against a native Windows Postgres unnecessary. The native PostgreSQL 17 install stays on the machine as a fallback and still holds a working copy of the same schema. Verified on Supabase: schema migrated, seeders ran, the pricing engine still reproduces the workbook to the rupee, the PDF renders, and the API log is silent |
| MySQL → PostgreSQL | 🟢 Done | The data layer now runs on PostgreSQL 17, installed natively rather than in Docker. Done **now** on purpose: the database carried zero leads, contacts and quotations after the demo purge, and everything else is rebuilt by the seeders — the same move after the lead import lands would have been a data migration rather than a provider swap. Pomelo out, Npgsql in; the MySQL migrations are archived to `_archive/Migrations-mysql` rather than deleted, and a single `InitialPostgres` replaces them. Four things needed real decisions. **Case folding**: MySQL's default collation folded case and the entire search surface silently depended on it — Postgres does not, so a search for "rajesh" would have stopped matching "Rajesh" with nothing erroring, results just thinning out. Fixed with a non-deterministic ICU collation (`crm_ci`, strength 2) applied to every string column bar identifiers, which leaves the expression-tree query engine untouched. **Timestamps**: the first attempt mapped them zoneless, closer to what MySQL did, and broke the moment a query said `ValidUntil < DateTime.UtcNow` — Npgsql renders that as `now()`, which is timestamptz, and Postgres refuses to compare the two. They are `timestamptz` now, with a value converter normalising every Kind to UTC, so neither the writes (`DateTime.UtcNow`) nor the reporting code's `new DateTime(year, 1, 1)` boundaries need touching. **jsonb** for the audit trail's change set, which also meant excluding it from the collation pass — Postgres rejects a collation on jsonb outright. Verified end to end: the pricing engine reproduces the workbook to the rupee on the new provider (23,400 − 20% + 3,000 PLC = 21,720/sq ft, basic 1,41,18,000, annexure 1,05,88,500, schedule variance nil), the PDF renders, and the API log is silent — zero warnings, zero exceptions |
| Demo data cleared | 🟢 Done | The workspace now carries the company's own records and nothing else. Two halves, and both are needed: `Seed:Demo` (default **false**) stops the generators — a delete alone would have meant eight hundred sample leads back in the list views the next morning, because `CrmSeeder`, `HistorySeeder` and the demo half of `DbSeeder` run on every start. `Data/Scripts/purge-demo-data.sql` removes what was already there: the four showcase projects with their stock and pricing, 806 leads, 220 contacts, 138 quotations, every opportunity, call, visit, follow-up and goal, the two extra demonstration tenants, and the audit trail that described them. What survives is the install (company, branch, users, platform admin), both live projects with their towers, units, rate cards, payment plans and charge heads, and every feature — only rows went. Trinity's sold units had demonstration buyers attached by the seeder rather than by a sale, so the buyer, salesperson and booking date are cleared and the stock reads Available again; the real buyers arrive with the data import. A `mysqldump` was taken first |
| Developer name on the document | 🟡 Fixed | The printed quotation fell back to a hard-coded "JHS Infra Homes LLP" wherever a project carried no developer of its own — a default that puts another company's name on a priced offer, which is worse than a blank line. It now falls back to the tenant. Trinity's developer is set to Jeet Homes |
| Bliss Emporio — Mall of Banaras | 🟢 Done | A second live project, seeded the way Trinity Tower is: a project is not one row, so the block, the four-level floor plan (60 shops), the level-wise rate cards, two payment plans and six charge heads land together — a project with no rate card cannot be quoted, and a rate card with no plan produces a price with no schedule under it. Retail heads rather than residential ones: common-area maintenance deposit, façade signage rights, power per KVA. **The commercial figures are indicative and marked as such** — areas, rates and plan terms are named constants at the top of the seeder for the pricing desk to correct in one place. The RERA number is deliberately left unset: it prints on the quotation as a statutory registration, and a placeholder there would be a false statement rather than a missing one |
| Board rate card by unit type | 🟡 Fixed | The inventory board resolved one rate card for a whole project and applied it to every tile, ignoring the card's unit type — which the quotation engine has always honoured. Invisible while every project sold one configuration; the moment a project prices by configuration it showed a food-court shop at the ground-floor retail rate, so a rep reading a tile would quote a number the quotation would then contradict — the exact failure the board's own "priced at today's card" rule exists to prevent. The board now loads the cards in force and each tile picks its own, most specific first; a single project-wide card carries no unit type and still matches everything. The board header reports the headline card as a "priced as of" note, since a project pricing by configuration has no one active card |
| Quotation document (print) | 🟢 Done | The PDF is now a ruled accounting voucher rather than a designed brochure — the format an Indian buyer, broker and banker already knows how to read. A boxed masthead over a title bar, a reference grid (quotation no. / dated / valid until / plan / revision / status), the party block against the property block, a rate derivation, a particulars grid that foots to a total, the amount in words, the payment schedule with a running cumulative, and a terms panel that closes into the signature block. **No colour at all**, and that is a working decision rather than a taste one: this document is printed on office lasers, scanned into loan files and photocopied at registrar counters, and every one of those steps drops colour to grey — a layout that separates a total from a row with a tinted band stops working the first time somebody copies it. Emphasis is carried by rules and weight instead, the grammar a ledger already uses. Totals are rows of their own table rather than bars beneath it, because a separate element with matching widths drifts against the table's cell padding and a total that does not sit under its column is the first defect a reader spots. The terms and signature panels are kept on one page, and the annexure takes a fresh page only when it would otherwise split |
| Quotation percent precision | 🟡 Fixed | Every fraction in the system was stored at two decimals alongside the money. A 12.5% discount saved as 0.13 and re-priced itself at thirteen percent the next time anyone edited the quotation, and a nineteen-instalment schedule stored each milestone's 0.0714 share as 0.07. The fraction columns — discounts, tax rates, milestone shares, return rates — are now decimal(9,6); the money columns are untouched. Listed explicitly rather than matched on a name, because "TaxAmount" and "TaxRate" differ by one word and getting a money column into that set would cap every price in the system at four figures |
| Quotation defects fixed | 🟡 Fixed | Three found while wiring the above. `DELETE /api/quotations/{id}` was declared on two controllers — an ambiguous match that surfaced as a 500 on every delete. `Revise` copied only the lines, producing a revision with no rate, no schedule and no charge heads: a blank cost sheet under a real quote number. And `Recalculate` reads discount and tax as percentages while the builder stores them as fractions, so revising a priced quotation turned 12% GST into 0.12% and left the revision worth roughly its own subtotal — priced quotations now skip it and copy their figures verbatim |
| CI pipeline | ⚪ Planned | Not started |

### Phase 2 — Core CRM (Wk 4–6)

| Module | Status | Notes |
|---|---|---|
| Leads workspace | 🟢 Done | The whole sales desk, rebuilt on a server-driven query engine. **Leads**: dense grid (22 columns, three row densities, pinned first column, per-user column visibility), nested AND/OR filter builder generated from server field metadata, live facet counts, saved views, bulk assign/stage/delete, one-click convert to contact + opportunity, and round-robin-style auto-assignment. **Pipeline**: drag-and-drop Kanban with per-stage value, weighted value and stage age. **Follow-ups / Tasks / WhatsApp / Email**: one queue object, four channel-scoped views, with an agenda strip and SLA clocks. **Calls**: log plus a telephony dashboard and an exportable per-agent/per-day report. **Site visits** and **OBM visits**: scheduling, check-in/out, feedback, geo capture and cost-per-lead. **Quotations**: server-computed totals, revisions, status transitions that move the deal. **Inventory**: project → tower → unit with expiring soft holds and a stack-plan view. **Contacts / Customer database**: unified timeline, consent flags, KMeans segments |
| Lead management (original) | 🟢 Done | Rebuilt against FarVision (a real estate ERP) as a reference: the list view runs on a TanStack Table data grid — sortable columns, saved list views as segmented "Display" pills (All / My leads / Unassigned / Hot & high / Booked), a collapsible advanced-filter row with faceted Source/Stage/Priority filters and live counts, free-text search, column visibility, pagination. The lead record page follows Salesforce's own layout: a highlights panel (object label, record name, compact key-field strip, Call / Email / Edit / Convert actions), the chevron Path across the six stages with Lost as a separate exit and a "Mark as …" shortcut for the next step, tabbed Details / Preferences / Related, a dense two-column field grid with hover-to-edit affordances, and an activity timeline with a quick-log toolbar (Note, Call, Email, WhatsApp, Site Visit, Task) that auto-logs stage and owner changes. Auto-assignment, dedup and scoring are still not built — capture and logging are manual |
| Contact & client 360 | 🟢 Done | Contacts and the customer database share one object and one view, separated by an invisible scope filter. Timeline endpoint merges calls, site visits, quotations and follow-ups; consent flags (do-not-call, do-not-email, WhatsApp opt-in) are enforced on the record |
| Local AI/ML | 🟢 Done | Five models, all ML.NET, all in-process — nothing is sent to an external inference service. **Lead conversion** (FastTree, trained on closed leads; scores cached on the row so lists sort in SQL). **Opportunity win probability** (FastTree over deal shape and engagement; trains on `LastOpenStage` rather than the outcome stage, which is what keeps the label out of the features). **Revenue and intake forecasting** (SSA, with a damped linear trend below twelve months of history). **Customer segmentation** (KMeans, clusters named after their own centroids). **Call-note sentiment** (text featurisation, labelled by what the call led to). Plus a rules-based next-best-action and an explainable assignment policy. A hosted service refits everything on startup and every six hours; each model reports its engine, training rows and test metric, and falls back to a documented heuristic when there is not enough labelled history |
| Enterprise backend hardening | 🟢 Done | Tenant isolation via EF global query filters over `ITenantScoped` (what replaces Postgres RLS on MySQL — a query cannot forget the tenant clause); soft delete with matching filters; an audit interceptor that writes the change trail on every SaveChanges so no write path can skip it; a dynamic query engine compiling filter trees to expression trees; ProblemDetails exception handling; rate limiting; response compression; health checks |

### Platform build-out — 25 Aug 2026

Four phases, taken end to end. Each was verified against a running API and a real
database rather than by inspection; the notes say what was proved.

| Phase | Status | What landed |
|---|---|---|
| **1 — Make the access model true** | 🟢 Done | `PermissionResolver` is wired into the request path through `[SecuredBy]`, which infers the verb from the HTTP method, plus `[PermissionAction]` where the convention is wrong. Every hand-written `[Authorize(Roles = …)]` list is gone — several named roles that had been renamed and authorised nobody. Field-level security strips restricted properties out of the response by walking the serialised JSON, so it reaches a lead whether it comes back bare, inside `items`, or nested under a quotation. Sessions are recorded against a `jti` and can be withdrawn: deactivating somebody, changing their role, resetting their password or suspending their company ends what they already hold, within seconds rather than at the twelve-hour expiry. Tenant suspension is enforced at sign-in and on every request — with a deliberate exemption for the platform operator, or suspending the only tenant would lock the platform out of itself. **Proved:** unticking Create on Lead makes `POST /api/leads` return 403 with a message naming the permission; hiding `phone` removes it from the wire; deactivating a user kills their live token |
| **2 — Make a plan mean something** | 🟢 Done | A plan catalogue in code (seats, lead and branch ceilings, which modules open, API/webhooks/custom fields/SSO), with per-company negotiated overrides on the company row. Limits are checked on the write path, not in the screen. Usage is snapshotted daily per tenant so a subscription argued over three months later reads the number as it stood. Trials expire on a schedule and suspend the workspace rather than deleting it. **Proved:** on Starter, an invite past five seats, a second branch and a lead past the ceiling are each refused with the plan, the limit and the count in the message |
| **3 — Let the tenant shape their own CRM** | 🟢 Done | Custom fields per tenant on leads, contacts and opportunities, stored in a `jsonb` bag and validated against their definitions — a choice field refuses a value outside its list, a used field refuses a type change, and a field holding data cannot be deleted, only retired. Pick lists (sources, loss reasons, requirement types, funding modes, call and visit outcomes) are per-company copies: renaming moves the label and never the stored value, so historic records keep resolving. Branding on the quotation and its public link. A setup checklist counted from the data rather than from a "wizard completed" flag. **Proved:** a field added through the console accepts a valid value, refuses an invalid one, and survives a round trip |
| **4 — Open the platform** | 🟢 Done | API keys scoped to a tenant and a set of `object:verb` permissions, stored as a hash and reaching `/api/v1` only — a key presented to the CRM's own endpoints is refused by path. A versioned public API: list leads and units, and capture an inbound enquiry with an `externalId` that makes a retried delivery idempotent. Webhooks with an HMAC signature over `{timestamp}.{body}`, a front-loaded retry ladder, auto-disable after repeated failure, a delivery log and a replay that re-sends the captured payload. The audit trail is readable at last, filtered by record type, actor and action. **Proved:** a real receiver got a signed `lead.created`, the signature verified against the endpoint's secret, and the retry landed it on the first attempt |

**Bugs the work surfaced and closed**

| Bug | Why it mattered |
|---|---|
| `View` could never be granted | `Read` is an alias for `View`, so the permission serialiser's "skip the composites" filter dropped View with it, and `ToString()` on that value answered "Read". Every profile reported View off, and saving what the screen showed would have stripped View from the matrix |
| The role dropdown created the wrong seat | It offered `BranchManager`, `TeamLead`, `SalesAgent` — none are catalogue keys, and `RoleCatalog.For` silently resolves an unknown key to the narrowest seat. A "Branch Manager" created from that list was a sales executive. Unknown keys are rejected now |
| Audit entries pointed at a placeholder | The interceptor read an inserted row's key during `SavingChanges`, before the database had assigned one, writing `Lead#-2147482647`. Creates are now held and written in a second pass, and they record what was set rather than only that something was |
| The audit trail carried secrets and traffic | A create entry copied webhook secrets and key hashes in full, and session touch-writes buried real edits within minutes. Both are redacted |
| Custom fields could not be queried | The bags inherited the case-folding collation, and Postgres refuses `LIKE` against a non-deterministic collation. They are `jsonb` now, and the usage probe is an indexable key-existence operator |
| A tenant suspension was a one-way door | Suspending the only company locked out the platform operator who had to lift it. Operators are exempt at sign-in, on select-company and in the request guard |

---

### Post-sales — 25 Aug 2026

A Farvision-class post-sales module: what happens to a unit after somebody buys
it. Verified the same way as the phases above — against a running API and a real
database, 31 checks, all passing.

| Area | Status | What landed |
|---|---|---|
| **Booking** | 🟢 Done | A booking snapshots its commercials off the quotation rather than pointing at it, so re-pricing a template never restates a signed deal. `AgreementValue` is kept apart from `OtherCharges` because only the first is registrable consideration — conflating them overpays stamp duty on every registration. Allotting the unit is atomic with creating the booking, inside a transaction handed to the retrying execution strategy so a replay cannot land half of it. Applicants carry role, relation, KYC state, PAN and only the last four digits of Aadhaar |
| **The payment plan** | 🟢 Done | The schedule belongs to post-sales once the deal is signed, not to the quotation: an instalment can be rescheduled, waived or added, and the plan revised wholesale — none of which touches a line already demanded or paid, and the last line absorbs the rounding drift. A copied schedule is topped up with a balancing instalment, because the quotation's percentages are computed on the unit consideration and a plan copied verbatim never bills the parking, the club or the deposits |
| **Demands and collections** | 🟢 Done | Milestone demands with day-wise penal interest computed against the *changing* balance, so a customer who paid 80% on time owes interest on the 20%. Receipts allocate oldest-demand-first, interest before principal, and credit the customer gross of TDS — crediting only what reached the bank leaves every 194-IA booking permanently 1% short. Cheques are held pending and a bounce restores the demands they had answered. A stage run raises every construction-linked instalment on a slab across a tower in one pass |
| **The ledger** | 🟢 Done | A customer statement that reconciles line by line to the reported outstanding, ageing in the five buckets the industry prints, collection efficiency measured against what was demanded rather than against sale value, and a six-month forecast of scheduled against demanded |
| **Paperwork and compliance** | 🟢 Done | A 27-row document checklist generated at booking across six stages, per-applicant rows where the document is per-applicant. RERA 70% designated-account split recorded on every receipt. Brokerage slabs with their trigger points and 194-H deduction |
| **Customers** | 🟢 Done | The people who bought, one row each — keyed on the lead rather than on the booking, so somebody who takes two flats is one customer with two units. A row appears only once its lead is closed as **Booked**; the gate is enforced on the server, not by a filter on the screen, so post-sales can never work an account the sales desk has not closed. Each row expands to the units behind it and links back to the originating lead. Bookings the gate holds back are counted and stated on the page rather than silently dropped |
| **The screens** | 🟢 Done | Salesforce record anatomy throughout: a chevron path across the booking's life, a highlights panel of the five figures somebody checks mid-call, then tabs over related lists — applicants, plan, ledger, receipts, documents, details. A collections desk with the ageing filter and the stage run, and an overview that opens with what is overdue rather than with sale value |

**Bugs the work surfaced and closed**

| Bug | Why it mattered |
|---|---|
| Interest paid was booked as interest waived | The recompute folded the interest a customer had *paid* into `InterestWaived`, so the write-off a finance team reconciles included money that had been collected, and the same rupees were subtracted from the balance twice — understating one booking's outstanding by ₹6,130.83. Paid interest now has its own column, and a waiver is only ever something a person recorded |
| Interest stopped accruing between payments | `RecomputeAsync` runs when money moves, and interest is the one figure that changes when nothing happens. Between the day a demand was raised and the day somebody finally paid, the ageing report, the desk and the customer's statement all understated what was owed, by a day more every day. A four-hourly accrual pass now brings every overdue booking up to today; it recomputes rather than accumulates, so a missed night simply lands the same numbers the next morning |
| A schedule that did not sum to the total | The quotation's milestone percentages are shares of the unit consideration, so a schedule copied straight across billed the flat and not the charges — ₹1,14,800 that was owed and would never have been invoiced |
| CompanyAdmin could not see a booking | `ApplyRoleDefaults` built the administrator's grant only from objects the sales defaults had already mentioned, so every object added later was invisible to the person who administers the company. The role now gets the whole catalogue, less tenant create/delete |
| The booking transaction was refused outright | `NpgsqlRetryingExecutionStrategy` will not accept a transaction it did not open, because replaying half of one is worse than not retrying. Booking, schedule, checklist and unit status are now handed to the strategy as a single unit |
| Instalment shares rounded to whole percent | `BookingMilestone.Percent` holds a fraction and had inherited money precision, so a 20% slab stored as `0.19` and the schedule printed a split back to the customer that was not the one they signed. It joins the other fraction columns at six decimals |
| The two halves of a sale contradicted each other | Accepting a quotation takes the flat off the board and marks it Booked; `BookAsync` then refused to allot anything that was not Available or Held. The only way to open a booking was to never accept the quotation. Booked is allowed through now, with a direct check that no *other* accepted quotation is holding the unit — the invariant that actually matters. Only surfaced when the button was wired; the suite had always booked from quotations nobody had accepted |
| A catch-all rename took three apps down | Converting Post Sales to a required catch-all (`[...section]`) so its root could have a real page put it beside the optional ones the other apps use (`[[...section]]`). Next refuses that pair across the whole tree — the router stopped resolving and **HR, Customer Care, Construction and every module under them started 404ing**, twenty URLs in all. Post Sales now has an explicit route file per module and no catch-all at all |
| A dead URL said the product was broken | Nineteen destinations are declared in navigation but not built. The UI correctly greys them out, so they are unreachable by clicking — but each is still a real URL somebody can type, bookmark or be sent. All of them answered with the framework's bare "This page could not be found". A root `not-found` now looks the address up in the two nav configs and either states what that module will hold, or says plainly that nothing is served there. The status code is still 404; only the page changed |
| The module had no way in | Nothing in the UI created a booking — the primary record of the module could only be made with a POST by hand. "Open booking" now sits on an accepted quotation and collects the applicants, their relation and their PAN, which is what the 1% TDS on every instalment is later filed against |
| Taking a receipt threw on success | The controller read `.Value` off an `ActionResult` that `Ok()` had produced, which is null every time — the payload lives in `.Result`. Both paths share one projection now |

### Post-sales, second pass — 26 Aug 2026

The first pass built the money: bookings, plans, demands, receipts, ledger,
escrow. This pass builds the **file** — the paperwork that follows a unit from
allotment to keys — and the letters that go with it.

| Module | Status | What it does |
|---|---|---|
| **Document automation** | 🟢 Done | Twelve seeded letters — demand, reminder, allotment, welcome, receipt, statement of account, possession offer and letter, loan NOC, cancellation, transfer, brokerage advice — each an editable HTML draft with real clauses in it: the penal-interest sentence, the 194-IA line, the "cheques in favour of" instruction. A merge engine fills them from the booking's own figures, including three rendered tables (schedule, ledger, outstanding). Deliberately not a template *language*: tokens in, values out, no expressions — the failure mode of a clever template is a demand letter for the wrong amount |
| **Template editor** | 🟢 Done | The wording is a commercial and legal decision that changes without a deployment, so it lives on a screen. Merge fields are published rather than left to be discovered, click-to-insert at the cursor, and **preview against a real booking** naming every token that did not resolve — before three hundred copies go out with a blank where the amount should be |
| **Letters on the booking** | 🟢 Done | Every letter stored **as it went out**, not re-rendered on demand. When the template is edited next month the copy the customer holds must still be the copy this system can show — every dispute in this business turns on exactly that. Draft → Issued → Sent, with print |
| **Agreement & registration** | 🟢 Done | A strictly ordered ladder: drafted → with customer → franked → executed → registered. A rung cannot be skipped, franking will not record without the stamp duty actually paid, and registration will not record without the number the sub-registrar issued. The consideration is snapshotted at drafting so a later edit cannot restate what a registered deed says |
| **Home loan** | 🟢 Done | Sanction → tripartite → disbursement, in that order and enforced. A disbursement writes a receipt into the ledger at the same moment, so the account can never show a bank that has paid and a customer who still owes. A lapsed sanction with money still to come is flagged — it is why a bank-funded instalment stops arriving and nobody looks for it |
| **Possession & handover** | 🟢 Done | Offer → joint inspection → snags → collections → keys. Handover is gated on every one of them and the screen leads with what is still in the way rather than with a button that fails. Dues are read from the ledger at the moment of the check, not from a flag somebody ticked last week |
| **Cancellation & refund** | 🟢 Done | Requested → approved → refunded. The refund arithmetic is computed rather than typed — received, the clause's forfeiture, brokerage already paid out, what is left. Approval releases the unit back to inventory and cancels the open demands, so nothing keeps accruing interest against a customer who has left |
| **Transfer / name change** | 🟢 Done | The incoming buyer becomes the primary applicant and the outgoing one is kept on the record as `PreviousOwner` rather than overwritten — their payments have to stay attributable for the rest of the file's life, because a TDS certificate issued three years ago names a person. Completion is gated on the transfer charge being received in full |

**Proved against the running system**

| Test | Result |
|---|---|
| Demand letter generated | `DEM/2026/0001`, **zero unresolved tokens**, real figures throughout |
| Agreement: skip to Registered | **400** "Record drafted first." |
| Agreement: frank with no duty | **400** "Franking needs the stamp duty that was paid." |
| Agreement: register with no number | **400** "Registration needs the number the sub-registrar issued." |
| Handover before anything | **400** naming both gates |
| Handover with 7 snags | **400** naming all four gates |
| Handover after snags and collections | **400** "5,00,000 is still outstanding" — the last real gate |
| Template preview | "every token resolved" against a live booking |

**Bugs the work surfaced**

| Bug | Why it mattered |
|---|---|
| Merge tokens collided with C# | The seeded templates were written in a raw *interpolated* string, where `{{…}}` is the interpolation hole — the same delimiter the merge tokens use. The two languages collided and the compiler won |
| Two different things called `DocumentTemplate` | The existing record defined a paper to **collect** from a customer; the new one defines a letter the system **produces**. Renamed the first to `ChecklistDocument`, which is what it always was |

**Still open**

| Gap | Where it stands |
|---|---|
| Company letterhead address | `Company` carries no postal address, so `{{company.address}}` renders as a dash. The token exists and the letterhead is laid out for it; the field and its settings row do not |
| Brokerage | Slabs, triggers and 194-H are modelled and the advice letter is seeded, but nothing computes or releases a payout yet |
| Reminder ladder | The reminder letter exists and can be written by hand or in bulk. Nothing sends it on a schedule — that is a job kind away, and no mail provider is connected regardless |
| Allocation restatement | Unchanged from the first pass: a recomputed accrual leaves the original receipt split stale. Totals reconcile; the two columns disagree about which rupees were interest |

---

**Still open in post-sales**

| Gap | Where it stands |
|---|---|
| Agreement, loan, possession, cancellation, transfer | The models are built and the record page reports their state, but nothing drives them: there is no screen or endpoint to frank an agreement, record a disbursement, offer possession or process a cancellation. Today those are DB edits |
| Documentation, Handover | Still scaffolds. The data behind them exists — the document checklist, the possession record, the applicants — so these are screens over objects that are already there, not new modelling |
| Brokerage | Slabs, triggers and 194-H deduction are modelled and computed, with no screen to set a slab or release a payout |
| A recomputed accrual leaves its allocation stale | Interest is recomputed against the balance as it changed, but a receipt's allocation split was written when it was taken. If the recompute lands on a smaller figure than was charged at the time, the split still reads the old one — the totals reconcile, the two columns disagree about which rupees were interest. Restating allocations during recompute is the fix, and it is a design decision rather than a patch |
| Reminder ladder | Demands turn overdue and accrue, but nothing reaches out. The follow-up objects and the WhatsApp/email queues exist and are unwired for the same reason as elsewhere: no provider is connected |

---

### Phase 3 — Inventory & Deals (Wk 7–9)

| Module | Status | Notes |
|---|---|---|
| Property / inventory management | ⚪ Planned | Project → tower → floor → unit hierarchy with realtime double-booking safeguards |
| Deals & booking workflow | ⚪ Planned | Kanban pipeline, site-visit scheduling, payment milestones, booking documents |

### Phase 4 — Partners & Commission (Wk 10–11)

| Module | Status | Notes |
|---|---|---|
| Channel partner + commission engine | ⚪ Planned | Sub-broker hierarchy, self-service lead registration, commission structures & payouts |
| Communication hub | ⚪ Planned | WhatsApp Business API, click-to-call/CTI, unified activity timeline |

### Phase 5 — Automation & Reporting (Wk 12–13)

| Module | Status | Notes |
|---|---|---|
| Task & workflow automation | ⚪ Planned | SLA tracking, status-change triggers, drip sequences |
| Documents & e-signature | ⚪ Planned | Per-company document library, agreement templates, e-sign integration |
| Reporting & analytics | 🟡 In Progress | The Home dashboard now reads live aggregates over eight objects through `/api/analytics`. What is left is scheduled delivery — subscribing to a dashboard and having it emailed on a cadence |

### Phase 6 — Commercialization (Wk 14–15)

| Module | Status | Notes |
|---|---|---|
| Billing & subscription | ⚪ Planned | Plan management, usage add-ons, payment gateway, trials |
| Public API & portal integrations | ⚪ Planned | 99acres, MagicBricks, Housing.com, Meta/Google Lead Ads connectors |

### Phase 7 — Mobile & Hardening (Wk 16+)

| Module | Status | Notes |
|---|---|---|
| Mobile field-sales PWA | ⚪ Planned | Offline-tolerant lead updates and site-visit logging |
| Audit, security & compliance | ⚪ Planned | Full audit trail, anomaly alerts, DPDP/GDPR export & deletion tooling |

---

## Statutory billing — 26 Aug 2026

The R&D pass over Farvision's booking-to-handover scope turned up sixteen concepts this system
did not model at all. The biggest hole was the money's legal spine: **there was no GST anywhere**
— only loose `TaxRate` decimals scattered across the pricing models (0.12 in one place, 0.18 in
another, 5% in a third), and no tax invoice, credit note, cheque register or TDS tracking.

This batch closed the four that are legally required to bill an Indian under-construction flat.

| Piece | What it does | Verified |
|---|---|---|
| **GST profile** | Per project, because a two-state developer holds two GSTINs and the place of supply for immovable property is the state the *building* stands in — never where the buyer lives. The state code is derived from the GSTIN's first two digits rather than typed, since a code that disagrees with the GSTIN is a rejected return nobody notices until the filing bounces. Carries the OC date, after which the supply leaves GST entirely | Saved `27AABCB1234C1Z5`; state code came back `27`, IFSC upper-cased |
| **Tax invoice** | Raised against a demand, with its own unbroken per-financial-year series. Applies the statutory abatement — one third of the consideration is deemed land and is not taxed, so 5% is charged on two thirds, an effective 3.33%. Splits CGST/SGST with the odd paisa to CGST, the convention every accounting package here follows | `BRG/GST/2026-27/0001` on ₹4,46,428.57 → taxable ₹2,97,619.05, abatement ₹1,48,809.52, CGST ₹7,440.48 + SGST ₹7,440.47 = ₹14,880.95. Second attempt on the same demand refused by number |
| **Credit note** | The only way to reduce an issued invoice. Carries the *invoice's* rate, not today's, because the reversal has to undo what was charged. Partial credits allowed — that is how an area reduction is handled — with a running total so credits can never exceed the invoice | Draft refused ("cancel the draft rather than crediting it"); over-credit refused naming the ₹4,46,428.57 headroom; partial ₹46,428.57 credit produced `CN/2026-27/0001` |
| **Cheque register** | Post-dated cheques, which are money promised but invisible to a receipt-based ledger — no receipt exists until one clears, so nothing else can answer "what is due for banking this week". Clearing raises the receipt *and* allocates it; bouncing reverses both | Early banking refused ("dated 15 Sept 2026"); duplicate number refused; clearing moved booking 4 from ₹0 received / ₹43,54,941 outstanding to ₹1,50,000 / ₹42,04,941, and the bounce put it back exactly |
| **TDS certificates** | The Form 16Bs buyers owe back under 194-IA. The buyer withholds 1% and is credited the gross on trust; until the certificate arrives the developer has given credit for money it cannot prove was deposited. A sync opens a row per TDS-bearing receipt, so nobody has to remember | ₹4,95,000 + ₹5,000 TDS → one row, gross ₹5,00,000, quarter `Q2 2026-27`. Re-sync opened 0. A ₹4,200 certificate was flagged `Mismatched` — "short by 800.00" — and the correct one cleared the flag |

Two things worth a decision:

- **The seeded demands carry 12% tax**, which is the old pre-2019 rate. The invoice now computes
  the correct 3.33% effective, so demand `BRG/DEM/2026/0001` asks for ₹5,00,000 while its invoice
  totals ₹4,61,309.52. The invoice is right; the demand's own `TaxAmount` is a stale cost-sheet
  estimate. The cost sheet should be re-priced off the GST profile.
- **Nothing renders the invoice as a document yet.** The figures are correct and the record exists;
  a printable tax-invoice template alongside the twelve existing letters is the obvious next step.

### Still missing from the full Farvision ERP scope

Confirmed absent, in the order they cost money:

| Missing | Why it matters |
|---|---|
| Area re-measurement | Carpet area on completion routinely differs from what was sold. The consideration is revised, which means a fresh demand or a credit note. Common enough that its absence shows on nearly every project |
| Add-on / customisation works | Extra billing for buyer-requested changes, outside the payment plan |
| Unit change / swap | A buyer moving to a different unit mid-booking |
| Snag list as real items | Possession tracks only a *count* of snags. A list nobody can itemise is a list nobody can close |
| Defect liability period | The five-year warranty window after handover, and claims inside it |
| Society formation & handover | The point the developer stops being responsible for the building |
| Maintenance billing | Recurring charges after possession |
| RERA delay compensation | Interest owed *to* the buyer when possession is late — a liability, not a receivable |
| RERA quarterly progress (QPR) | The filing, and Form 3 for escrow withdrawal |
| Brokerage engine | Modelled and the advice letter is seeded, but nothing computes or releases a payout |
| Channel partner master | Registration and RERA agent number |
| Rebate / early-payment schemes | Discounts for paying ahead of the milestone |
| Dunning ladder | The reminder letter exists; nothing sends it on a schedule |
| Customer self-service portal | The buyer's own view of their ledger |
| Co-applicant ownership shares | Needed to split TDS and to register correctly |
| Receipt cancellation / excess refund | Money in that has to go back out |

---

## Loose ends inside Phase 1

Marked "Done" above because the core flow works, but each of these has a known gap worth closing before this leaves foundation stage.

| Item | Gap |
|---|---|
| Password reset | An administrator can now reset somebody else's password from the user list (`POST /api/users/{id}/reset-password`), which always leaves the account blocked until the owner replaces it. Self-service from the forgot-password screen still has no backend endpoint behind it |
| Real OTP delivery | Verification code is fixed for local testing — no email/SMS provider is connected |
| Microsoft SSO | Button is on the sign-in screen; it isn't wired to a real OAuth flow |
| MyOperator telephony | The call dashboard and report are real, but they read the CRM's own call log. No cloud-telephony account is linked, so there are no recordings, IVR routing or live agent status. Both screens say so on the page rather than implying a connection |
| WhatsApp / email sending | Touchpoints are queued and logged on the real follow-up object, but nothing sends: no WhatsApp Business API provider and no SMTP/transactional provider are wired up. Both screens carry the same notice |
| Sentiment metric | The model reports 100% test accuracy against the seeded corpus because the demo notes are composed from a fixed set of phrases. On real free-text notes it will read lower; the figure is a property of the seed data, not of the model |
| Delete / deactivate records | Users can now be deactivated individually or in bulk, with a guard that refuses to leave a company without an active administrator. Companies and branches can still be created and edited, not removed — by design, pending a soft-delete decision |
| Real OTP delivery | Verification code is generated properly but there is still no email/SMS provider, so it goes to the log and the response says `delivered: false` |
| Charge head administration | The heads are seeded and priced correctly, and the builder picks them, but there is no screen to add or reprice one — that is a DB edit today |
| Quotation revisions | A revision now copies the priced basis faithfully, but it is a fresh draft under a new number rather than a linked version chain — the list shows both and does not thread them |

---

## ⚠️ Testing shortcuts currently switched on

> **These weaken sign-in. Both are development-only and neither survives a real deployment.**

| Shortcut | State | How it turns itself off |
|---|---|---|
| Sign-in code (OTP) | **Off** — a correct password signs in outright | `SignInPolicy` honours `Auth:SkipSecondFactor` **only** when the environment is Development. On AWS the environment is Production, the flag is ignored whatever it is set to, and the code step returns with no code change. The API logs a loud warning on every boot while it is off |
| Account passwords | Every account is `123456` | Set directly in the database on 25 Aug 2026 for local testing, with `MustChangePassword` cleared so sign-in goes straight through. Real passwords have to be re-issued before anyone outside the team touches this |

The sign-in endpoint no longer enforces a minimum password length either — that
rule belongs where a password is *chosen* (`ChangePasswordRequest`, ten
characters), not where an existing one is typed back in. A minimum on the login
endpoint publishes the policy to anyone probing it and locks out any account
whose password predates the current rule.

Serving it on the LAN (`serve-lan.ps1`, port 3000 on 192.168.1.2) with both of
the above on means anyone on the network can sign in as anyone. That is fine on
a controlled test network and nowhere else.

---

### Admin console — 25 Aug 2026

The Setup tree had 18 built screens and 14 declared-but-empty ones. Two of the
empty ones turned out to be screens over tables the system already writes, so
they were built first — real enterprise capability, no new modelling, nothing to
migrate.

| Screen | Status | What it does |
|---|---|---|
| **Login history** | 🟢 Done | Every sign-in over a chosen window, with the device and address it came from, and whether the session is still open. Reads the same `UserSessions` rows the request guard consults on every call, so a row marked live means that token really does still open the CRM — and ending it here signs the holder out within seconds. Live sessions show elapsed time rather than a clock time, because the question being asked is "is anyone on this right now" |
| **Security health check** | 🟢 Done | Eight checks computed from live rows — never-used accounts, dormant accounts, starter passwords, administrator count, stale sessions, API key age, field-level restrictions, and whether the sign-in code is switched off. Weighted score where a Risk costs three times a Warning, so one real hole outranks a handful of tidy-ups. Every finding carries what was measured, why it matters, and a link to the screen that fixes it |

| **Sharing rules** | 🟢 Done | Owner-based ("everything Tele Sales owns goes to their AGM") and criteria-based ("every lead in Varanasi goes to the Varanasi desk"). Sharing only ever widens — there is deliberately no rule that takes access away, because once both directions exist the answer depends on which rule ran last and the model stops being auditable. **Enforcement written, not just the screen:** owner rules fold into the owner filter the scope already applies; criteria rules ride alongside as an OR predicate built from the entity's own fields |
| **Login policies** | 🟢 Done | Sign-in window, allowed days, CIDR address ranges and an idle timeout, held per profile so "Tele Sales works ten to seven from the office" is one rule rather than forty copies. Window and address are checked at the door; the idle limit belongs to the session and ends a token left untouched. Platform operators are exempt throughout, so a time-of-day rule can never lock out the person who has to lift it |
| **Permission sets** | 🟢 Done | Extra access laid on top of a profile, each grant optionally carrying an expiry — which is the difference between cover for a fortnight and access nobody remembers granting. Granting or revoking ends that person's live sessions so the change takes effect on their next sign-in |
| **Business hours & holidays** | 🟢 Done | The working week per branch, with a timezone and recurring holidays. This is the foundation the SLA clock stands on: "respond within four hours" means four *working* hours, and without it a lead arriving on Friday evening is late by Saturday morning |
| **Assignment rules** | 🟢 Done | Lead routing, tried top to bottom with the first match winning — and the order is shown and reorderable, because a rule that never fires because a broader one sits above it is the commonest way routing goes wrong. Round robin (cursor stored on the row, so it survives a restart), least-loaded over open leads, or one named person. A lead matching nothing is left unassigned rather than handed to somebody arbitrary |
| **Duplicate rules** | 🟢 Done | Exact field matching rather than a similarity score — a fuzzy matcher demos well and then flags two brothers at one address, and a rule that cries wolf gets switched off within the week. Phones compare on their last ten digits, so `+91 98765 43210` and `9876543210` are one person. Warn or refuse the save |
| **Escalation & SLA rules** | 🟢 Done | Targets measured in working minutes against the business hours, with the action raising a follow-up task for the owner or their manager, marking the lead hot, or reassigning it. Each rule fires once per record — the event table carries a unique index on rule-plus-record, because an escalation that repeats on every sweep is one everyone mutes |
| **Approval processes** | 🟢 Done | Ordered steps rather than a branching flow, because an approval that can branch is one nobody can answer "who has it now" about. A step names the submitter's manager, a role, or a person |
| **Scheduled jobs** | 🟢 Done | Cron schedules over a fixed registry of jobs the system knows how to run — a scheduler that can run anything is a remote execution endpoint with a friendly name. The outcome, duration and next run are written onto the row, so "did it run and did it work" is answerable without reading a log |
| **Import** | 🟢 Done | CSV via papaparse, with headers auto-mapped where the names line up and everything else mapped by hand. Rows are inserted one at a time: a 400-row file with two bad numbers imports 398 and names the two, rather than refusing the lot. Optionally routes through the assignment rules and skips duplicates |
| **Export** | 🟢 Done | Leads as UTF-8 CSV with a BOM so Excel does not mangle accented names. Scoped to what the person running it can already see — an export is the easiest way to walk out of a business with its book |
| **Mass transfer** | 🟢 Done | Reassigns ownership in bulk, always previewed first. A transfer is not reversible in any useful sense, so the screen reports the count and a sample before the button that moves them is enabled |

**What the survey turned up**

`SharingRule` and `LoginPolicy` were modelled and migrated but **nothing read
them** — no service, filter or controller referenced either table. A screen over
them would have let an administrator write rules that did nothing at all, so
enforcement was written first and the screens sit on top of it. `PermissionSet`
turned out to be the opposite case: the resolver already honours it through
`p.Assignments`, so that one genuinely needed only a screen.

**Bugs the work surfaced and closed**

| Bug | Why it mattered |
|---|---|
| Criteria sharing could never have matched | The shared list path cast to `IQueryable<IOwnedRecord>` before scoping, so the concrete entity type was gone by the time a rule looked for its fields — reflection would have found only `OwnerId`, and every rule naming a real column would have silently matched nothing. The scope now takes the entity type through unchanged |
| A new escalation rule fired on the whole back catalogue | The first sweep raised **14,910 follow-up tasks in one pass** against leads going back months. That is not an alert, it is a denial of service against the follow-up list, and the desk would simply stop opening it. A rule now only considers records that arrived after it did |
| Duplicate detection could not run at all | `EndsWith` on a phone column threw `nondeterministic collations are not supported for substring searches` — the third time this database's case-insensitive collation has bitten a pattern match. Fixed the same way as the others: collate the column to `"C"` before the comparison |
| Nine create/update paths returned null | `(await ListAction(ct)).Value` is null every time, because `Ok()` puts the payload in `.Result`. Each list is now a private method returning the list, with a thin action wrapping it |

---

## Local environment

> **Development only — not a deployment target**

| | |
|---|---|
| Frontend | `localhost:3000` |
| API | `localhost:5080` |
| MySQL database | `bull_realty_crm` @ `:3306` |
| Seeded company admin | `priya.sharma@bullrealtyglobal.com` / `<the configured seed password>456` — the seed ships `<the configured seed password>`, but every account is created with `MustChangePassword`, so the first sign-in replaces it. This is what it was replaced with on 25 Aug 2026 |
| Seeded platform admin | `superadmin@bullrealtyglobal.com` (password from `Seed:AdminPassword`) — lands on the company picker |

---

## Immediate next steps

1. Let the calendar create and reschedule, not only read — drag a block to move a visit, and open the booking dialogs straight from an empty slot. The write paths and the clash check (`SchedulingController.EnsureFreeAsync`) already exist; the grid just does not call them yet.
2. Move saved dashboard layouts off `localStorage` and onto the API, so a dashboard follows the user between machines and can be shared with a team rather than living in one browser.
3. Connect MyOperator, then a WhatsApp Business API provider — the objects, queues and dashboards are built and waiting for credentials.
4. Close the Phase 1 auth gaps: password reset and real OTP delivery matter most before more people touch the system.
5. Improve the lead conversion model — it tests at AUC 0.52, barely better than chance, because the seeded lead history carries almost no learnable signal. The opportunity win model (AUC 0.63) shows what the same pipeline does with honest features.
6. Give the duplicate detector a real entry point on the leads toolbar; the backend endpoint exists and works.
7. Stand up a CI pipeline so future changes aren't verified by hand.

---

*Bull Realty Global — CRM & ERP · Register updated 25 Aug 2026*
