# RetroSportsSquares — Architecture Map

> Snapshot copied from the RetroSportsSquares project notes on 2026-07-06 (originally written ~2026-07-02). Point-in-time reference — verify details against the RetroSportsSquares repo before relying on them. `[[double-bracket]]` references point to notes in that project, not files here.

Solution: `RSS.sln`. Backend = ASP.NET Core Web API (.NET 8) split into `RSS` (API), `RSS-DB` (EF Core data layer), `RSS-Services` (business logic). Frontend = `RetroSportsSquaresWeb/` (React + Vite + TS, retro arcade-themed sports squares betting game), originally scaffolded via Replit.

---

## 1. Backend — RSS (API project, `RSS-API.csproj`)

### Entry point: `RSS/Program.cs`
- Middleware pipeline (in order): HTTPS redirect → CORS (allow `localhost:5173`) → JWT auth (+ SignalR query-string token support) → `EmailExtractionMiddleware` → rate limiter → Authorization.
- Rate limits: 10 req/min by IP on auth paths; 3 req/hour by email/user on forgot-password, reset-password, request-email-change.
- DI: all RSS-Services classes, mapper helpers (`MapperHelpers`, `BasketballMapperHelper`, `FootballMapperHelper`, `SoccerMapperHelper`, `TimeHelpers`), Resend email (`IEmailService`→`ResendEmailService`), `IGameHubNotifier`→`GameHubNotifier`, hosted background workers (sport automations), MySQL DbContext (Pomelo) + ASP.NET Identity, IMemoryCache (security-stamp validation).
- SignalR hub mapped at `/hubs/game`.

### Controllers / routes
- `AuthController` [`/auth`] — `POST /login`, `POST /signup`, `GET /me` [Authorize], `POST /forgot-password` [rate-limited], `POST /reset-password` [rate-limited]
- `UserController` [`/user`, all Authorize] — `PATCH /display-name`, `PATCH /gamer-tag`, `POST /request-email-change` [rate-limited], `POST /confirm-email-change`
- `SquareGamesController` [`/squaregames`] — `GET GetAvailableSquareGames`, `POST CreateGame` [Auth], `POST start/{gameId}` [Auth, host], `GET find/{shortId}` [Auth], `GET {id}` [Auth], `GET GetSquareGameScoreData/{id}`, `POST join/{gameId}` [Auth], `POST leave/{gameId}` [Auth], `POST begin-selections/{gameId}` [Auth, host], `POST skip-player/{gameId}` [Auth, host], `GET turn-status/{gameId}` (public), `POST SquareSelections/{gameId}` [Auth], `GET GetGameboard/{gameId}` (public), `DELETE {gameId}` [Auth, host], `GET GetOutsideSquareNumbers/{gameId}` (public, closed games only)
- `AvailableSportsGamesController` [`/availablesportsgames`] — `GET GetAvailable{gameType}League{leagueId}GameOptions`, `GET GetAvailableSportsAndLeagues`
- `PlayerDashboardController` [`/playerdashboard`, all Authorize] — `GET stats`, `GET current-games`, `GET past-games?page&pageSize` (paginated)
- `AdminController` [`/admin`, all `Authorize(Roles="Admin")`] — `GET summary`, `GET games/current`, `GET games/past?page&pageSize`, `GET players/stats`, `GET users` (read-only reporting; backed by `AdminDashboardService`). Admin role seeded at startup by `AdminRoleSeeder` from `Admin:SeedEmail` config; role claim added to JWT in `AuthController.GenerateJwtToken`, `isAdmin` returned on login/`/me`. Frontend: `/admin` route guarded by `AdminRoute` component, ADMIN navbar link gated on `user.isAdmin`, `use-admin-dashboard.ts` hooks (30s staleTime + manual refresh). Built on `adminDashboard` branch (July 2026).

### Major modules
- **Middleware**: `EmailExtractionMiddleware` — extracts email (forgot/reset password) or user ID (email-change request) into `context.Items["rate-limit-key"]` for rate limiter.
- **Hubs**: `GameHub` (`/hubs/game`) — join/leave `game-{gameId}` SignalR groups. `GameHubNotifier` (implements `IGameHubNotifier`) broadcasts: `PlayerJoined`, `TurnAdvanced`, `SelectionsStarted`, `SquareSelected`, `PlayerLeft`, `GameStarted`, `GameDeleted`, `ScoreUpdated`.
- **SportsDataAutomation**: `BaseSportsAutomation` (abstract hosted service, runs daily at configured PST hour) → `BasketballAutomation`/`FootballAutomation`/`SoccerAutomation` (initial daily load from api-sports.io) and `*RefetchAutomation` variants (periodic score refresh for in-progress games).
- **Helpers**: `MapperHelpers` maps domain → DTOs (AvailableGamesMapper, ScoreDataMapper, PreGameboardMapper, SelectedGamePlayerSquaresMapper, OutsideSquareMapper, etc.)

### Auth model
- JWT (HMAC-SHA256), issuer/audience/key from config, claims include `NameIdentifier`, `Email`, `security_stamp`, role claims; TTL is role-based — 24h for admins, 7d for players. `POST /auth/logout` rotates the security stamp + evicts the stamp cache (immediate logout-everywhere). Refresh tokens deferred, see [[project-refresh-tokens]].
- SignalR connections authenticate via `?access_token=` query string (`OnMessageReceived`).
- `OnTokenValidated` compares JWT's `security_stamp` against current value (cached in IMemoryCache, 30s TTL) — invalidates old tokens across devices after password/security changes (see git history: "add security stamp auth for better invalidation across devices after pw resets").
- Password reset / email change tokens generated via `UserManager` (ASP.NET Identity), delivered through Resend email service (`TokenService`).

---

## 2. Backend — RSS-DB (`RSS-DB.csproj`, data access layer)

- **DbContext**: `AppDbContext` (extends `IdentityDbContext<ApplicationUser>`), Pomelo MySQL provider; custom value converter for `SquareGames.PeriodWinners` (Dictionary<int,string?> ↔ JSON longtext). `AppDbContextFactory` for migration tooling.
- **Entities**:
  - `ApplicationUser` — extends IdentityUser; `DisplayName`, `GamerTag`, `CreatedAt`, nav `GamePlayers`
  - `SquareGames` — UUID PK, FK→`DailySportsGames`; name, type, player count, price/square (decimal), open/closed, period count, period winners, turn-based flags, board coords (TopNumbers/LeftNumbers), `IsPublic`
  - `GamePlayer` — UUID PK, FK→`SquareGames`+`ApplicationUser`; turn order, host flag, turn history, nav `GamePlayerSquares`
  - `GameSquares` — UUID PK, FK→`SquareGames`+`GamePlayer`; row/col coords, home/away digits, claimed-by, created timestamp
  - `DailySportsGames` — UUID PK; external API ID, home/away teams, league, sport type, status, scores, period scores, `InUse` flag, UTC start time
- **Migrations** (11, Apr–Jun 2026, chronological): initialCreate → SeedData → AddQuarterSkippedFlags → SeedData1 → AddSquareSelectionLimit → AddTurnBasedFields → RemoveGamePlayerCountFields → GenericPeriodScoring → AddIsPublicToSquareGames → updateSqPriceToDecimal → AddGamerTagToUser.
  - Note: per [[project_pending_migrations]] memory, `AddSquareSelectionLimit` and `AddTurnBasedFields` may still need `dotnet ef database update` applied in some environments.
- **Key packages**: `Microsoft.EntityFrameworkCore` 8.0, `Pomelo.EntityFrameworkCore.MySql` 8.0, `Microsoft.EntityFrameworkCore.Tools` 8.0, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 8.0.

---

## 3. Backend — RSS-Services (`RSS-Services.csproj`, business logic)

- `AvailableGamesServices` — game CRUD (create, list public open games, delete w/ cascade, fetch by ID), period-winner lookup, score/winner retrieval
- `GamePlayerServices` — player-game lifecycle: create host player, join (capacity check), leave, verify host, turn advancement, turn status, selection-phase start
- `PlayerDashboardService` — player analytics: stats (periods won, wagered, wagers won, win rate — computed, not a stats table; see [[feedback_computed_stats]]), current games, past games (paginated)
- `SquareServices` — board management: generate empty board, create selections, validate availability, check selection limits, get gameboard state, get edge numbers
- `SportsGameServices` — external sports data: fetch from api-sports.io (football/basketball/soccer), map via sport-specific helpers, save to DB, check today's-games cache
- `UserServices` — user profile updates (find by ID, display name, gamer tag)
- `GeneralServices` — generic `SaveData<T>` repository helper
- `TokenService` — auth token generation + email delivery (password reset, email verification, email-change confirmation + notice email)
- **Helpers**: `BasketballMapperHelper`, `FootballMapperHelper`, `SoccerMapperHelper` (parse api-sports.io JSON), `TimeHelpers` (UTC/PST utilities)
- **Interface**: `IGameHubNotifier` (8 broadcast event types, implemented in RSS.Hubs)
- **DTOs**: `CurrentGameSummaryDTO`, `PastGameSummaryDTO`, `PlayerStatsDTO`, `PreGameboardDTO`, `QuarterlyWinnerDTO`, `SportScoreUpdateDTO`, `SportsGamesAvailableDTO`, `SportsGamesInUseDTO`, `TurnStatusDTO`, `OutsideSquareNumbersDTO`
- Depends on: RSS-DB, `ZlEmailProvider` (external email wrapper). No direct NuGet deps.

### Backend architectural patterns
1. Background hosted-service automation per sport, daily PST-scheduled fetch + periodic refetch
2. SignalR game-scoped groups (`game-{gameId}`) for real-time events
3. EF Core + ASP.NET Identity, JSON-serialized complex types (PeriodWinners)
4. Custom middleware-driven rate limiting keyed by email/user ID for sensitive endpoints
5. Security-stamp JWT validation for cross-device invalidation on password reset
6. Transactional game creation (SquareGames + GamePlayer + board generation in one DB transaction)
7. Turn-based and free-for-all selection modes supported by the same `SquareLimitCheck` (see [[project_free_for_all_round]])
8. Sport-agnostic scoring: flexible period counts (4 for football/basketball, 2 for soccer)

---

## 4. Frontend — RetroSportsSquaresWeb (React + Vite + TS)

### Entry points
- `client/src/main.tsx` — React root render
- `client/src/App.tsx` — sets up `QueryClientProvider`, `TooltipProvider`, Wouter `Router`
- `client/index.html` — SPA shell (`#root`), loads retro fonts (Press Start 2P, VT323, DM Sans)
- Routing: **Wouter** (v3.3.5). Routes:
  - `/` Home, `/login` Login, `/signup` Signup, `/options` GameOptions, `/leagues/:sport` LeagueOptions, `/arena/:type/:leagueId` Dashboard, `/game/:id` GameBoard, `/player-dashboard` PlayerDashboard, `/settings` Settings, `/confirm-email-change`, `/reset-password`

### Backend integration
- API base URL: `VITE_API_URL` env var (default `https://localhost:7187`)
- Route/endpoint definitions centralized in `shared/routes.ts` (auth, games, selections, dashboard, user)
- `client/src/lib/queryClient.ts` — TanStack Query v5 config: `credentials: "include"`, no refetch-on-focus, infinite staleTime, no retry; custom queryFn with 401 handling
- Auth token stored in `localStorage` ("token"), sent as Bearer header
- `useGameHub()` hook (`use-game-hub.ts`) — SignalR client to `/hubs/game`, subscribes to `TurnAdvanced`, `SquareSelected`, `GameDeleted`, `PlayerJoined`, `SelectionsStarted`, `GameStarted`, `ScoreUpdated`, `PlayerLeft`; invalidates React Query cache on events (see [[feedback_polling_source_of_truth]] — live state should derive from polling/turnStatus query, not the one-shot game query)

### Major modules (client/src)
- **Pages**: Home, Login, SignUp, GameOptions (sport picker), LeagueOptions, Dashboard (game lobby: join/create/delete/search-by-ID), GameBoard (10x10 grid, turn-based/free-for-all selection, live scoreboard, outside-numbers entry, winner tracking), PlayerDashboard (stats + paginated past games), Settings (profile/email/password), ConfirmEmailChange, ResetPassword, not-found
- **Components**: `Navbar`, `CreateGameDialog` (name/player count/wager/square limit/turn-based toggle/timeout), `Scoreboard` (period mapping: Q1-Q4 football/basketball, H1-H2 soccer), `RetroButton`, `RetroCard`, `EmailChangeModal`, `PasswordChangeModal`, `ui/` (50+ shadcn/ui components)
- **Hooks**: `use-auth.ts` (login/signup/auth), `use-games.ts` (games list/create/options/score/turn/begin-selections/skip/delete), `use-gameplay.ts` (join/leave/select square/board/outside numbers), `use-game-hub.ts` (SignalR), `use-dashboard.ts` (stats/current/past games), `use-toast.ts`, `use-mobile.tsx`
- **Lib**: `queryClient.ts`, `auth-utils.ts` (401 handling, redirect-to-login), `utils.ts` (`cn()` Tailwind merge)

### Shared layer (`shared/`)
- `schema.ts` — TS interfaces for all API types (SquareGame, SquareGameScoreData, SquareSelection, BoardSquare, OutsideSquare, User, TurnStatus, PlayerStats, CurrentGameSummary, PastGameSummary, etc.)
- `routes.ts` — API_BASE_URL + endpoint definitions
- `models/auth.ts` — Drizzle ORM tables (sessions, users) — legacy from Replit scaffold; primary auth is the .NET Identity/JWT backend, not this Express/Passport layer
- Note: `search bar` fuzzy matching design documented in [[project_search_and_join]] (Fuse.js, name/team only + direct-join-by-ID modal)

### Key dependencies (by purpose)
- **UI**: `@radix-ui/*` + shadcn/ui, `lucide-react`, `react-icons`, `recharts`, `framer-motion`, `tailwindcss` + typography plugin, `class-variance-authority`
- **State/API**: `@tanstack/react-query` v5, `zod` + `zod-validation-error`, `drizzle-orm`/`drizzle-zod`, `@microsoft/signalr` v10
- **Routing/forms**: `wouter`, `react-hook-form`, `@hookform/resolvers`
- **Utilities**: `date-fns`, `fuse.js`, `next-themes`, `clsx`/`tailwind-merge`
- **Build**: `vite` v7.3, `@vitejs/plugin-react`, `typescript` v5.6, `esbuild`, `drizzle-kit`, Replit dev plugins (cartographer, dev-banner, runtime-error-modal)
- **Legacy/unused server scaffold** (present but superseded by .NET backend): `express`, `express-session`, `connect-pg-simple`, `passport`/`passport-local`, `pg`, `ws`, `memorystore`, `stripe`, `nodemailer`, `xlsx`

### Build & scripts
- `npm run dev` — Vite dev server (HMR)
- `npm run build` — `script/build.ts`: Vite client build → `dist/public/`; esbuild-bundles legacy `server/index.ts` → `dist/index.cjs`
- `npm start` — production Node server (legacy Express scaffold, likely unused given .NET backend)
- `npm run check` — TS type-check
- `npm run db:push` — `drizzle-kit push` (legacy Drizzle schema, separate from RSS-DB/EF Core)

### Config
- `vite.config.ts` — path aliases `@`→client/src, `@shared`→shared, `@assets`→attached_assets; root=client; output=dist/public
- `tailwind.config.ts` — class-based dark mode, retro arcade palette (red/black), sharp border radius, VT323/Press Start 2P fonts
- `tsconfig.json` — ESNext, strict, path aliases, includes client/src+shared+server, noEmit
- `components.json` — shadcn "new-york" style, CSS vars enabled
- `drizzle.config.ts` — schema=shared/schema.ts, postgresql dialect (legacy scaffold, not the live MySQL/EF backend)

---

## Cross-cutting notes
- Two persistence stacks coexist in the repo: the **live** one is MySQL + EF Core (RSS-DB) driven by the .NET API; the **legacy** one is a Replit-scaffolded Postgres/Drizzle/Express/Passport stack under `RetroSportsSquaresWeb/shared` and `server/` that appears vestigial from initial scaffolding — verify before assuming it's active.
- Real-time state flows: .NET `GameHub` SignalR broadcasts → frontend `useGameHub()` invalidates TanStack Query caches → components re-fetch via the same REST endpoints in `shared/routes.ts`.
- Related memory files: [[feedback_computed_stats]], [[project_free_for_all_round]], [[feedback_polling_source_of_truth]], [[project_pending_migrations]], [[project_search_and_join]], [[project_email_implementation]].
