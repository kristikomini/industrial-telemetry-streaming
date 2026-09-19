# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade `MotorValley.Backend` and `MotorValley.Tests` from `net8.0` to `net10.0`.
**Scope**: Small two-project SDK-style solution with one project reference, 25 code files, and 15 assessed issues.

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: Two low-difficulty projects already target modern .NET, use SDK-style project files, and have a shallow dependency structure with bounded package and API changes.

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | All-at-Once | The solution is small, modern, SDK-style, and has a straightforward dependency graph. |
| Package Management | Per-Project | Keep the existing project-local package layout; defer central package management until after migration. |
| Unsupported Packages | Resolve Inline | Handle package compatibility or deprecation findings during the atomic upgrade pass. |
| Unsupported API Handling | Fix Inline | Address the assessed source-incompatible APIs during the same upgrade pass. |
| Test Coverage | Skip | No pre-upgrade test-baseline work was requested or selected. |

## Tasks

### 01-upgrade-projects: Upgrade all projects and resolve compatibility issues

Verify that the .NET 10 SDK/toolchain is available and compatible with the repository configuration, then upgrade `MotorValley.Backend` and `MotorValley.Tests` together from `net8.0` to `net10.0`. Both projects are already SDK-style, so no conversion task is needed. Keep package references per project and update the assessed EF Core packages to the `10.0.12` line across the backend and tests, including `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Sqlite`, and `Microsoft.EntityFrameworkCore.InMemory`.

Resolve package findings inline while preserving behavior: investigate the deprecated `Microsoft.Azure.SignalR` and `xunit` references and document or apply the appropriate compatible resolution. Recompile and fix the seven backend source-compatibility incidents, concentrated in `TimeSpan.FromMinutes(double)` and `TimeSpan.FromSeconds(double)` usage. Restore dependencies and complete one bounded build-and-fix pass for the full solution, keeping the existing project references intact.

**Done when**: Both project files target `net10.0`, package references restore successfully per project, assessed package findings have an explicit resolution, the seven backend API incidents are fixed, and the solution builds with zero errors.

---

### 02-validate-solution: Run final build and existing test validation

Validate the completed atomic upgrade across the full solution after Task 01 succeeds. Run the existing backend test project and confirm that the project reference, EF Core test dependencies, and runtime behavior remain compatible with `net10.0`. Record any deferred package-management or deprecation follow-up without introducing a pre-upgrade test baseline, because test coverage was set to Skip.

**Done when**: The full solution builds cleanly, the existing test suite passes, no dependency conflicts remain, and deferred CPM or package-deprecation follow-up is documented for post-migration work.