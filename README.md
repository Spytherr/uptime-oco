# Uptime Oco

Uptime Oco is a self-host uptime monitoring tool for tracking the availability and response times of websites and APIs.
It is built with ASP.NET Core MVC (.NET 10), SQLite (Entity Framework Core), SignalR for real-time updates, and Tailwind CSS.

## Features

- HTTP/HTTPS checks with custom intervals, expected status codes, and retry thresholds.
- Real-time dashboard updates via SignalR.
- Incident logging for tracking outages and recovery.
- Alert notifications via Discord and generic webhooks.
- User authentication with a first-run setup wizard or automatic provisioning via environment variables.

## Requirements

- Docker and Docker Compose (recommended), or
- .NET 10 SDK (for running locally without Docker)

## How to Run

### Using Docker Compose (Recommended)

1. Start the application:
   ```bash
   docker compose up -d --build
   ```

2. Open `http://localhost:8080` in your browser.

On first launch, you can create the admin account via the web setup wizard. Alternatively,
you can uncomment and set `INITIAL_ADMIN_EMAIL` and `INITIAL_ADMIN_PASSWORD` in `docker-compose.yml` to provision the account automatically.

To stop the container:
```bash
docker compose down
```

### Running Locally (.NET)

1. Run the project:
   ```bash
   dotnet run --project uptime-oco
   ```

2. Open `http://localhost:5299` or `https://localhost:7201` in your browser and complete the initial setup.

To rebuild CSS assets (requires Node.js):
```bash
cd uptime-oco
npm install
npm run css:build
```
