# CareerForge

Робоче місце для підготовки до співбесід. Завантажте резюме та опис вакансії, отримайте гібридну оцінку відповідності з поясненнями, та тренуйтеся проходити співбесіди з відповідями, які оцінює ШІ — українською або англійською.

> 🇬🇧 [In English](./README.md)

- **Бекенд** — .NET 9 (minimal APIs, Clean Architecture: `Api` / `Application` / `Domain` / `Infrastructure`).
- **Фронтенд** — React 19 + Vite + TypeScript + Tailwind v4.
- **Сховище** — PostgreSQL 16 + `pgvector` (семантична схожість).
- **LLM** — безкоштовний Gemini → безкоштовний Groq у разі fallback. **Не потрібно жодних платних API-ключів.**

---

## Локальний запуск — від клонування до робочого додатку за 5 хвилин

### Крок 1 — Встановіть один раз

| Інструмент | Навіщо | Як встановити |
| --- | --- | --- |
| [.NET 9 SDK](https://dotnet.microsoft.com/download) | Бекенд-середовище | `brew install dotnet@9` (mac) або через сайт |
| [Node 20+](https://nodejs.org/) | Фронтенд dev-сервер | `brew install node@20` або через сайт |
| [Docker](https://www.docker.com/products/docker-desktop/) | Локальний Postgres + pgvector однією командою | Docker Desktop |

### Крок 2 — Отримайте безкоштовні API-ключі

Знадобиться два — обидва мають щедрі безкоштовні ліміти й не вимагають оплати:

1. **Gemini** → <https://aistudio.google.com/apikey> → "Create API key" → скопіюйте.
2. **Groq** → <https://console.groq.com/keys> → "Create API Key" → скопіюйте.

(Використовуються як ланцюжок fallback: спочатку Gemini, потім Groq, коли Gemini вичерпує денний ліміт.)

### Крок 3 — Клонуйте + підніміть базу

```bash
git clone <посилання-на-репо>
cd CareerForge
docker compose up -d
```

Це підніме `pgvector/pgvector:pg16` на `localhost:5432` (база: `careerforge`, користувач/пароль: `postgres`/`postgres`). Дані зберігаються в томі Docker, тож переживуть перезапуск.

### Крок 4 — Покладіть API-ключі у user-secrets

User-secrets зберігаються *поза* репозиторієм (у `~/.microsoft/usersecrets/...`), тож вони ніколи не потраплять у git.

```bash
cd src/CareerForge.Api
dotnet user-secrets init
dotnet user-secrets set "Llm:Gemini:ApiKey" "ВСТАВТЕ_СВІЙ_GEMINI_KEY"
dotnet user-secrets set "Llm:Groq:ApiKey"   "ВСТАВТЕ_СВІЙ_GROQ_KEY"
```

> Хочете через змінні середовища? Встановіть `Llm__Gemini__ApiKey` і `Llm__Groq__ApiKey` у вашій оболонці.

### Крок 5 — Запустіть бекенд

З тієї ж директорії:

```bash
dotnet run
```

API слухає на **<http://localhost:5117>**. Міграції EF Core застосовуються автоматично при старті. Інтерактивна довідка API доступна на <http://localhost:5117/scalar> у режимі розробки.

### Крок 6 — Запустіть фронтенд (новий термінал)

```bash
cd web
npm install
npm run dev
```

Відкрийте **<http://localhost:5173>**. Зареєструйтеся з будь-якою поштою (листи насправді не надсилаються — у режимі розробки токен підтвердження відображається в інтерфейсі). Завантажте резюме, вставте вакансію, спробуйте збіг або сесію співбесіди.

Готово.

---

## Усунення проблем

| Симптом | Імовірна причина | Рішення |
| --- | --- | --- |
| `Connection refused` при старті бекенду | Контейнер Postgres не запущено | `docker compose up -d`, зачекайте ~3 с |
| `Gemini` 401 | API-ключ не встановлено або недійсний | Запустіть `dotnet user-secrets set` ще раз, перезапустіть `dotnet run` |
| Обидва LLM-рівні падають з `429` | Денний ліміт Gemini вичерпано + проблема з Groq | Зачекайте ~хвилину, ШІ-оцінювач покаже коректне повідомлення про недоступність |
| `pgvector extension does not exist` | Використовується звичайний Postgres замість образу `pgvector/pgvector` | Використайте наш `docker compose` |
| Бурштинова стрічка "немає звʼязку із сервером" | Бекенд не запущено або неправильний порт | Перевірте, що `dotnet run` працює на порту 5117 |
| `dotnet ef` не знайдено | Інструменти EF не встановлені глобально | `dotnet tool install --global dotnet-ef` |

---

## Налаштування

| Параметр | Значення за замовчуванням | Призначення |
| --- | --- | --- |
| `ConnectionStrings:Default` | локальний Docker Postgres | Стандартний рядок підключення Npgsql |
| `Cors:AllowedOrigins` | `["http://localhost:5173"]` | Додайте origin вашого фронтенду в продакшені |
| `Database:AutoMigrate` | `true` | Запускати міграції EF при старті |
| `Jwt:SigningKey` | dev-only заглушка | **Обовʼязково в продакшені.** ≥ 32 байти |
| `Llm:DefaultLlmProvider` | `gemini` | Один з `gemini`, `groq`, `ollama` |
| `Llm:DefaultEmbeddingProvider` | `gemini` | Один з `gemini`, `ollama` |
| `Llm:Gemini:ApiKey` | — | Обовʼязково |
| `Llm:Groq:ApiKey` | — | Використовується як fallback, коли Gemini вичерпано |

У продакшені краще використовуйте змінні середовища (`Section__Key=...`), а не `appsettings.*.json`.

---

## Структура проєкту

```
src/
  CareerForge.Api/             ASP.NET minimal-API хост, ендпоінти, валідація
  CareerForge.Application/     Use cases, DTOs, абстракції (без інфра-залежностей)
  CareerForge.Domain/          Entities, value objects, enums
  CareerForge.Infrastructure/  EF Core, LLM-провайдери, парсери документів, identity
tests/
  CareerForge.Api.Tests/
  CareerForge.Application.Tests/
web/                           React + Vite фронтенд
docker-compose.yml             Локальний Postgres + pgvector
```

---

## Тести

```bash
dotnet test                  # бекенд
cd web && npx tsc --noEmit   # перевірка типів фронтенду
```

---

## Деплой (бюджетний варіант)

1. **База даних** — керований Postgres + pgvector. [Neon](https://neon.tech) і [Supabase](https://supabase.com) пропонують безкоштовний рівень із підтримкою `CREATE EXTENSION vector;`.
2. **API** — будь-який хост контейнерів:
   - [Azure Container Apps](https://azure.microsoft.com/products/container-apps) — щедрий безкоштовний грант, scale-to-zero
   - [Fly.io](https://fly.io) — `fly launch` з `src/CareerForge.Api`
   - [Render](https://render.com) — hobby tier
3. **Фронтенд** — `npm run build` у `web/` → статичний деплой на [Cloudflare Pages](https://pages.cloudflare.com) або [Vercel](https://vercel.com). Налаштуйте rewrite `/api` на ваш API origin.

Чек-лист перед продакшеном:

- Згенеруйте справжній `Jwt:SigningKey` (≥ 32 байти криптографічно стійкої випадковості).
- Додайте origin вашого фронтенду до `Cors:AllowedOrigins`.
- Запускайте з `ASPNETCORE_ENVIRONMENT=Production`.

---

## Питання безпеки

- API-ключі зберігайте у **`dotnet user-secrets`** (поза репозиторієм) або у змінних середовища — ніколи не в `appsettings.*.json`.
- Закомічений `appsettings.Development.json` містить лише *заглушку* JWT signing key; обовʼязково замініть її перед будь-яким деплоєм.
- Стандартні облікові дані Postgres (`postgres` / `postgres`) призначені лише для локального Docker-контейнера. У продакшені використовуйте керовану базу з надійними обліковими даними.
