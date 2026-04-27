# CareerForge

A SaaS platform for job-search preparation. Resumes and job descriptions are analysed
into structured profiles, scored against each other with explainable findings, and used
to drive AI-graded mock interviews. English and Ukrainian.

> 🇺🇦 [Українською](./README.uk.md)

- **Backend** — .NET 9, Clean Architecture (`Api` / `Application` / `Domain` / `Infrastructure`).
- **Frontend** — React 19, Vite, TypeScript, Tailwind v4. Optional iOS shell via Capacitor.
- **Storage** — PostgreSQL 16 with the `pgvector` extension for semantic similarity.
- **LLMs** — Gemini → Groq fallback chain. Both free tiers are sufficient for development.

---

## Local development

### 1. Prerequisites

| Tool | Purpose |
| --- | --- |
| [.NET 9 SDK](https://dotnet.microsoft.com/download) | Backend runtime |
| [Node 20+](https://nodejs.org/) | Frontend toolchain |
| [Docker](https://www.docker.com/products/docker-desktop/) | Local Postgres + pgvector |

### 2. Provider API keys

Create free accounts and obtain a key from each:

1. **Gemini** — <https://aistudio.google.com/apikey>
2. **Groq** — <https://console.groq.com/keys>

### 3. Database

```bash
docker compose up -d
```

Starts `pgvector/pgvector:pg16` on `localhost:5432` (database `careerforge`,
user/password `postgres`/`postgres`). Data persists in a Docker volume.

### 4. Backend configuration

API keys are stored via `dotnet user-secrets` outside the repository:

```bash
cd src/CareerForge.Api
dotnet user-secrets init
dotnet user-secrets set "Llm:Gemini:ApiKey" "<gemini-key>"
dotnet user-secrets set "Llm:Groq:ApiKey"   "<groq-key>"
```

Environment variables (`Llm__Gemini__ApiKey`, `Llm__Groq__ApiKey`) are also accepted.

### 5. Run

```bash
# Terminal 1
cd src/CareerForge.Api && dotnet run

# Terminal 2
cd web && npm install && npm run dev
```

Backend listens on `http://localhost:5117`; the OpenAPI reference is at `/scalar` in
Development. Frontend serves on `http://localhost:5173`.

---

## Configuration reference

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Default` | local Docker Postgres | Npgsql connection string |
| `ConnectionStrings:Replica` | — | Optional read replica; consumed by `IReadOnlyDb` impls |
| `Cors:AllowedOrigins` | `["http://localhost:5173"]` | Frontend origins permitted to call the API |
| `Database:AutoMigrate` | `null` | `null` → enabled in Development only; explicit `true`/`false` to override |
| `Jwt:SigningKey` | development placeholder | **Required in production**, ≥ 32 bytes |
| `Llm:DefaultLlmProvider` | `gemini` | One of `gemini`, `groq`, `ollama` |
| `Llm:DefaultEmbeddingProvider` | `gemini` | One of `gemini`, `ollama` |
| `Llm:Gemini:ApiKey` | — | Required for the primary LLM tier |
| `Llm:Groq:ApiKey` | — | Required for the fallback LLM tier |
| `RateLimits:HeavyPerHour` | `10` | Per-user budget for LLM-heavy endpoints (matching, parsing) |
| `RateLimits:InteractivePerMinute` | `6` | Per-user budget for interactive LLM endpoints |
| `Sentry:Dsn` (or `SENTRY_DSN`) | — | Optional. Enables server-side error reporting |
| `VITE_SENTRY_DSN` | — | Optional. Build-time DSN for browser error reporting |

In production, prefer environment variables (`Section__Key=...`) over `appsettings.*.json`.

---

## Project layout

```
src/
  CareerForge.Api/             ASP.NET minimal-API host, endpoints, validation
  CareerForge.Application/     Use cases, DTOs, abstractions (no infrastructure dependencies)
  CareerForge.Domain/          Entities, value objects, enums
  CareerForge.Infrastructure/  EF Core, LLM providers, document parsers, identity, background jobs
tests/
  CareerForge.Api.Tests/
  CareerForge.Application.Tests/
web/                           React + Vite frontend (with optional Capacitor iOS shell)
docker-compose.yml             Local Postgres + pgvector
```

---

## Tests and verification

```bash
dotnet test                  # backend unit + integration tests
cd web && npx tsc --noEmit   # frontend type-check
cd web && npm run build      # frontend production bundle
```

---

## Deployment

The backend ships as a standard .NET 9 container (see `Dockerfile`). The frontend is a
static Vite build that can be served either as a CDN-backed static site or via the
nginx container in `web/Dockerfile`.

Reference deployment topology:

- **Backend** — [Render](https://render.com/) Web Service from the `Dockerfile` at the
  repository root. Set `VITE_API_BASE` (frontend) and the secrets listed below.
- **Frontend** — [Render](https://render.com/) Static Site with build command
  `cd web && npm install && npm run build` and publish path `web/dist`. Inject
  `VITE_API_BASE` at build time so the bundle points at the deployed backend URL.
- **Database** — [Neon](https://neon.tech/) (managed PostgreSQL with `pgvector`
  preinstalled). Run `CREATE EXTENSION IF NOT EXISTS vector;` once after creating the
  database.

Any Docker-compatible host accepts the same images.

Production checklist:

- Generate a cryptographically random `Jwt:SigningKey` (≥ 32 bytes).
- Add the frontend origin to `Cors:AllowedOrigins`.
- Run with `ASPNETCORE_ENVIRONMENT=Production`.
- Run migrations as a release task, not at startup. With `ASPNETCORE_ENVIRONMENT=Production`
  and `Database:AutoMigrate` unset, the API will not migrate on boot; apply migrations
  explicitly:
  ```bash
  cd src/CareerForge.Api
  dotnet ef database update --connection "$ConnectionStrings__Default"
  ```
- Tune `RateLimits:HeavyPerHour` and `RateLimits:InteractivePerMinute` for the
  active LLM tier.

---

## Security

- Provider API keys live in `dotnet user-secrets` (development) or environment variables
  (production). They are never committed.
- The `appsettings.Development.json` JWT signing key is a placeholder and must not be
  used in production.
- Default Postgres credentials apply to the local Docker container only. Production
  deployments must use a managed database with strong credentials.
