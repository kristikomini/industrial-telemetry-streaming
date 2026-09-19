# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: .NET 10 (LTS)

## Upgrade Options
- **Upgrade Strategy**: All-at-Once
- **Package Management**: Per-Project (defer CPM to post-migration)
- **Unsupported Packages**: Resolve Inline
- **Unsupported API Handling**: Fix Inline
- **Test Coverage**: Skip

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Strategy
**Selected**: All-at-Once
**Rationale**: The solution has two low-difficulty, SDK-style projects, both targeting modern .NET 8, with a shallow dependency graph and a bounded set of package and source-compatibility updates.

### Execution Constraints
- Upgrade both projects in one atomic operation; do not introduce dependency-tier or project-by-project phases.
- Keep package references per project during this migration; defer central package management until after the upgrade is complete.
- Resolve unsupported or deprecated packages inline when encountered, including documenting the outcome for Microsoft.Azure.SignalR and xunit where replacement is not required by the assessment.
- Fix the seven backend source-compatibility incidents inline during the same upgrade pass, then restore and perform one bounded build-and-fix pass.
- Run final solution validation only after the atomic upgrade is complete; do not generate a separate test-baseline task because Test Coverage was confirmed as Skip.
