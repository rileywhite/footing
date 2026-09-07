# Footing

Personal finance web application built with .NET 10 and Blazor WebAssembly. A standalone
static site — there is no server-side compute in production.

## Standing Constraint

Footing is permanently a standalone WebAssembly app. No server-side calls, ever. Zero
third-party requests. Any new outbound request the app makes — a font CDN, an API call,
an analytics beacon, anything not served from `footing.app` itself — is a product-level
regression, not a feature. Treat this as a hard constraint when adding dependencies or UI.

## Architecture

- **Footing.Client** — Blazor WebAssembly app. This is the entire application; it is
  published as static files and hosts itself in the browser.
- Client-side data only, via `Blazored.LocalStorage`. There is no server database and
  never was any PII sent off the browser.
- Excel export via Simplexcel, generated client-side.

There is no ASP.NET Core server host, no .NET Aspire orchestration, and no hybrid
SSR/WebAssembly rendering. Those existed early in the project's history and have been
removed; do not reintroduce them.

## Testing

- Unit: xUnit + FluentAssertions (`src/Footing.Tests.Unit`)
- Functional: bUnit component tests (`src/Footing.Tests.Functional`)
- Integration: Moq-based service tests (`src/Footing.Tests.Integration`)
- E2E: Playwright browser automation (`src/Footing.Tests.E2E`), run against the static
  Release publish output of `Footing.Client` — not against a hosted server process.

## Deployment

- GitHub Pages, via `.github/workflows/deploy-pages.yml`. Publishes `Footing.Client` in
  Release configuration and deploys the static output directly.
- Custom domain: `footing.app`. There is no compute in production — Pages serves static
  files only.
- CI: `.github/workflows/ci.yml` runs the `build-and-test` status check. That one job
  builds `src/Footing.slnx`, runs the unit, functional and integration suites, then
  builds `Footing.Tests.E2E`, installs Chromium via `playwright.ps1 install --with-deps`,
  and runs the E2E suite with `PLAYWRIGHT_REQUIRED=1`. E2E therefore gates merges like
  every other suite, and under that variable a failed `Footing.Client` publish or a
  missing browser fails the check rather than quietly skipping the tests. `main` is
  protected and requires this check to pass before merge; nothing is pushed to `main`
  directly.

There is no Docker image, no Helm chart, no AKS deployment, and no Azure Pipelines
CI/CD. Those existed early in the project's history and have been removed; do not
reintroduce them.
