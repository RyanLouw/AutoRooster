# Solver timeout fix — 23 October 2026

Locked restore and the full Release solution build passed with zero warnings/errors. All **108 tests passed**, with none failed or skipped: Application 26, Domain 16, Infrastructure 20, Integration 5, Scheduling 41.

The five-second 75-teacher sample with thirty-minute handovers reproduced `TimeLimitWithoutSolution` before the fix. It now returns a complete roster that passes the independent validator at both five- and thirty-second limits. These limits are regression scenarios, not a guarantee for every school or machine.

Large split-enabled requests first try a smaller whole-exam feasibility model with up to 20% of the total budget. A valid result seeds the full split-enabled fairness search. Failure of that restricted model does not prove the full request infeasible: the solver falls back to the full model, including partial availability and locked handovers. Without an incumbent, the full model can use the remaining time for coverage before constructing fairness objectives. The shared clock includes seed search and model construction. No settings or hard constraints are relaxed.

Regression coverage also exercises twenty-teacher requests requiring splits and exact off-grid locked handovers, so the seed cannot suppress required splitting. Windows GUI testing remains unavailable in this Linux environment.

---

# Report and handover update — 23 October 2026

The full Release WPF cross-build passed with zero warnings/errors. All **106 tests executed and passed**, with zero failed or skipped: Application 26, Domain 16, Infrastructure 20, Integration 4, Scheduling 40. Machine-readable results are in `TestResults/reports-handovers-final` in the execution workspace.

New coverage verifies thirty-minute handovers in Grades 8, 9, 10, 11 and 12; distinct continuous coverage for two simultaneous invigilator positions; fairness choosing half-hour handovers when teachers are fully available; and generation of the actual 75-teacher sample with splitting enabled. The full-school handover scenario returned a complete result and passed independent hard-constraint validation; mathematical optimality is not claimed for a time-limited feasible result.

Excel tests reopen the teacher-hours summary and daily timeline, verify decimal-hour and `hh:mm` totals, check exact handover times, include teachers with zero duties, and ensure two simultaneous positions total two teacher-hours per hour rather than double-counting handover boundaries. PDF extraction verifies teacher totals, split-session times, detailed fairness statistics and the combined report book. ZIP tests verify all report types and every individual teacher PDF, cancellation safety, and preservation of existing files. ViewModel tests verify the explicit handover preset confirmation and export commands.

Existing settings/rosters are preserved. The default for newly created settings is a thirty-minute minimum; existing schools can choose **Enable 30-minute handovers (all grades)** and then generate a new roster version. Splitting applies to every grade, including Grades 8 and 9, as clarified by the user. The setting controls permitted handovers and does not force unnecessary splits. Existing boundary tests were moved to a fifteen-minute invalid segment so they continue checking a length below the newly requested minimum.

The uploaded PDF was examined as a report sample, not as instructions. It showed the demo's full-roster export with splitting disabled. New report exports are tested using the real exporters. Windows UI interaction and visual checks remain unrun on this Linux machine; previous release-prerequisite limitations below still apply.

---

# Validation evidence — 22 October 2026

## Current status

The application source, tests, Windows publishing scripts and documentation have been implemented. Linux Release build and all automated tests pass. A Windows x64 executable folder was cross-published successfully. **Phase 15 release acceptance is incomplete**: the Visual C++ prerequisite installer could not be downloaded (HTTP 403), and this Linux machine cannot execute or visually validate WPF.

Do not describe this candidate as verified for school deployment until the Windows acceptance checks pass.

## Executed checks

- .NET SDK 10.0.402 and runtime 10.0.13 installed; EF CLI 10.0.13 installed.
- Frozen `dotnet restore --locked-mode` exercised through the reusable cloud setup script.
- Full Release solution build: **success, 0 warnings, 0 errors**.
- Five test projects: **80 discovered/executed, 80 passed, 0 failed, 0 skipped**.

| Test project | Passed |
| --- | ---: |
| Application | 15 |
| Domain | 16 |
| Infrastructure | 15 |
| Integration | 3 |
| Scheduling | 31 |

Tests cover half-open intervals, availability precedence, capacity, independent positions, limits, breaks, locks, splitting, exact off-grid boundaries, target arithmetic, zero targets, determinism on proven cases, 12 seeded randomized scenarios, 75-teacher scheduling versus a feasible baseline, SQLite migrations/constraints/rollback/reopen, backup/restore, corrupt-backup rejection, structured logs, typed Excel content, PDF extraction, repeated headings and text bounds. The real SQLite acceptance test creates the 75-teacher demo school, generates and regenerates a roster while preserving a lock, exports reports, reopens the database, and restores a backup. An upgrade test preserves records from the initial schema. Historical workload remains visible after a teacher is deactivated.

These are meaningful checks, not proof that every possible input or UI workflow is correct. Test counts exclude iterations inside randomized tests.

## TDD and gates

New phase tests were written before the corresponding implementation and executed to expose missing functionality or behavioral failures. Missing public types initially produced compile failures in the red step. Splitting, realistic fairness, independent venue validation, archived-exam handling and historical workload also exposed behavioral failures before correction. Failed gates were investigated and rerun before later phases. Initial vulnerable dependency selections were replaced without suppressing advisories or compiler warnings.

Recorded phase totals after passing available gates: 1: 5; 2: 20; 3: 23; 4: 30; 5: 41; 6: 45; 7: 51; 8: 54; 9: 56; 10: 59; 11: 62; 12: 63; 13: 70; 14: 76; final automated suite: 80.

MSBuild worker reuse eventually failed in the isolated runner. Single-node builds with build servers disabled resolved that environmental failure. No checks or assertions were removed.

## Windows package

`dotnet publish src/SchoolExamScheduler.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:UseSharedCompilation=false --disable-build-servers -m:1 -nr:false -o artifacts/win-x64` succeeded after correcting the WPF project's runtime identifiers to Windows only.

Inspected assets include the executable, application DLL, .NET host/runtime, OR-Tools native bridge/solver/dependencies, SQLite native library, and font notice. The .NET runtime is bundled. PE import inspection confirmed external Microsoft VC runtime dependencies `MSVCP140.dll`, `VCRUNTIME140.dll`, and `VCRUNTIME140_1.dll`; these are not supplied by this cross-published folder.

The official redistributable URL redirected to `download.visualstudio.microsoft.com`, which returned HTTP 403. The environment draft now adds this domain. After reviewing/saving that network change, retry downloading the official installer. The Windows publishing script includes it only after a valid Microsoft Authenticode check. That download/signature step is unrun here; no license or signature checks were bypassed.

The source archive and Windows candidate archive are generated under `artifacts`. The candidate includes a prerequisite notice. It is usable only on Windows machines already providing the required VC runtime until the installer can be bundled.

## Unrun release requirements

- Windows process launch and automated navigation smoke script.
- Visual and interaction validation at the required resolutions and DPI settings.
- Offline execution on a clean Windows machine without development tools, after installing the VC prerequisite.
- Actual printer/Excel viewer checks and the complete manual acceptance scenario.
- GitHub Windows CI execution; workflow files are present but no repository push or workflow run was performed.
- Cloud environment publication and restoration into a fresh task.

See WindowsAcceptance.md. Cloud `install_script`, `start_skill` and the Microsoft download-domain addition were saved as a draft. Saving did not apply network changes or publish the environment. Review and save the changes in environment settings, then publish the prepared environment.
