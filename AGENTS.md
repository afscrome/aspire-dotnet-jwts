# Agents

This file provides quick orientation for coding agents working in this repository.

## Repository Purpose

This repo contains an Aspire hosting integration package that adds dashboard and CLI resource commands for generating local development JWTs, similar to `dotnet user-jwts` workflows.

## High-Level Layout

- `src/`
  - Main package source (`AlexCrome.Aspire.Hosting.UserJwts.csproj`).
  - Core implementation files for signing token resources, resource builder extensions, and claim/parameter defaults.
- `tests/unit/`
  - Unit tests for low-level behaviors (resource defaults, handlers, extension helpers).
- `tests/integration/`
  - Integration tests for end-to-end behavior, including resource command flows and generated token configuration.
- `playground/`
  - Local sample app used to manually validate package behavior in an Aspire AppHost + API scenario.
- `docs/`
  - Documentation assets, including demo media.
- `artifacts/`
  - Build outputs and intermediate files.

## Important Projects

- `src/AlexCrome.Aspire.Hosting.UserJwts.csproj`
  - The package under development.
- `tests/unit/AlexCrome.Aspire.Hosting.UserJwts.UnitTests.csproj`
  - Unit test project.
- `tests/integration/AlexCrome.Aspire.Hosting.UserJwts.IntegrationTests.csproj`
  - Integration test project.
- `playground/AppHost/AlexCrome.Aspire.Hosting.UserJwts.AppHost.csproj`
  - Aspire AppHost sample for manual checks.
- `playground/DotnetAppWithAuth/ApiService.csproj`
  - Sample API service used by the AppHost.

## Build and Test Entry Points

From repo root:

- Build: `dotnet build`
- Test (MTP/TUnit): `dotnet test`
- Pack: `dotnet pack`

### Microsoft Testing Platform (MTP) Notes

- This repo uses `TUnit`, which runs on Microsoft Testing Platform (MTP).
- For MTP/TUnit options, pass arguments after `--` in `dotnet test`.
- VSTest-style `dotnet test --filter ...` is not supported in this repo.

Full MTP example (run integration tests and emit TRX):

```bash
dotnet test tests/integration/AlexCrome.Aspire.Hosting.UserJwts.IntegrationTests.csproj -- --report-trx --results-directory artifacts/TestResults/integration
```

Filtering example (run only tests in one class via tree filter):

```bash
dotnet test tests/unit/AlexCrome.Aspire.Hosting.UserJwts.UnitTests.csproj -- --treenode-filter "/*/*/SigningTokenResourceTests/*"
```

## Working Conventions

- Prefer changes in `src/` plus corresponding test coverage in `tests/unit/` and/or `tests/integration/`.
- Use `playground/` for manual validation and behavior checks, not as the source of package logic.
- Avoid editing `artifacts/`; it is generated output.
