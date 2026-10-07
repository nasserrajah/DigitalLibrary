@'
# Digital Library Backend

ASP.NET Core 8 Web API - Digital & Educational Library

## Features
- JWT Authentication + Refresh Tokens
- ASP.NET Core Identity + Roles (Admin, User)
- Google + Facebook OAuth
- Books + Categories (max 20) + Favorites
- User Library with 500MB storage limit
- Notifications
- SQLite + EF Core
- Swagger / OpenAPI
- Global Error Handling
- Serilog Logging

## Tech Stack
- .NET 8
- Entity Framework Core 8
- SQLite
- ASP.NET Core Identity
- JWT
- FluentValidation
- AutoMapper
- Serilog

## Quick Start

\`\`\`bash
# 1. Restore + Build
dotnet restore
dotnet build

# 2. Create DB + Migrate
dotnet ef database update -p src/Library.Infrastructure -s src/Library.API

# 3. Run
dotnet run --project src/Library.API
\`\`\`

## Swagger
http://localhost:5199/swagger

## Default Admin (Development)
- Email: admin@library.local
- Password: Admin@12345

## Architecture
- **Library.Domain** — Entities, Enums, Constants
- **Library.Application** — DTOs, Services, Validators
- **Library.Infrastructure** — EF Core, Identity, JWT, FileStorage
- **Library.API** — Controllers, Middleware, Swagger
- **Library.Shared** — Cross-cutting concerns

## Testing
\`\`\`bash
dotnet test
\`\`\`
'@ | Set-Content -Path "README.md" -Encoding UTF8