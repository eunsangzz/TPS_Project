# TPS Project Server

Node.js + PostgreSQL backend for Unity login, player data and a public top-ten leaderboard.

Production URL:

```text
https://tps-project.onrender.com
```

## Setup

Requires Node.js 22.9+ and PostgreSQL. The server intentionally refuses to start
without DATABASE_URL; it never reports a file-only save as a permanent DB save.

```powershell
cd Server
npm.cmd install
$env:DATABASE_URL = 'postgresql://USERNAME:PASSWORD@HOST:5432/DATABASE'
npm.cmd start
```

Alternatively, use a local `Server/.env` file with the keys in `.env.example`.
`.env` is ignored by Git. Keep DB credentials on the server, never in Unity.

## Render Setup (Existing tps-project Service)

1. Render Dashboard > New > Postgres. Use the same region and workspace as
   the existing web service. Name suggestion: `tps-project-db`.
2. Choose the plan yourself. Free Render Postgres expires after 30 days;
   it is a trial, not permanent free storage. Do not rely on it long term.
3. When the database is available, copy its **Internal Database URL**.
4. Open existing `tps-project` web service > Environment > Add Environment Variable:
   set `DATABASE_URL` to the Internal Database URL. Do not post it in chat or Git.
5. Keep Root Directory `Server`, Build Command `npm ci`, Start Command `npm start`.
   Deploy the updated code after the environment variable is configured.
6. Startup creates the tables and indexes automatically. Logs should show
   `TPS server ready. Storage: PostgreSQL`.
7. `/ping` must return `storage: "postgresql"`; `/leaderboard` should return
   `{"entries":[]}` before any scores are saved.

No cloud database is provisioned by these source files. Until DATABASE_URL is
configured and this revision is deployed, the current Render service still runs
the previous implementation.

For a connection from your PC, use the **External Database URL**, not the
internal URL. Configure TLS as directed by the provider; never disable certificate
verification to work around a connection failure.

Official docs: https://render.com/docs/postgresql-creating-connecting
Free plan limitations: https://render.com/docs/free

## Score Flow

- Every run starts at score zero. Enemy kills enqueue the running total.
- Unity sends `POST /scores` with `{ "score": 500 }` and a Bearer token.
- PostgreSQL updates `best_score = GREATEST(best_score, submitted_score)` atomically.
  Lower scores, retries and simultaneous submissions cannot erase the best score.
- `GET /player-data` returns the account's `bestScore` on login.
- `GET /leaderboard` is public and returns up to ten positive scores, highest first.
  Ties use the earliest record timestamp, then a stable user ID ordering.
- The Login scene creates its UI automatically, including ranking refresh and
  loading/empty/error states. No new scene objects need to be wired in Inspector.
- The HUD shows the current run, confirmed personal best and save status.
- On death, the result overlay pauses gameplay. `RETURN TO LOGIN` opens Login
  again. The persistent GameSession keeps pending HTTP saves alive across scenes.
- Pending best scores are saved locally by user ID, retried with backoff and
  removed only after the DB confirms the score. Reopening the game and signing
  into the same account retries a pending save. Force-quitting before success
  cannot guarantee the server has received it yet.

## Identity and Security Limits

- The browser portal is served at `/` on the same Render web service. It includes
  registration, login, logout, personal best, personal rank and the public top ten.
- New accounts register with a unique case-insensitive username (3-24 letters,
  digits or underscores), a display name (1-32 characters) and a password
  (12-128 characters). Passwords use salted asynchronous scrypt hashes, never
  plaintext. The accounts table is added without resetting existing scores.
- Register in the browser, then enter the same username/password in Unity.
  `POST /auth/register` and `/auth/login` return bearer tokens for native clients.
- Browser endpoints `/web/auth/register`, `/web/auth/login`, `/web/auth/logout`
  use an HttpOnly, SameSite=Strict cookie (Secure over HTTPS).
  They require same-origin JSON requests. `/web/me` returns the profile and rank,
  but never returns a token or password hash. Browser tokens are not in localStorage.
- Authentication requests are limited to 30 per IP per 15 minutes per server
  process. Multiple server replicas need a shared rate-limit store. The Express
  proxy configuration assumes one trusted reverse proxy, as used for this service.
- Email verification, password recovery, MFA and conversion of guest accounts
  into member accounts are not implemented. Keep your password; use a distinct
  password for this development project.
- `player` / `1234` is still a shared demonstration account. Everyone using it
  shares one record; registered users get separate records. Set DEMO_PASSWORD to
  change the demo password (and enter that password in Unity).
- Guests get separate records. Unity stores a guest resume token on that device;
  `CONTINUE AS GUEST` reuses the identity. `NEW GUEST` starts a different identity.
  Uninstalling/clearing PlayerPrefs loses guest recovery. Sessions expire in 30 days.
- Session token hashes live in PostgreSQL and survive web-service restarts.
- The server validates score format and account ownership, but trusts the
  client-reported score. PlayerPrefs are also editable. This is NOT anti-cheat;
  do not use the leaderboard for prizes or a competitive production release.
- Existing `Server/data/player-data.json` is left untouched and is not automatically
  imported. The new DB starts with default player data. Back up any valued old data
  before deploying. Production backups and permanent hosting must be configured
  with your DB provider.

## Tests

```powershell
cd Server
npm.cmd test
```

Tests run the actual SQL and HTTP endpoints against PGlite (an embedded PostgreSQL
engine) in a temporary directory, including closing/reopening the DB. This checks
persistence, isolation, retries, concurrent records, top-ten ordering and expiry.
It does not validate your Render credentials, network or TLS configuration.

For isolated browser testing without touching Render data, run `npm.cmd run preview`
and open `http://127.0.0.1:3100`. This uses an explicitly local PGlite test database
under ignored `Server/data/web-preview`; it is not the production server or a
fallback for a missing DATABASE_URL. Change PREVIEW_PORT if 3100 is occupied.

## Test

Open this URL in a browser:

```text
http://localhost:3000/ping
```

Expected response:

```json
{
  "message": "pong",
  "storage": "postgresql",
  "serverTime": "2026-08-31T00:00:00.000Z"
}
```

## Auth Test

Guest login:

```powershell
Invoke-RestMethod http://localhost:3000/auth/guest `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"displayName":"Guest"}'
```

Demo user login:

```powershell
Invoke-RestMethod http://localhost:3000/auth/login `
  -Method Post `
  -ContentType "application/json" `
  -Body '{"username":"player","password":"1234"}'
```

Load player data with the returned token:

```powershell
$token = "paste-token-here"
Invoke-RestMethod http://localhost:3000/player-data `
  -Headers @{ Authorization = "Bearer $token" }
```

Save player data:

```powershell
$token = "paste-token-here"
Invoke-RestMethod http://localhost:3000/player-data `
  -Method Put `
  -ContentType "application/json" `
  -Headers @{ Authorization = "Bearer $token" } `
  -Body '{"level":2,"xp":150,"coins":250,"selectedWeapon":"Rifle"}'
```
