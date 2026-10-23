# Architecture and implementation gates

The complete requirements are in Requirements.md. Execute its 15 phases in order, with failing tests before implementation and a full Release build/test gate after each phase.

1. Solution, dependency injection, logging and startup.
2. Domain entities and boundary validation.
3. EF Core SQLite migrations and persistence.
4. Scheduling inputs and independent validation.
5. Whole-period CP-SAT feasibility.
6. Continuous split-session coverage.
7. Availability-adjusted weighted fairness.
8–10. Teacher, examination and generation UI.
11. Transactional manual editing and roster history.
12–14. Reporting, exports, backups and settings.
15. Windows publishing and acceptance validation.

## Decisions

- .NET 10 LTS; WPF targets net10.0-windows. Linux can cross-build with EnableWindowsTargeting but cannot execute WPF. Windows startup and visual checks remain mandatory before release.
- Domain and Scheduling remain independent of EF and WPF. Application defines use cases; Infrastructure implements persistence/export; Desktop composes services.
- ViewModels live in the cross-platform Application project to permit Linux unit tests.
- Local school dates and wall times use DateOnly and TimeOnly; intervals are half-open.
- CP-SAT adoption is conditional on native Linux/Windows package smoke tests and license review in phase 5. No greedy fallback.
- No network services or telemetry are required at runtime.
- No demo records are inserted into a user database without confirmation.

## Scheduler model and licensing

OR-Tools 9.15.6755 is Apache-2.0 licensed. Its NuGet package targets .NET 8 and supplies linux-x64 and win-x64 native dependencies. The Linux native runtime is exercised by scheduling tests. Windows native execution must still be tested on Windows. Preserve upstream notices when distributing.

Candidates are exact fixed-time segments. Each atomic position interval has exactly one selected candidate. Teacher conflict cliques include minimum rest. Daily and period minutes are hard linear bounds. Locks force exact candidate selection. Off-grid exam endpoints, availability transitions and lock boundaries are retained; regular candidate handovers use increments from exam start. The solver never truncates examinations. Unsplit short exams may be shorter than the split minimum.

Fairness capacity counts the union of demanded, eligible, available wall-clock minutes, caps each day, then caps the period. It is an upper-bound approximation: minimum breaks and indivisible session lengths can further reduce achievable capacity. Targets normalize weight times capacity.

Optimization uses sequential lexicographic solves: maximum normalized deviation; total absolute deviation; unused eligible teachers; weighted daily imbalance, assignment count and maximum duty count. Earlier proven objective values are fixed before the next objective. FairnessWeight and UnassignedTeacherPenalty enable their respective priority levels; DailyBalanceWeight and DutyCountBalanceWeight scale the final secondary objective. All stages share the configured time budget. Only proof of all stages produces Optimal. An incumbent retained after a timeout is Feasible. Targets use milliminute precision, and normalized objective denominators have a one-minute floor. Reports use unrounded target statistics.

Single-worker CP-SAT, canonical identifier ordering and seeded search support repeatability for completed solves on the same solver/runtime. Wall-time-limited intermediate solutions can differ with machine speed; tests of exact determinism use cases that reach proven optima. A feasibility pass precedes objective auxiliary variables. Fixed-time conflict cliques, disabled redundant symmetry/probing and stronger LP relaxation reduce the realistic-case search cost.

## Offline reports

ClosedXML 0.105.1 writes typed spreadsheet dates and times without Excel. PDFsharp is pinned to 6.2.4, whose NuGet metadata declares MIT licensing and .NET 10 support. Version 6.5.0 uses different licensing and is intentionally not adopted. The patched System.Security.Cryptography.Pkcs 10.0.13 overrides the vulnerable older transitive dependency. DejaVu fonts are embedded; their redistribution notice is retained in Infrastructure/Fonts/LICENSE.txt. PDF output uses A4 landscape pages, wrapped cells, repeated headers and page numbers; long rows continue across pages. Exports are staged to a temporary destination-side file and moved without overwriting existing files.

## Windows native runtime prerequisite

PE import inspection of the OR-Tools Windows DLLs confirms dependencies on MSVCP140.dll, VCRUNTIME140.dll and VCRUNTIME140_1.dll, in addition to the packaged solver DLLs and Windows system libraries. A self-contained .NET deployment does not itself supply these VC runtime names. scripts/Publish-Windows.ps1 downloads Microsoft's x64 redistributable and verifies its Authenticode signature on Windows before including it. The cloud download returned HTTP 403 from download.visualstudio.microsoft.com; a domain addition has been saved in the environment draft. No redistributable or Windows signature-validation step is claimed as completed in the Linux package.


## Fair starting rosters and target caps

The solver keeps its compact whole-exam feasibility seed for large split-enabled requests. Before full CP-SAT fairness optimisation, a bounded incumbent-improvement pass tries deterministic teacher reassignments and valid handovers. Moves must improve the lexicographic workload score (worst relative deviation, total absolute deviation, unused teachers, squared relative deviation, segment count). Eligibility, positive/negative availability, daily/period limits, rest time, exact locks, minimum segment length and segment count remain mandatory. Transfers preserve coverage of the original position; an independent validator checks the final incumbent before use. This is a warm start, not a replacement for the period-wide CP-SAT model or a claim of global optimality. The full model receives the incumbent as hints plus a bound preventing regression in its primary fairness objective.

Capacity-weighted targets now use capped redistribution: assign required teacher-minutes proportionally to weight × capacity, cap shares exceeding a teacher's capacity, then redistribute the remainder across other teachers. If total capacity is insufficient, targets saturate at capacity; target arithmetic does not establish feasibility. Capacity counts the union of demanded wall-clock minutes and applies teacher limits, while the independent scheduling model also handles rest and indivisible segments.

The realistic demonstration is seeded only in the separate `demo-school-exams.db` selected by the explicit `--demo` argument. The normal database is not populated or replaced by this action. The 49-subject catalogue is optional and configurable; adding it is idempotent and preserves matching school subject codes/names.

Editable Excel worksheets are a separate final-adjustment projection. Teacher choices include stable identifiers, calculations refer to edited rows, and simple overlap/time checks are clearly labelled as partial checks. Original report worksheets remain snapshots. Excel changes do not import into the app, modify roster history, recalculate app targets, or replace independent hard-constraint validation.
