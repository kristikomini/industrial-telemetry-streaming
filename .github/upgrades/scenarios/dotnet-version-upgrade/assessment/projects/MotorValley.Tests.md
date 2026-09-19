# MotorValley.Tests\MotorValley.Tests.csproj

[← Back to the assessment index](../../assessment.md)

## Project Info

- **Current Target Framework:** net8.0
- **Proposed Target Framework:** net10.0
- **SDK-style**: True
- **Project Kind:** DotNetCoreApp
- **Dependencies**: 1
- **Dependants**: 0
- **Number of Files**: 4
- **Number of Files with Incidents**: 1
- **Lines of Code**: 124
- **Estimated LOC to modify**: 0+ (at least 0.0% of the project)

## Related Projects

**Depends on (1)** — projects this one references:

- [c:\Users\krsit\Desktop\Motor-Valley-Sentinel-main\backend\MotorValley.Backend\MotorValley.Backend.csproj](../projects/MotorValley.Backend.md)

## Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["MotorValley.Tests.csproj"]
        MAIN["<b>📦&nbsp;MotorValley.Tests.csproj</b><br/><small>net8.0</small>"]
        click MAIN "../projects/MotorValley.Tests.md"
    end
    subgraph downstream["Dependencies (1)"]
        P1["<b>📦&nbsp;MotorValley.Backend.csproj</b><br/><small>net8.0</small>"]
        click P1 "../projects/MotorValley.Backend.md"
    end
    MAIN --> P1

```

## API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 287 |  |
| ***Total APIs Analyzed*** | ***287*** |  |

## NuGet Package Issues

| Package | Current Version | Suggested Version | Severity | Issue |
| :--- | :---: | :---: | :---: | :--- |
| Microsoft.EntityFrameworkCore.InMemory | 8.0.10 | 10.0.12 | 🟡 Potential | NuGet package upgrade is recommended |
| xunit | 2.9.2 | — | 🔵 Optional | NuGet package is deprecated |

Every project affected by these packages, and the versions the repository settles on: [aggregate NuGet packages](../nuget/aggregate-packages.md).

