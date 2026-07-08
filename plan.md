# BerryMindful — Implementation Plan

## Context
BerryMindful is a grocery tracking web app whose primary goal is reducing food waste. The core insight: most food spoils because people forget what they bought and when. The app solves this by making item entry nearly effortless via receipt scanning, then proactively alerting users as items approach their expiry window.

## Status (as of 2026-07-06)
Phase 1 is **complete and fully live-verified** (2026-07-07): all three API keys set, Vision + Claude pipeline tested on real receipts (incl. purchase-date extraction), and the Resend expiry digest delivered + deduped for real. The forgot/reset-password flow is also implemented and live-verified end to end (2026-07-07, real emailed token → client reset page → login; note `zachsmith852@gmail.com` no longer uses the shared test password). The waste analytics dashboard (Phase 2) is built and live-verified (2026-07-07): new `PantryItems.StatusChangedAt` column + `/analytics/waste` endpoint + `/analytics` client page (Recharts). Next up (per the 2026-07-07 assessment): the user staples list for recipe relevance (own branch — see the action item in the Recipe Recommendations section) and NotificationsController prefs endpoints, then the rest of the Phase 2 queue. Reminder: Resend's sandbox sender (`onboarding@resend.dev`) only delivers to the Resend account owner's address — verify a domain before emailing other users.

Dev quickstart:
- API: `dotnet run --launch-profile https` from `server/BerryMindful.Api` → https://localhost:7068 (MySQL runs as the local `MySQL83` Windows service; connection string is in user-secrets)
- Client: `npm run dev` from `client/` → http://localhost:5173
- Test accounts: `zach.test@example.com` / `password123`, and `zachsmith852@gmail.com` (password set by Zach via the reset flow; receives real Resend email — it's the Resend account owner's address)
- Startup logs state whether the real scanner ("Vision + Claude pipeline active") and Resend delivery are enabled or falling back.

---

## Tech Stack
| Layer | Choice |
|---|---|
| Frontend | React (Vite) + TanStack Query (no store library — see Frontend Architecture) |
| Backend | ASP.NET Core 10 Web API (.NET 10 LTS, supported to Nov 2028; controller-based, mirroring the RSS architecture) |
| ORM | EF Core 9 + Pomelo 9 MySQL provider (Pomelo 9 runs on .NET 10; bump to EF Core 10 when Pomelo 10 ships) |
| Database | MySQL 8 (local `MySQL83` Windows service in dev) |
| OCR | Google Cloud Vision API |
| Item parsing | Claude API (`claude-haiku-4-5`) via official Anthropic C# SDK, structured outputs for guaranteed-valid JSON |
| Auth | ASP.NET Core Identity (built-in `IdentityDbContext` / `UserManager`) + JWT (access + refresh tokens) |
| Notifications | Email digest via shared **ZlEmailProvider** library (Resend) (MVP) → Web Push API (Phase 2) |
| Hosting | Self-hosted VPS (Linux, nginx reverse proxy) |

---

## Project Structure

Backend mirrors the RetroSportsSquares 3-project solution layout (API / data / services), using conventional dotted .NET naming. API project owns controllers/middleware/hosted workers, Data project owns entities + DbContext + migrations, Services project owns business logic + DTOs. No separate "Core" project — domain entities live in `BerryMindful.Data`, interfaces and DTOs live in `BerryMindful.Services` (dependency direction: `Api` → `Services` → `Data`). Frontend stays a plain `client/` folder.

```
berrymindful/
├── client/                       # React (Vite) app
│   ├── src/
│   │   ├── api/                  # Typed fetch wrapper (client.ts)
│   │   ├── pages/                # Route-level pages (Auth, Pantry, Scan, Confirm, AddItem)
│   │   ├── hooks/                # useAuth, usePantry, useReceipts (no store library)
│   │   ├── types/                # Shared TypeScript types
│   │   └── utils/                # downscaleImage (client-side resize before upload)
├── server/                       # BerryMindful.sln
│   ├── BerryMindful.Api/         # API project (entry point)
│   │   ├── Program.cs            # DI, middleware pipeline, rate limiter, JWT setup,
│   │   │                         #   key-based scanner/email fallback selection
│   │   ├── Controllers/          # AuthController, ReceiptsController, PantryController
│   │   ├── Workers/              # ExpiryNotificationWorker (hosted service)
│   │   └── uploads/              # Receipt images (gitignored; object storage later)
│   ├── BerryMindful.Data/        # Data access layer
│   │   ├── AppDbContext.cs       # extends IdentityDbContext<ApplicationUser>
│   │   ├── AppDbContextFactory.cs  # design-time factory for migrations
│   │   ├── Entities/             # ApplicationUser, Receipt, PantryItem,
│   │   │                         #   NotificationLog, RefreshToken
│   │   └── Migrations/
│   └── BerryMindful.Services/    # Business logic
│       ├── ReceiptServices/      # IReceiptScanner seam: VisionClaudeReceiptScanner
│       │                         #   (GoogleVisionOcrService → ClaudeReceiptParser)
│       │                         #   + StubReceiptScanner fallback; ReceiptService
│       ├── PantryServices/
│       ├── NotificationServices/ # ExpiryNotificationService (digest compose + dedupe),
│       │                         #   LoggingEmailService (no-key fallback)
│       └── DTOs/                 # ReceiptScanResultDto, PantryItemDraftDto, etc.
└── plan.md

../ZlEmailProvider/               # Sibling repo: shared Resend email transport
                                  #   (IEmailService.SendAsync) — project reference for
                                  #   now, private package eventually
```

Notes vs. the original sketch: rate limiting lives inline in Program.cs, with `Middleware/RateLimitKeyMiddleware.cs` extracting the target email for the per-email password-endpoint limits; no separate `TokenService` materialized — the password-reset endpoints live in AuthController using `UserManager` + `IEmailService` directly; and there's no docker-compose — dev MySQL is the local Windows service.

### Program.cs / middleware pipeline (mirrors RSS)
- Pipeline order: HTTPS redirect → CORS (allow `localhost:5173`) → JWT auth → rate limiter → Authorization → controllers.
- Rate limits on auth paths (10 req/min by IP); forgot-password / reset-password use a tighter per-email policy (3 req/hour) keyed via `RateLimitKeyMiddleware`, which buffers the JSON body and stashes the normalized email before the rate limiter runs.
- Per-user rate limit on `POST /receipts/scan` (20/hour) — the only endpoint that incurs Vision + Claude API costs, so this is the cost-control valve.
- DI registrations: all BerryMindful.Services classes, `IEmailService` (ZlEmailProvider's `ResendEmailService`, or `LoggingEmailService` without a key), MySQL DbContext (Pomelo) + ASP.NET Identity, `ExpiryNotificationWorker`. Scanner and email implementations are selected at startup based on which secrets are present.
- Secrets (JWT signing key, `Anthropic:ApiKey`, `GoogleVision:CredentialsPath`, `Resend:ApiKey`) via `dotnet user-secrets` in dev, environment variables on the VPS — never in appsettings.json.

---

## Data Model (MySQL)

`AppDbContext` extends `IdentityDbContext<ApplicationUser>`, so the full set of Identity tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserTokens`, etc.) is generated by the first migration — no hand-rolled users table or password hashing.

### `ApplicationUser` (extends `IdentityUser` — Email, PasswordHash, SecurityStamp etc. come built in)
```
+ CreatedAt, NotificationEmail (nullable), NotificationsEnabled (bool, default true)
```

### `Receipts`
```
Id (PK, UUID), UserId (FK → AspNetUsers, string), StoreNameRaw, PurchasedAt,
ImageUrl, RawOcrText, CreatedAt
```

### `PantryItems`
```
Id (PK, UUID), UserId (FK → AspNetUsers, string), ReceiptId (FK nullable), Name, Category,
PurchasedAt, EstimatedExpiryDays, ExpiresAt (computed on insert),
Status (enum: Active | Used | Tossed), CreatedAt, UpdatedAt
```

### `NotificationLogs`
```
Id (PK, UUID), UserId (FK → AspNetUsers, string), PantryItemId (FK), SentAt,
Type (enum: Warning | Expired)
```

Note: Identity PKs are `string` (GUID) by default — all `UserId` FKs are strings referencing `AspNetUsers.Id`, same as RSS's `GamePlayer` → `ApplicationUser` pattern.

---

## API Endpoints (controller-based, RSS-style)

### `AuthController` [`/auth`]
- `POST /auth/signup` — email + password → `UserManager.CreateAsync` → JWT
- `POST /auth/login` — `SignInManager.CheckPasswordSignInAsync` → access token + refresh token
- `POST /auth/refresh` → new access token
- `GET /auth/me` [Authorize] — current user profile
- `POST /auth/forgot-password` [rate-limited 3/hour per email] — Identity reset token, emailed via `IEmailService` (BerryMindful-branded copy composed in the controller — ZlEmailProvider's `SendPasswordResetAsync` has RSS-branded copy hardcoded); always 204 so responses don't reveal account existence
- `POST /auth/reset-password` [rate-limited 3/hour per email] — `UserManager.ResetPasswordAsync` (rotates security stamp → invalidates outstanding tokens) + revokes refresh tokens; generic "invalid link" error for unknown email or bad token
- Implemented but not in the original sketch: `POST /auth/refresh` reads the HttpOnly refresh cookie and rotates it; `POST /auth/logout` revokes the refresh token and rotates the security stamp.

### `ReceiptsController` [`/receipts`, all Authorize]
- `POST /receipts/scan` — multipart image upload → triggers Vision + Claude pipeline → returns parsed items for user confirmation
- `POST /receipts/confirm` — user confirms/edits parsed items → saved to `PantryItems`
- `GET /receipts` — list past receipts

### `PantryController` [`/pantry`, all Authorize]
- `GET /pantry` — all active items, sorted by expiry
- `PATCH /pantry/{id}/status` — mark item as Used or Tossed
- `POST /pantry/items` — manual item entry (fallback)
- `DELETE /pantry/{id}`

### `NotificationsController` [`/notifications`, all Authorize] *(not built yet — the `NotificationsEnabled` / `NotificationEmail` columns exist and the digest respects them; this controller just exposes the toggle)*
- `GET /notifications/prefs` / `PUT /notifications/prefs` — manage email notification opt-in

---

## Receipt Processing Pipeline

```
1. Client downscales image (~1500px, JPEG) and uploads to POST /receipts/scan
2. .NET saves image to local storage (or object storage bucket later)
3. BerryMindful.Services `ReceiptServices`:
   a. Sends image bytes to Google Cloud Vision (TEXT_DETECTION)
   b. Receives raw OCR text block
   c. Builds a structured Claude prompt (see below)
   d. Calls Claude API → parses JSON response
   e. Returns a ReceiptScanResult DTO (unconfirmed items) to client
4. Client shows editable item list — user can rename, remove, or adjust dates
5. POST /receipts/confirm persists Receipt + PantryItems rows
```

### Claude Integration
Use the official Anthropic C# SDK (`dotnet add package Anthropic`) with **structured outputs** — pass a JSON schema via `OutputConfig.Format = new JsonOutputFormat { Schema = ... }` on `client.Messages.Create(...)`. The API then guarantees the response is valid JSON matching the schema, which eliminates the malformed-JSON fallback path entirely.

Output schema (enforced by the API, not the prompt):
```json
{
  "type": "object",
  "properties": {
    "purchaseDate": { "type": ["string", "null"], "description": "Transaction date printed on the receipt as YYYY-MM-DD, or null if none is visible" },
    "items": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "category": { "type": "string", "enum": ["produce", "dairy", "meat", "seafood", "bakery", "frozen", "pantry", "beverage", "other"] },
          "estimatedExpiryDays": { "type": "integer" }
        },
        "required": ["name", "category", "estimatedExpiryDays"],
        "additionalProperties": false
      }
    }
  },
  "required": ["purchaseDate", "items"],
  "additionalProperties": false
}
```

The scanner uses `purchaseDate` when it's plausible (not in the future, not over a year old); otherwise it falls back to today. Verified 2026-07-07: Claude reads the printed transaction date, even preferring it over a handwritten correction.

### Claude Prompt Design
```
System: You are a grocery item identifier. Given raw receipt OCR text, extract the
purchase date and each food item.
- purchaseDate: the transaction date printed on the receipt as YYYY-MM-DD, or null
  if no date is visible.
For each item, provide:
- name: human-readable food name
- category: the closest matching category
- estimatedExpiryDays: integer, typical days until spoilage from purchase date (assume 
  proper refrigeration/storage). Use conservative estimates.

Common abbreviations: ORG/ORGC = Organic, BNNA = Banana, STRBRY = Strawberry,
MLK = Milk, CHKN = Chicken, WHL = Whole, LF = Low Fat, 3PK/2PK = multipack (ignore count),
SML/LRG = size descriptor (ignore), DELI = deli department, BF = Beef, GND = Ground,
YGT = Yogurt, CHDR = Cheddar, BROC = Broccoli, AVCD = Avocado, TMAT = Tomato,
CSR = Caesar (CSR KIT = Caesar salad kit: produce, short shelf life),
KS = Kirkland Signature (brand, not a food word).

Skip non-food lines: tax, subtotal, total, store name, cashier, loyalty points, coupon.

Few-shot examples:

Input: "ORG BNNA 3PK  1.49"
Output: {"purchaseDate":null,"items":[{"name":"Organic Bananas","category":"produce","estimatedExpiryDays":5}]}

Input: "GND BF 93/7 LB  5.99\nWHL MLK GAL  3.49\nORG STRBRY PINT  4.99\n06/29/2026 19:06"
Output: {"purchaseDate":"2026-06-29","items":[{"name":"Ground Beef 93/7","category":"meat","estimatedExpiryDays":2},{"name":"Whole Milk","category":"dairy","estimatedExpiryDays":10},{"name":"Organic Strawberries","category":"produce","estimatedExpiryDays":4}]}

User: <raw OCR text here>
```

### Response Parsing
- Response JSON is schema-guaranteed — deserialize directly to `PantryItemDraft[]`
- Remaining failure modes are API-level only (timeout, rate limit, empty receipt): catch, log, and return a `scanError` flag so the client can offer the manual-entry fallback

---

## Frontend Architecture

### Pages
| Route | Purpose |
|---|---|
| `/` | Landing / auth gate |
| `/login` `/register` | Auth forms |
| `/pantry` | Main dashboard — item cards sorted by expiry |
| `/scan` | Receipt camera/upload flow |
| `/scan/confirm` | Review + edit parsed items before saving |
| `/pantry/add` | Manual item entry form |

### State Management
- **TanStack Query** — for all server state (pantry, receipts). Handles caching and background refetch.
- **Auth** — token in localStorage + a small `useAuth` hook (RSS pattern). No store library: with TanStack Query owning server state, the only global client state is auth. Add Zustand later only if real client-only state accumulates.

### Receipt Upload UX
- `<input type="file" accept="image/*" capture="environment">` — opens camera on mobile, file picker on desktop
- **Downscale client-side before upload** — canvas resize to ~1500px on the long edge, JPEG ~80% quality. Phone cameras produce 12MP+ images; OCR doesn't need them, and this cuts upload time, Vision latency, and cost.
- Show loading state during OCR pipeline (~3–6s expected latency)
- Editable confirm screen before committing

### Pantry Dashboard
- Cards with color-coded urgency: green (>5 days), yellow (2–5 days), red (<2 days / expired)
- "Mark used" / "Mark tossed" actions inline
- Filter by category

---

## Auth
Built entirely on ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`) — same setup as RSS:
- `ApplicationUser : IdentityUser`, `AppDbContext : IdentityDbContext<ApplicationUser>` — Identity owns password hashing, lockout, security stamps, and token providers. No custom credential code.
- **JWT** (HMAC-SHA256): issuer/audience/key from config; claims include `NameIdentifier`, `Email`, `security_stamp`. Access tokens 15min TTL + refresh tokens (7 days, HttpOnly cookie).
- **Security-stamp validation** (carried over from RSS): `OnTokenValidated` compares the JWT's `security_stamp` claim against the current stamp (IMemoryCache, 30s TTL) — password resets and explicit logout invalidate tokens across all devices immediately, without waiting for expiry.
- **Password reset** tokens via `UserManager.GeneratePasswordResetTokenAsync` (1-hour lifespan via `DataProtectionTokenProviderOptions`), emailed as a `/reset-password?token=…&email=…` link to the client; reset also revokes all refresh tokens.
- No external auth vendor needed for MVP.

---

## Notifications (MVP: Email)
`ExpiryNotificationWorker` — a hosted background service in `BerryMindful.Api/Workers/`, following the RSS `BaseSportsAutomation` pattern (abstract daily-scheduled base class is overkill for one worker, but same shape: registered in Program.cs DI, runs daily at a configured local hour). It:
1. Queries `PantryItems` where `Status = Active` and `ExpiresAt` is within 2 days
2. Skips items already logged in `NotificationLogs` for that window
3. Sends a summary digest email via `IEmailService` — the shared **ZlEmailProvider** library (Resend), referenced as a sibling-repo project reference (eventually a private package). Enable delivery with `dotnet user-secrets set "Resend:ApiKey" "re_..."`; without the key, emails log to console. From/name and digest hour configured in appsettings (`Email`, `Notifications` sections).

Email is simpler than Web Push for MVP — no service worker, no browser permission UX to build. Web Push is Phase 2.

---

## Recipe Recommendations (Spoonacular) — built + live-verified 2026-07-07 (real API key)

Implemented on the `recipeRecommendations` branch as specced below and live-verified end to end with the real Spoonacular key: startup log "Spoonacular provider active", real search from pantry ingredients (receipt-noise names normalized), real images in the 4-column grid, cache hit on repeat search (12ms, no quota spent), dead image URLs fall back to a placeholder. Gotcha from setup: the secret name is `Spoonacular:ApiKey` — it was initially saved as "Spoontacular" (extra t) and silently fell back to the stub; the startup log line is how you catch that.

Users find recipes that use up what's already in their pantry — directly serving the food-waste mission (cook it before it expires).

### API choice & key handling
- Spoonacular `GET /recipes/findByIngredients` with params: `ingredients` (comma-separated), `number` (1–100, default 10), `ranking` (1 = maximize used ingredients, 2 = minimize missing ingredients), `ignorePantry` (bool — skip common pantry staples like water/salt/flour).
- **All Spoonacular calls go through the .NET API, never the browser.** The key would be visible in the client bundle otherwise, and the free tier is only ~150 points/day (`findByIngredients` ≈ 1 point + 0.01/result), so the server is the cost-control point — same reasoning as the `/receipts/scan` rate limit.
- Secret: `Spoonacular:ApiKey` via `dotnet user-secrets` in dev, env var on the VPS.

### Backend (`BerryMindful.Services/RecipeServices/` — mirrors the `IReceiptScanner` seam)
- `IRecipeProvider` — `FindByIngredientsAsync(ingredients, ranking, ignorePantry, number, ct)`.
- `SpoonacularRecipeService` — typed `HttpClient` (registered via `AddHttpClient`), builds the query string, maps the JSON response to DTOs. Non-200 / timeout → caught and surfaced as a clean 502-style error DTO, not an unhandled 500.
- `StubRecipeService` — no-key fallback returning canned recipes (matches the scanner/email startup-selection pattern; startup log line states which is active).
- `RecipeSuggestionDto`: `Id, Title, ImageUrl, UsedIngredientCount, MissedIngredientCount, UsedIngredients[], MissedIngredients[], Likes` (Spoonacular's response shape, camel-cased through).
- `RecipesController` [`/recipes`, Authorize]:
  - `GET /recipes/by-ingredients?ingredients=a,b,c&ranking=1&ignorePantry=true&number=12` — validates ranking ∈ {1,2}, clamps number to 1–24 (no reason to allow 100), 400 on empty ingredients.
- **Cost controls**: per-user rate limit (e.g. 30/hour, same mechanism as scan) + `IMemoryCache` keyed on the normalized request (lowercased, sorted ingredient list + ranking + ignorePantry + number), TTL ~24h — re-running the same search costs zero quota.
- Ingredient normalization server-side before hitting Spoonacular: lowercase, trim, dedupe, strip size/brand noise tokens (e.g. "93/7", "3pk"). Pantry names come from receipts, so "Ground Beef 93/7" should be sent as "ground beef". Simple heuristics for MVP; a stored Claude-normalized ingredient name at scan time is a later upgrade.

### Client
- New protected route `/recipes` (lazy-loaded like Analytics), "Find Recipes" button in the Pantry page header nav.
- `types/recipes.ts` + `hooks/useRecipes.ts` — `useQuery` keyed `["recipes", ingredients, ranking, ignorePantry]`, `enabled` only after the user applies filters (no fetch on page load), generous `staleTime` mirroring the server cache.
- `RecipesPage`:
  - **Filter button → modal**: checkbox list of distinct Active pantry item names (all checked by default, sorted soonest-expiry first with an "expiring soon" badge — nudges users toward at-risk items), two-choice radio (Maximize used ingredients / Minimize missing ingredients → ranking 1/2), "Ignore common pantry staples" checkbox (default true), Apply button.
  - **Results**: responsive card grid (`display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr))` — same responsive behavior as flex-wrap but with even rows), each card: image, title, used/missed ingredient count badges (green/amber), missed-ingredient names, likes.
  - States: initial "select ingredients to search" empty state, loading skeleton, error with retry, no-results message.
  - Last filter selections persisted to localStorage so the modal reopens with prior choices.

### Action item — user staples list (next recipe branch, e.g. `recipeStaples`)
Problem (assessed 2026-07-07): the pantry only contains receipt-scanned items, so ingredients most households always have (onion, garlic, butter, eggs, oil) show up as "missing" in every search. ranking=2 then biases toward trivially simple recipes, and ranking=1 shows intimidating missed-ingredient counts. Spoonacular's `ignorePantry` only covers its own narrow staples list (water/salt/flour) — it doesn't fix this. It's a relevance problem with the search *input*, not a reason to switch APIs.

Plan:
- **Storage**: `UserStaples` table (UserId FK + ingredient name), seeded from a default checklist (onion, garlic, butter, eggs, cooking oil, flour, sugar, salt/pepper/common spices) on first use.
- **UI**: manage staples from the Recipes filter modal (or a small settings panel) — add/remove, defaults pre-checked.
- **Server**: `RecipesController` merges staples into the ingredient list (normalized + deduped like pantry names) before calling Spoonacular; the cache key already hashes the final ingredient list so no cache changes needed.
- **Results UX**: distinguish "missing, but it's a common staple" from "genuinely missing" on cards; frame genuinely-missing ingredients as "add to shopping list" hooks rather than failures.
- Cost note: Spoonacular free tier (~150 pts/day, ~1.12 pts per 12-result search) is fine at current scale given the 24h cache + rate limit. If it ever becomes a real cost, the escape hatch is a Claude-generated `IRecipeProvider` (Haiku, pennies, naturally assumes staples, prioritizes expiring items — no photos/source links) — a swap behind the existing seam, not a rebuild. Not now.

### Later improvements (not MVP)
- "View full recipe": `findByIngredients` returns no instructions/source URL. MVP deep-links to `https://spoonacular.com/recipes/{title-slug}-{id}`; proper version proxies `GET /recipes/{id}/information` (1 extra point) for `sourceUrl` + instructions.
- "I cooked this" → bulk-mark the used ingredients as Used — closes the loop with the waste analytics dashboard. (Also listed in the Phase 2 future-ideas queue.)
- Check Spoonacular ToS attribution requirements (image credit / backlink) before public deploy.

### Verification
1. No key → stub recipes returned, startup log says stub is active
2. Real key → search from real pantry ingredients returns plausible recipes
3. Ranking toggle demonstrably changes result ordering; ignorePantry changes results
4. Identical repeat search served from cache (log line, no Spoonacular call); rate limit returns 429 past the cap
5. Grid reflows correctly at mobile width; modal usable on mobile

---

## Phasing

### Phase 1 — MVP (Core Loop)
- [x] Auth (signup, login, JWT — Identity-backed; forgot/reset password implemented 2026-07-07 with per-email rate limiting + client pages)
- [x] Receipt scan endpoint + Cloud Vision + Claude pipeline — live-tested with real keys on a Costco receipt (2026-07-07)
- [x] Confirm + save pantry items
- [x] Pantry dashboard (list, color-coded expiry, mark used/tossed)
- [x] Manual item entry fallback
- [x] Daily email notification digest — via shared ZlEmailProvider lib (Resend); live delivery + dedupe verified 2026-07-07

### Phase 2 — Growth Features
- [x] Waste analytics dashboard (items tossed vs used over time) — built + live-verified 2026-07-07: `StatusChangedAt` column (backfilled from `UpdatedAt` for pre-existing rows), `GET /analytics/waste?days=30|90|0` (`WasteAnalyticsService`, in-memory weekly/category/most-tossed aggregation), client `/analytics` page (Recharts, lazy-loaded chunk; KPI tiles, weekly stacked bars, category bars, top-5 table). Note: deleted items are hard-deleted and vanish from analytics history (known limitation).
- [x] Recipe recommendations from pantry ingredients (Spoonacular `findByIngredients` — see "Recipe Recommendations" section above; built + live-verified with the real key 2026-07-07 on `recipeRecommendations` branch)

**Queued (prioritized 2026-07-07 assessment):**
- [ ] User staples list for recipe relevance — own branch; see "Action item — user staples list" in the Recipe Recommendations section
- [ ] NotificationsController prefs endpoints (already flagged as next in Status)
- [ ] "I cooked this" → bulk-mark used ingredients as Used — closes the recipe → pantry → waste-analytics loop
- [ ] Category filters + search on the pantry page
- [ ] Per-item custom shelf-life override

**Deferred — thinking about, not scheduled:** household sharing (invite by email, shared pantry view), barcode scanning (Open Food Facts API), Web Push notifications (service worker), store detection from receipt header → store-tier shelf-life adjustments.

---

## Dev Environment Setup (Recommended First Steps)

*Steps 1–8 are done; step 9 is code-complete with the live key test pending.*

1. ~~docker-compose MySQL~~ → using the machine's existing `MySQL83` Windows service instead (no Docker installed); database `berrymindful`, connection string in user-secrets (`ConnectionStrings:Default`)
2. Scaffold `BerryMindful.sln` on .NET 10: `dotnet new sln`, add `BerryMindful.Api` (webapi), `BerryMindful.Data` (classlib), `BerryMindful.Services` (classlib); references `Api` → `Services` → `Data`
3. `BerryMindful.Data`: add `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (9.x) + `Pomelo.EntityFrameworkCore.MySql` (9.x), create `ApplicationUser` + `AppDbContext : IdentityDbContext<ApplicationUser>` + `AppDbContextFactory`
4. Run first EF migration: `dotnet ef migrations add InitialCreate` (creates Identity tables + domain tables)
5. `BerryMindful.Api`: `dotnet user-secrets init` and store the JWT signing key (Claude/Vision/SMTP keys join later); wire Identity + JWT in Program.cs, build `AuthController` — get `/auth/signup` and `/auth/login` working
6. Vite React app: `npm create vite@latest client -- --template react-ts`
7. Wire TanStack Query + `useAuth` hook, build login page
8. Stub `POST /receipts/scan` returning hardcoded JSON → build the confirm + save flow end to end before wiring real OCR
9. Integrate Cloud Vision + the Anthropic C# SDK (structured outputs) once the data pipeline shape is validated; add Claude/Vision keys to user-secrets. ✅ Pipeline built (`GoogleVisionOcrService` → `ClaudeReceiptParser` → `VisionClaudeReceiptScanner`); the API auto-selects it over the stub when both keys are present. To enable, run from `server/BerryMindful.Api`:
   ```
   dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
   dotnet user-secrets set "GoogleVision:CredentialsPath" "C:\path\to\vision-service-account.json"
   ```
   (Google Vision uses a service-account JSON downloaded from Google Cloud Console; `GOOGLE_APPLICATION_CREDENTIALS` env var also works.)

---

## Verification (End-to-End Test)
1. ✅ Register a user → JWT returned (plus refresh rotation, logout security-stamp invalidation verified)
2. ✅ Upload a real grocery receipt image → confirm OCR text appears in logs, Claude returns valid JSON *(verified 2026-07-07 with a real Costco receipt: OCR + 4 items parsed, deposit/tax lines skipped; note — GCP project needs billing enabled for Vision)*
3. ✅ Confirm items → appear in pantry dashboard with correct expiry dates
4. ✅ Mark one item as "Used" → status updates, item leaves active list
5. ✅ Add an item expiring tomorrow → digest composed and deduped correctly *(verified 2026-07-07 with real Resend delivery to zachsmith852@gmail.com: 2 Expired + 1 Warning items in one digest, Resend 200, rerun sent 0 via NotificationLogs dedupe; trigger dev runs with `Notifications__RunOnStartup=true`)*
6. ✅ Forgot/reset password → real emailed link → client reset page → login with new password; enumeration-safe 204s, 3/hour per-email limit (4th request 429), bad token rejected, all sessions revoked on reset *(verified 2026-07-07)*
