# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

Detailed findings live alongside this file in `assessment/`. This page is the index: read it first, then open only the documents you need.

## Table of Contents

- [Executive Summary](#executive-summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Detailed Reports](#detailed-reports)
  - [Projects Relationship Graph](assessment/project-graph.md)
  - [Aggregate NuGet packages details](assessment/nuget/aggregate-packages.md)
  - [Most Frequent API Issues (complete list)](assessment/api-issues/most-frequent-api-issues.md)
  - [Project Details](#project-details)

## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 2 | All require upgrade |
| Total NuGet Packages | 101 | 6 need upgrade |
| Total Code Files | 25 |  |
| Total Code Files with Incidents | 7 |  |
| Total Lines of Code | 1110 |  |
| Total Number of Issues | 15 |  |
| Proposed Target Framework | net10.0 |  |
| Estimated LOC to modify | 7+ | at least 0.6% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Test Coverage | Package Issues | API Issues | Binding Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| [MotorValley.Backend\MotorValley.Backend.csproj](assessment/projects/MotorValley.Backend.md) | net8.0 | 🟢 Low | 🧪 Recommended | 4 | 7 | 0 | 7+ | AspNetCore, Sdk Style = True |
| [MotorValley.Tests\MotorValley.Tests.csproj](assessment/projects/MotorValley.Tests.md) | net8.0 | 🟢 Low | — | 2 | 0 | 0 |  | DotNetCoreApp, Sdk Style = True |

🧪 **Test Coverage** — projects risky enough to add behavior-locking tests before upgrading, to catch regressions the upgrade may introduce. Requires the **dotnet-test** plugin.

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 95 | 94.1% |
| ⚠️ Incompatible | 2 | 2.0% |
| 🔄 Upgrade Recommended | 4 | 4.0% |
| ***Total NuGet Packages*** | ***101*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 7 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 1595 |  |
| ***Total APIs Analyzed*** | ***1602*** |  |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |
| M:System.TimeSpan.FromMinutes(System.Double) | 4 | 57.1% | Source Incompatible |
| M:System.TimeSpan.FromSeconds(System.Double) | 3 | 42.9% | Source Incompatible |

The table above is the top 10. See [the complete list](assessment/api-issues/most-frequent-api-issues.md) for every affected API.

## Detailed Reports

- [Projects Relationship Graph](assessment/project-graph.md)
- [Aggregate NuGet packages details](assessment/nuget/aggregate-packages.md)
- [Most Frequent API Issues (complete list)](assessment/api-issues/most-frequent-api-issues.md)

### Project Details

- [MotorValley.Backend\MotorValley.Backend.csproj](assessment/projects/MotorValley.Backend.md)
- [MotorValley.Tests\MotorValley.Tests.csproj](assessment/projects/MotorValley.Tests.md)


