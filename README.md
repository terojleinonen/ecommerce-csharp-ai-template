# React + C# (.NET) E‑Commerce with AI – Template

This is a starter template for an e‑commerce application with:

- **Frontend:** React + TypeScript (Vite)
- **Backend:** .NET 8 minimal APIs (C#)
- **AI features (stubs):**
  - Product recommendations
  - Semantic search
  - Chat assistant
  - AI product descriptions

> This repo is meant as a **learning/portfolio** starting point – you’ll still need to plug in real DB, auth, and real AI keys.

## Structure

```text
.
├── backend
│   └── src
│       ├── ECommerce.Api
│       ├── ECommerce.Core
│       └── ECommerce.Infrastructure
└── frontend
    └── (Vite React app)
```

## Quick start

### Backend

```bash
cd backend/src/ECommerce.Api
dotnet restore
dotnet run
```

The API listens on `https://localhost:5001` (by default) and exposes e.g.:

- `GET /api/products`
- `POST /api/ai/chat`

### Frontend

```bash
cd frontend
npm install
npm run dev
```

The frontend assumes backend at `https://localhost:5001`. You can change the URL in `src/services/api.ts`.

## AI Configuration

- Put your OpenAI (or other provider) API key into `appsettings.Development.json` under `AI:OpenAI:ApiKey`.
- The code uses **dependency injection** and an `IAIService` interface so you can swap providers.

## Notes

- EF Core is configured for a simple in‑memory DB by default to keep the template simple.
- For production, replace with PostgreSQL/SQL Server and real migrations.
- Authentication is not fully implemented – you can add ASP.NET Identity or JWT later.
