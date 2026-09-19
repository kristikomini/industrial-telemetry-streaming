# 02-validate-solution: Run final build and existing test validation

Validate the completed atomic upgrade across the full solution after Task 01 succeeds. Run the existing backend test project and confirm that the project reference, EF Core test dependencies, and runtime behavior remain compatible with `net10.0`. Record any deferred package-management or deprecation follow-up without introducing a pre-upgrade test baseline, because test coverage was set to Skip.

**Done when**: The full solution builds cleanly, the existing test suite passes, no dependency conflicts remain, and deferred CPM or package-deprecation follow-up is documented for post-migration work.

## Confirmed Scope

- Solution: `backend/MotorValley.sln`, containing the SDK-style projects `MotorValley.Backend` and `MotorValley.Tests`.
- Both projects currently target `net10.0`; `MotorValley.Tests` retains its project reference to `MotorValley.Backend`.
- Backend package state includes EF Core `10.0.12`, `Microsoft.Azure.SignalR` `1.33.1`, and stable `SQLitePCLRaw.lib.e_sqlite3` `2.1.12`; the test project uses EF Core InMemory `10.0.12` and xUnit `2.9.3`.
- No `Directory.Packages.props`, `Directory.Build.props`, `global.json`, or `nuget.config` is present in the repository scope, so package versions remain per-project and CPM is deferred.
- Assessment history identifies deprecated-package follow-up for Microsoft.Azure.SignalR and xUnit; current references are retained because the application and tests use them directly. The follow-up is to reassess replacement/CPM after migration, not to introduce new tests or a baseline.
- Decomposition verdict: atomic final validation. The evaluated Execution guidance and `breakdown-hints/common.md` plus `breakdown-hints/test.md` found no stubs, no multi-targeting, no package-replacement batch, and no independent implementation units to split.

## Provider Compatibility Research

- Before this task's edit, `backend/MotorValley.Backend/MotorValley.Backend.csproj` targeted `net10.0`, referenced EF Core `10.0.12`, and requested `Npgsql.EntityFrameworkCore.PostgreSQL` `8.0.10`.
- `Program.cs` uses `UseNpgsql` for the default PostgreSQL path, so the provider reference is runtime-relevant rather than unused metadata.
- NuGet flat-container metadata verified stable `Npgsql.EntityFrameworkCore.PostgreSQL` releases through `10.0.3`. Version `10.0.3` has a `net10.0` dependency group requiring `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.Relational` in `[10.0.4, 11.0.0)`, plus `Npgsql` `10.0.3`; the existing EF Core `10.0.12` satisfies that range.
- Applied change: updated only the backend provider reference to `10.0.3`; restore, warnings-as-errors build, and existing backend tests are now complete.
- The scenario skill root and its Execution/Breakdown Hints files were not present in the workspace-forwarded files; the existing task record contains the prior atomic verdict and the local scope independently confirms one coherent package-reference change.
