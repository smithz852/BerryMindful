---
name: verify
description: Build, launch, and drive BerryMindful (ASP.NET Core API + Vite React client) to verify changes at the running surface.
---

# Verifying BerryMindful changes

## Launch
- API: `dotnet run --launch-profile https` from `server/BerryMindful.Api` → https://localhost:7068 (self-signed dev cert — use `curl -k` / Playwright `ignoreHTTPSErrors: true`). MySQL runs as the local `MySQL83` Windows service; migrations auto-apply in Development.
- Startup logs state which providers are active (Vision+Claude scanner, Resend email, Spoonacular recipes) vs. key-less stubs — grep the log for `Receipt scanning:|Email:|Recipes:`.
- Client: `npm run dev` from `client/` → http://localhost:5173.
- Test account: `zach.test@example.com` / `password123`.

## API driving
- Login: `POST /auth/login` with `{"email","password"}` → `accessToken` in JSON; pass as `Authorization: Bearer`.
- No `python` on this machine — pipe JSON through `node -e` instead.

## UI driving (Playwright)
- The `playwright` package is NOT installed in the repo, but browsers are cached. Working recipe: `npm i playwright-core` in a scratch dir, then launch with
  `chromium.launch({ executablePath: process.env.LOCALAPPDATA + "\\ms-playwright\\chromium-1084\\chrome-win\\chrome.exe" })`
  and `newContext({ ignoreHTTPSErrors: true })` (the app fetches the https API).
- Login flow: fill `input[type=email]` / `input[type=password]` on `/login`, click `button[type=submit]`, `waitForURL("**/pantry")`.

## Gotchas
- `#root` is a flex column and `main` has `margin: 0 auto`, so `main` shrink-to-fits its content — page-level classes that need real width must set `width: 100%` (+ `box-sizing: border-box`, since `main` has horizontal padding). Check 375px viewports for horizontal overflow after layout changes.
