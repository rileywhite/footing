# Footing

Personal finance web application built with .NET 10, Blazor WebAssembly, and Aspire.

## Architecture

- **Footing** — ASP.NET Core server (Blazor Server host)
- **Footing.Client** — Blazor WebAssembly library (interactive client-side UI)
- **Footing.AppHost** — .NET Aspire orchestration
- **Footing.ServiceDefaults** — Shared OpenTelemetry, health checks, resilience

## Key Patterns

- Client-side data only (Blazored.LocalStorage, no server DB)
- Privacy-first: no PII leaves the browser
- Hybrid Blazor rendering (SSR + WebAssembly)
- Excel export via Simplexcel

## Testing

- Unit: xUnit + FluentAssertions
- Functional: bUnit (Blazor component tests)
- Integration: Moq-based service tests
- E2E: Playwright browser automation

## Deployment

- Docker (multi-stage Dockerfile)
- Helm chart for Kubernetes (AKS)
- Azure Pipelines CI/CD
