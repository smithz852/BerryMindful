# BerryMindful — Implementation Plan

## Context
BerryMindful is a grocery tracking web app whose primary goal is reducing food waste. The core insight: most food spoils because people forget what they bought and when. The app solves this by making item entry nearly effortless via receipt scanning, then proactively alerting users as items approach their expiry window.

## Status (as of 2026-07-06)
Phase 1 is **complete and fully live-verified** (2026-07-07): all three API keys set, Vision + Claude pipeline tested on real receipts (incl. purchase-date extraction), and the Resend expiry digest delivered + deduped for real. Next up: the forgot/reset-password flow (branch `liveTestingAndPwFlow`), then the rest of early Phase 2. Reminder: Resend's sandbox sender (`onboarding@resend.dev`) only delivers to the Resend account owner's address — verify a domain before emailing other users.

Dev quickstart:
- API: `dotnet run --launch-profile https` from `server/BerryMindful.Api` → https://localhost:7068 (MySQL runs as the local `MySQL83` Windows service; connection string is in user-secrets)
- Client: `npm run dev` from `client/` → http://localhost:5173
- Test accounts: `zach.test@example.com` / `password123`, and `zachsmith852@gmail.com` / `password123` (receives real Resend email — it's the Resend account owner's address)
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

Notes vs. the original sketch: rate limiting lives inline in Program.cs (no `Middleware/` folder needed yet — a rate-limit-key middleware comes with the per-email forgot-password limits), `TokenService/` arrives with the deferred password-reset endpoints, and there's no docker-compose — dev MySQL is the local Windows service.

### Program.cs / middleware pipeline (mirrors RSS)
- Pipeline order: HTTPS redirect → CORS (allow `localhost:5173`) → JWT auth → rate limiter → Authorization → controllers.
- Rate limits on auth paths (10 req/min by IP); tighter per-email limits on forgot-password / reset-password (3 req/hour) come with those endpoints (deferred), keyed via rate-limit-key extraction middleware.
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
- `POST /auth/forgot-password` [rate-limited] — Identity reset token, emailed via TokenService *(deferred to early Phase 2; `IEmailService` is already wired)*
- `POST /auth/reset-password` [rate-limited] — `UserManager.ResetPasswordAsync` (rotates security stamp → invalidates outstanding tokens) *(deferred to early Phase 2)*
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
- **Password reset** tokens via `UserManager.GeneratePasswordResetTokenAsync`, delivered by `TokenService` through the email provider.
- No external auth vendor needed for MVP.

---

## Notifications (MVP: Email)
`ExpiryNotificationWorker` — a hosted background service in `BerryMindful.Api/Workers/`, following the RSS `BaseSportsAutomation` pattern (abstract daily-scheduled base class is overkill for one worker, but same shape: registered in Program.cs DI, runs daily at a configured local hour). It:
1. Queries `PantryItems` where `Status = Active` and `ExpiresAt` is within 2 days
2. Skips items already logged in `NotificationLogs` for that window
3. Sends a summary digest email via `IEmailService` — the shared **ZlEmailProvider** library (Resend), referenced as a sibling-repo project reference (eventually a private package). Enable delivery with `dotnet user-secrets set "Resend:ApiKey" "re_..."`; without the key, emails log to console. From/name and digest hour configured in appsettings (`Email`, `Notifications` sections).

Email is simpler than Web Push for MVP — no service worker, no browser permission UX to build. Web Push is Phase 2.

---

## Phasing

### Phase 1 — MVP (Core Loop)
- [x] Auth (signup, login, JWT — Identity-backed; forgot/reset password slipped to early Phase 2)
- [x] Receipt scan endpoint + Cloud Vision + Claude pipeline — live-tested with real keys on a Costco receipt (2026-07-07)
- [x] Confirm + save pantry items
- [x] Pantry dashboard (list, color-coded expiry, mark used/tossed)
- [x] Manual item entry fallback
- [x] Daily email notification digest — via shared ZlEmailProvider lib (Resend); live delivery + dedupe verified 2026-07-07

### Phase 2 — Growth Features
- Store detection from receipt header → store-tier shelf-life adjustments
- Web Push notifications (service worker)
- Waste analytics dashboard (items tossed vs used over time)
- Household sharing (invite by email, shared pantry view)
- Barcode scanning (Open Food Facts API)
- Category filters + search
- Per-item custom shelf-life override

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
