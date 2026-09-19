# 01-upgrade-projects: Upgrade all projects and resolve compatibility issues

Verify that the .NET 10 SDK/toolchain is available and compatible with the repository configuration, then upgrade `MotorValley.Backend` and `MotorValley.Tests` together from `net8.0` to `net10.0`. Both projects are already SDK-style, so no conversion task is needed. Keep package references per project and update the assessed EF Core packages to the `10.0.12` line across the backend and tests, including `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Sqlite`, and `Microsoft.EntityFrameworkCore.InMemory`.

Resolve package findings inline while preserving behavior: investigate the deprecated `Microsoft.Azure.SignalR` and `xunit` references and document or apply the appropriate compatible resolution. Recompile and fix the seven backend source-compatibility incidents, concentrated in `TimeSpan.FromMinutes(double)` and `TimeSpan.FromSeconds(double)` usage. Restore dependencies and complete one bounded build-and-fix pass for the full solution, keeping the existing project references intact.

## Research Findings

- **Projects in scope:** `backend/MotorValley.Backend/MotorValley.Backend.csproj` and `backend/MotorValley.Tests/MotorValley.Tests.csproj`; both are SDK-style and currently target `net8.0`.
- **Dependency shape:** `MotorValley.Tests` references `MotorValley.Backend`; update the backend first logically, then validate the dependent test project in the same atomic change. No other project references or package-management files were found.
- **Package definition:** Package versions are declared directly in each project file. No `Directory.Packages.props`, `Directory.Build.props`, `global.json`, or `nuget.config` was found in the repository scope.
- **Backend package actions:** update `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, and `Microsoft.EntityFrameworkCore.Sqlite` from `8.0.10` to `10.0.12`; update deprecated `Microsoft.Azure.SignalR` from `1.28.0` to the assessment-recommended `1.33.1`. The package is used by `Program.cs` through `AddAzureSignalR`, so it must remain referenced.
- **Test package actions:** update `Microsoft.EntityFrameworkCore.InMemory` from `8.0.10` to `10.0.12`; update deprecated `xunit` from `2.9.2` to the assessment-recommended `2.9.3`. The tests use `Xunit` directly, so the package must remain referenced. Existing project reference and runner packages stay intact.
- **API incidents:** the assessment reports four `TimeSpan.FromMinutes(double)` and three `TimeSpan.FromSeconds(double)` incidents in `AlertsController.cs`, `AlertIngestionService.cs`, `InMemoryCacheService.cs`, `RedisCacheService.cs`, and `AlertConsumerWorker.cs`. The current calls use integer literals; make the `double` argument explicit while preserving the existing durations and behavior.
- **Toolchain:** installed SDKs are `10.0.103` and `10.0.401`; use the current `10.0.401` SDK selected by the environment. The projects have no resource/XAML/COM or classic .NET Framework requirements, so `dotnet build` is the appropriate validator.
- **Stubs:** no `// STUB:` markers were found in the backend projects.

## Execution Decision

This task is **atomic**. It affects two tightly coupled SDK-style projects in one dependency chain, the selected strategy explicitly requires an all-at-once upgrade, and the test project must remain synchronized with its backend project reference. No decomposition hint condition requiring a split applies: there are no stubs, no .NET Framework migration, no Windows-only API isolation, and only four package updates plus two deprecated-package version resolutions.

**Done when**: Both project files target `net10.0`, package references restore successfully per project, assessed package findings have an explicit resolution, the seven backend API incidents are fixed, and the solution builds with zero errors.

## Retry Research (2026-09-19)

- **Current repository evidence:** The requested implementation edits are already present in the working tree. Both SDK-style projects target `net10.0`; the backend has EF Core, Design, and SQLite at `10.0.12` plus `Microsoft.Azure.SignalR` at `1.33.1`; the test project has EF Core InMemory at `10.0.12` plus `xunit` at `2.9.3`.
- **Source compatibility evidence:** All seven assessed calls are now explicit `double` literals: four `TimeSpan.FromMinutes(5d)` calls and three `TimeSpan.FromSeconds(2d/5d)` calls across the five assessed backend files. No `// STUB:` markers remain.
- **Assessment consultation:** The per-project assessment confirms four backend package/API findings and two test package findings; the package resolutions and seven source fixes above match that inventory. The assessment query surface was also attempted with absolute project paths but is unavailable for these generated artifacts, so the checked-in project reports and root assessment remain the source of record.
- **Decomposition assessment:** The Execution stage, `breakdown-hints/common.md`, and `breakdown-hints/test.md` were evaluated. The task remains atomic: two tightly coupled SDK-style projects, one project-reference edge, no stubs, no framework migration, no multi-targeting, and no package replacement requiring a research/implementation chain.
- **Validation plan:** Verify the installed .NET 10 SDK, restore/build `MotorValley.sln`, then run the backend test project. Record any package or API failures in `progress-details.md` before completion.
