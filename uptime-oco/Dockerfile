# 1) CSS (Tailwind) — requires Node
FROM node:22-alpine AS css
WORKDIR /src
COPY uptime-oco/package.json uptime-oco/package-lock.json* ./
RUN npm install
COPY uptime-oco/ ./
RUN npm run css:build

# 2) Build .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["uptime-oco/uptime-oco.csproj", "uptime-oco/"]
RUN dotnet restore "uptime-oco/uptime-oco.csproj"
COPY . .
COPY --from=css /src/wwwroot/css/site.css uptime-oco/wwwroot/css/site.css
RUN dotnet publish "uptime-oco/uptime-oco.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 3) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__UptimeOcoContext="Data Source=/app/data/uptime-oco.db"
ENV DataProtection__KeysPath=/app/data/keys
COPY --from=build /app/publish .
VOLUME /app/data
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "uptime-oco.dll"]