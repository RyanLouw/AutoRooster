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
6. Open Review Roster and select a version; its duties and validation load automatically. Replace or swap teachers, split or combine duties, lock/unlock, remove unlocked duties, or regenerate into a new version. Missing coverage keeps a roster incomplete. Finalization validates coverage and makes a version read-only. Unlock a duty before editing it.
7. Open Dashboard / Reports and select a version; its report loads automatically. Review teacher minutes, targets, coverage, and calendar filters. Export a complete Excel workbook or a full, individual-teacher, workload or venue PDF. Select a teacher before exporting an individual PDF. Choose new filenames: existing exports are preserved.
8. Create backups in Settings. Restore validates the backup, requires confirmation, retains a recovery copy, and closes the application so stale forms cannot write to restored data. Reopen after restoring.

Screens refresh automatically when opened after adding related records elsewhere. Historical exams with assignments cannot be modified; copy them to retain roster history. Deactivated teachers' historical duties remain in reports.

For an empty database, Settings offers **Create sample school**, with confirmation. This creates 75 teachers, grades 8–12, overlapping exams, weights and leave across ten dates, using seed 42. It does not run automatically. Tests also include a deliberately infeasible sample.

## Start the next test series and change staff

When the current series is finished:

1. Open **Settings → Start a new test series**.
2. Under **Teachers to keep**, leave ticks only beside teachers who will remain. **Keep all** and **Keep none** help select a group quickly.
3. Choose **Start new test series** and confirm. The old exams and roster versions disappear from the normal working lists; unticked teachers leave the active staff list. Old duty hours do not carry over. Subjects, venues, settings and dated availability are retained.
4. In **Teachers**, use **New teacher → Save teacher** to add staff. Select a current teacher to edit their details, weight, limits or responsibilities and save. **Deactivate** removes a teacher from current staff/future rosters. Untick **Active only** if you need to find and reactivate previous staff.
5. Add the next series' exams and venue allocations in **Examinations**, then generate a newly named roster. Only the new active exams and current staff determine its workload targets and hours.

Previous data is kept outside the normal lists. Enable **Show previous series** in Review or Reports to view/export read-only older results, or in Examinations to select an old exam and **Copy** it with a new date. Historical reports preserve the names, duty hours, targets and validation results captured when the series was finished, so later staff changes do not rewrite them. Incomplete old versions retain their recorded coverage issues. The optional automatic-backup setting also applies when finishing a series.

Dated availability is retained: earlier leave does not apply to different dates, but update/add entries for the next series. Exempt current teachers by clearing Can invigilate or deactivate staff who have left; do not delete teachers referenced by previous results.

## Realistic demo and fair teacher-hour targets

Use **Settings → Open realistic demo (separate database)**. This opens another Desktop window with its own saved database, `demo-school-exams.db`; your normal school data is untouched. You can also run `SchoolExamScheduler.Desktop.exe --demo`. The first opening seeds 75 uniquely named teachers, 49 configurable subjects, Grades 8–12, varied exam lengths, multiple simultaneous invigilators per room, leave, partial availability and reduced workload weights. Later openings preserve your test edits. This is a broad South African secondary-school starting catalogue, not a claim about every school's curriculum. **Subjects → Add school subject catalogue** adds missing catalogue subjects to your school without replacing existing subjects; rename, add or deactivate entries as needed.

1. In the demo, open **Generate Roster**. **Teacher hour targets** shows the period's equal-share average and each teacher's adjusted target before generation.
2. Generate a baseline. Review assigned hours beside targets, unused available teachers and the largest relative deviation. A complete result may be feasible without being mathematically optimal.
3. In **Teacher Availability**, mark a teacher unavailable on a date (or part of a day), give a reason and save. Positive availability entries restrict that date; negative entries take precedence.
4. In **Teachers**, give a teacher with other responsibilities a weight such as **0.5** instead of **1.0**, explain the responsibility in Notes and save. A lower weight reduces their proportional share; it does not exempt them. Use eligibility to exempt a teacher entirely, or daily/period limits for hard caps.
5. Return to Generate Roster: targets recalculate automatically. Generate a **new version** to redistribute actual duties and compare the reports. Existing rosters remain available for comparison.

Targets divide required supervision using workload weight × available capacity, cap each teacher's target at their capacity, and redistribute excess among remaining teachers. A fully unavailable teacher gets a zero target. Exact target hours may be impossible because of coverage, locks, breaks and allowed segment lengths. The scheduler improves a complete starting roster using constraint-checked reassignments and handovers, then continues CP-SAT optimisation; full-model search cannot worsen that starting roster's primary fairness bound.

## Final manual adjustments in Excel

Choose **Export Excel workbook**, or obtain `Reports.xlsx` from **Export ALL reports (ZIP)**. No Excel installation is needed to create it; use Excel to make final manual changes.

- Read **Start here**, then edit the blue teacher dropdowns in **Editable roster**. Employee numbers or unique IDs distinguish teachers sharing a name. Swap two duties by changing both teacher cells.
- **Editable hours** recalculates totals, daily hours and differences from the exported targets. Changing blue Start/End cells also updates durations. Check messages flag invalid times, unknown teachers and teacher overlaps/short breaks.
- Keep the date, venue, position and times when simply swapping teachers. If you change times, review continuity yourself. The Excel checks do not validate leave, limits, locks or complete coverage.
- The other report sheets remain the original snapshots for comparison. Edit existing rows only; adding/deleting rows or overwriting formulas can break the calculations. Keep Excel calculation set to Automatic and save a copy.
- Excel changes are **final manual adjustments only** and do not import into the app. To recalculate targets or produce matching app/PDF reports, change availability/weights in the app, generate a new version and export again.

## Teacher hours, timelines and all-report export

In **Settings**, choose **Enable 30-minute handovers (all grades)** and confirm. This enables splitting, sets the minimum session and regular handover grid to 30 minutes, and permits at least three segments per invigilator position. It applies to every grade, including Grades 8 and 9. You can still change these limits individually. Existing saved settings and rosters are preserved until you choose this action; generate a new roster version afterwards.

In an examination's venue allocation, **Invigilators at once** is the number of teachers who must be present simultaneously. A value of 2 requires two independently covered positions. A handover changes the teacher within one position: for example, teacher A at 08:00–08:30 followed by teacher B at 08:30–09:00. Fairness and availability can lead the solver to split sessions; it does not force a handover in every exam when one is unnecessary. A 30-minute duty is 0.50 hours, not 0.30 hours.

In **Dashboard / Reports**, select the new version; its report loads automatically:

- **Export teacher hours (PDF)** gives every teacher's total hours, `hh:mm` total, target and duty count across the selected examination period, including teachers with no assigned duties.
- **All teacher timetables** gives each teacher's dated duties, venue, simultaneous position, exact handover times, duration, hours and split/whole-exam status.
- **Export ALL reports (ZIP)** includes the Excel workbook, combined PDF, separate teacher-hours/timetable/examination/venue/calendar/unfilled-duty PDFs, and an individual PDF for every teacher.
- The Excel workbook includes **Teacher hours** (with daily hours), **Teacher timeline**, the existing detailed workload statistics, roster/calendar/venue sheets, and individual teacher sheets.

Exports use the entire selected roster version, regardless of on-screen filters. Reported totals count teacher time: two simultaneous invigilators working for one hour represent two teacher-hours. Changing settings cannot add handovers to an old export; regenerate the roster first.

## Data, backups and privacy

Data is stored in `%LocalAppData%\SchoolExamScheduler\school-exams.db`, with `logs` and `backups` directories alongside it. Upgrading application binaries does not remove these files. Do not distribute a real school database as demonstration data. Automatic backup before roster changes and deletions is optional. Backups must match the current schema; restore rejects incompatible versions instead of risking data loss. Database migrations run before the main window opens, and startup stops on migration failure.

Intervals are half-open `[start,end)`. Times use whole minutes; cross-midnight exams are rejected. Splitting retains exact exam endpoints, lock boundaries and availability transitions, with configurable regular increments, minimum lengths and segment counts. The practical objective limit is 1,000,000 required teacher-minutes per generation. Targets approximate capacity: breaks and indivisible segments can prevent exact attainment. See [architecture and algorithm decisions](docs/Architecture.md).

## Validation status and release acceptance

See [validation evidence](docs/Validation.md) and [Windows acceptance checklist](docs/WindowsAcceptance.md). Linux build/test and Windows package creation do not establish successful Windows startup or visual usability. Complete the Windows checks before deploying to school staff.

The full requested specification is retained in [Requirements.md](docs/Requirements.md). Dependency license metadata and font notices accompany the source; PDFsharp is deliberately pinned to MIT-licensed 6.2.4.
