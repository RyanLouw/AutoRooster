# School Exam Invigilation Scheduler

Offline Windows x64 application for examination invigilation, built with .NET 10, WPF, MVVM, EF Core/SQLite and OR-Tools CP-SAT. It needs no Excel, database server, web server or cloud account at runtime.

## Build and test

Install the .NET SDK pinned by `global.json` (10.0.402). From this directory:

```powershell
dotnet restore SchoolExamScheduler.sln --locked-mode
dotnet build SchoolExamScheduler.sln --configuration Release --no-restore
dotnet test SchoolExamScheduler.sln --configuration Release --no-build
```

All five test projects must discover tests. Compiler warnings are errors. The tests include real SQLite migrations, transactions, reopen, online backup/restore, exports, solver invariants, realistic 75-teacher scheduling, and an integrated school workflow. A feasible solver result is distinct from proven optimality.

Linux can cross-build WPF but cannot launch it. This cloud machine has `/workspace/tools/dotnet`, which selects the installed SDK and writable CLI/NuGet directories. Use `-m:1 -nr:false --disable-build-servers -p:UseSharedCompilation=false` for builds if worker reuse fails in an isolated runner. Restore accepts `-m:1 -nr:false`.

## Run and publish on Windows

```powershell
dotnet run --project src/SchoolExamScheduler.Desktop
./scripts/Publish-Windows.ps1
./scripts/Test-WindowsStartup.ps1
```

Publishing creates a self-contained `artifacts/win-x64` folder. Copy the **entire folder**, including native libraries and font notices, and run `SchoolExamScheduler.Desktop.exe`. No SDK is needed on the target machine. OR-Tools additionally requires the Microsoft Visual C++ 2015–2022 x64 runtime (MSVCP140.dll, VCRUNTIME140.dll and VCRUNTIME140_1.dll). The Windows publishing script downloads the official redistributable, verifies its Microsoft Authenticode signature, and includes it under prerequisites. On a clean machine run Install-VisualCppRuntime.ps1 once before using the scheduler. The current cloud-built package does not yet include that installer because the Microsoft download destination returned HTTP 403; see docs/Validation.md. The output is a portable application, not an MSI installer. `Test-WindowsStartup.ps1` requires an interactive Windows desktop and checks navigation at 1366×768 and 1920×1080. It opens the application's normal per-user database but never inserts sample data. Run it under a dedicated test Windows account. The included Windows CI workflow has not been executed from this cloud task.

## First use

1. Open Settings, enter the school name and examination-day window, and save. Set limits, rest time, splitting and solver budget as needed.
2. Add subjects, venues and teachers. Weights must be positive (at most 100); use eligibility to exempt a teacher. Blank daily limits use the school default; blank period limits have no cap.
3. In Teacher Availability, enter dates, partial-day restrictions or full-day leave. By default, teachers are available within the examination-day window. Positive entries restrict availability to their union on that date; unavailability always wins.
4. Create each examination with its grade, subject, date and exact same-day times. Add all venues and edit student/supervisor counts. The application prevents over-capacity and conflicting venue allocations.
5. Validate inputs and generate the complete period. Generation runs away from the UI thread and can be cancelled. Review diagnostics when no complete roster is found; hard constraints are never relaxed automatically.
6. Refresh versions in Review Roster, select a version, and load/validate it. Replace or swap teachers, split or combine duties, lock/unlock, remove unlocked duties, or regenerate into a new version. Missing coverage keeps a roster incomplete. Finalization validates coverage and makes a version read-only. Unlock a duty before editing it.
7. Refresh and load a version in Dashboard / Reports. Review teacher minutes, targets, coverage, and calendar filters. Export a complete Excel workbook or a full, individual-teacher, workload or venue PDF. Select a teacher before exporting an individual PDF. Choose new filenames: existing exports are preserved.
8. Create backups in Settings. Restore validates the backup, requires confirmation, retains a recovery copy, and closes the application so stale forms cannot write to restored data. Reopen after restoring.

Refresh a screen after adding related records elsewhere. Historical exams with assignments cannot be modified; copy them to retain roster history. Deactivated teachers' historical duties remain in reports.

For an empty database, Settings offers **Create sample school**, with confirmation. This creates 75 teachers, grades 8–12, overlapping exams, weights and leave across ten dates, using seed 42. It does not run automatically. Tests also include a deliberately infeasible sample.

## Data, backups and privacy

Data is stored in `%LocalAppData%\SchoolExamScheduler\school-exams.db`, with `logs` and `backups` directories alongside it. Upgrading application binaries does not remove these files. Do not distribute a real school database as demonstration data. Automatic backup before roster changes and deletions is optional. Backups must match the current schema; restore rejects incompatible versions instead of risking data loss. Database migrations run before the main window opens, and startup stops on migration failure.

Intervals are half-open `[start,end)`. Times use whole minutes; cross-midnight exams are rejected. Splitting retains exact exam endpoints, lock boundaries and availability transitions, with configurable regular increments, minimum lengths and segment counts. The practical objective limit is 1,000,000 required teacher-minutes per generation. Targets approximate capacity: breaks and indivisible segments can prevent exact attainment. See [architecture and algorithm decisions](docs/Architecture.md).

## Validation status and release acceptance

See [validation evidence](docs/Validation.md) and [Windows acceptance checklist](docs/WindowsAcceptance.md). Linux build/test and Windows package creation do not establish successful Windows startup or visual usability. Complete the Windows checks before deploying to school staff.

The full requested specification is retained in [Requirements.md](docs/Requirements.md). Dependency license metadata and font notices accompany the source; PDFsharp is deliberately pinned to MIT-licensed 6.2.4.
