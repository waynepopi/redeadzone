# Contributing to Deadzone

Thank you for your interest in contributing to Deadzone.

## Getting started

1. **Fork** the repository on GitHub.
2. Create a focused **feature or fix branch** from `main`:
   ```
   git checkout -b fix/my-bug-fix
   ```
3. Make your changes. Keep commits focused and avoid mixing unrelated changes.
4. Validate your build locally before opening a pull request.

## Required validation

Run these commands from the repository root before submitting:

```powershell
dotnet restore
dotnet build Deadzone.sln -c Release
dotnet test Deadzone.sln -c Release
```

All three must succeed with zero errors and no failing tests.

## Guidelines

- **Do not replace the project architecture** (WPF, SDL3, HIDMaestro) without prior discussion in an issue.
- **Do not change dependency versions casually.** If a dependency upgrade is needed, explain why in the pull request.
- **Hardware-related changes** should identify the controller model, connection method (USB/Bluetooth), and Windows version tested.
- Prefer small, reviewable pull requests over large all-in-one changes.
- Update documentation if your change affects user-visible behaviour.

## Architecture overview

```
Physical Controller (SDL3)
        ↓
  Deadzone Processing   ← Deadzone.Core
  (radial, per-stick)
        ↓
Virtual Controller (HIDMaestro)
        ↓
       Game
```

- `src/Deadzone.Core` — pure processing logic (no UI, no SDL, no HIDMaestro)
- `src/Deadzone.App`  — WPF application (SDL input, HIDMaestro output, UI)
- `tests/Deadzone.Core.Tests` — xUnit tests for processing logic

## Running the packager locally

Requires Inno Setup 7 installed at the default location.

```powershell
.\scripts\package.ps1 -Version 0.1.0
```

## Opening a pull request

- Describe what problem the PR solves.
- Reference any related issues.
- Identify the controller/connection method if applicable.
- Confirm the required validation commands passed.
