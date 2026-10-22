# CODEX MASTER PROMPT
# School Exam Invigilation Scheduler

## ROLE AND OBJECTIVE

You are a senior C#/.NET architect, optimization engineer, WPF developer, and test automation engineer.

Build a complete, reliable, offline Windows desktop application named:

**School Exam Invigilation Scheduler**

The application will help a South African school distribute examination supervision duties fairly among approximately 75 teachers.

The application must be suitable for actual school use, not merely a demonstration or prototype.

Implement the project from an empty repository through working, tested, publishable software.

Do not ask me to manually create project files, implement features, or fill in missing code.

Make reasonable technical decisions when requirements are not explicitly defined. Document those decisions.

Do not quietly reduce scope, skip functionality, or replace real implementations with fake functionality.

---

# 1. NON-NEGOTIABLE DEVELOPMENT RULES

Follow Test-Driven Development (TDD).

For every implementation phase:

1. Define the expected functionality.
2. Write meaningful automated tests before implementing the functionality.
3. Run the tests and confirm that they fail for the expected reason.
4. Implement the smallest correct solution.
5. Run the tests again.
6. Refactor where needed.
7. Rerun the tests.
8. Build the entire solution.
9. Run all existing tests.
10. Continue only when the phase passes all applicable quality gates.

## STRICT FAILURE GATE

**NEVER proceed to a new implementation phase if any test or build fails.**

When a failure occurs:

- Investigate the root cause.
- Correct the implementation.
- Re-run affected tests.
- Re-run all existing tests.
- Confirm the full solution builds successfully.
- Only then proceed.

Never delete or weaken a test merely to make the build pass.

Never change expected values to match incorrect implementation behavior.

Never disable warnings, remove validation, or bypass failing functionality.

Do not use empty placeholder tests that always pass.

Do not silently catch exceptions.

Do not claim success without executing verification commands and inspecting their results.

If blocked by an environmental issue, missing SDK, unavailable package, or failure that cannot be resolved safely, stop and report the blocker precisely.

Do not proceed to the next phase with an unverified or failing implementation.

## Important execution limitation

A passing test suite is a necessary gate, not proof that the whole application is flawless.

Use additional integration tests, usability checks, application startup smoke tests, and manual UI validation where automation cannot provide adequate coverage.

Do not claim the desktop UI was visually verified if no actual UI validation was performed.

---

# 2. TECHNOLOGY STACK

Use:

- C#
- Latest stable, supported .NET SDK compatible with the available development environment
- WPF
- MVVM architecture
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.Logging
- Entity Framework Core
- SQLite
- xUnit
- FluentAssertions or native xUnit assertions
- ClosedXML for Excel exports
- QuestPDF or another suitable supported PDF library

For scheduling optimization, prefer:

**Google OR-Tools CP-SAT for .NET**

Verify the current package compatibility, supported runtime, native dependency deployment, and license requirements before adopting it.

If OR-Tools is not viable, choose another suitable optimization approach and document why.

Do not replace the optimization requirement with a naive alphabetical or round-robin algorithm.

The application must:

- Run on Windows.
- Work offline.
- Require no SQL Server installation.
- Require no web server.
- Require no cloud service.
- Require no Microsoft Excel installation.
- Preserve data when closed.
- Be usable by nontechnical school staff.

The application should be installable or publishable as a Windows application.

---

# 3. SOLUTION ARCHITECTURE

Create this solution:

```text
SchoolExamScheduler.sln
│
├── src/
│   ├── SchoolExamScheduler.Desktop/
│   ├── SchoolExamScheduler.Domain/
│   ├── SchoolExamScheduler.Application/
│   ├── SchoolExamScheduler.Infrastructure/
│   └── SchoolExamScheduler.Scheduling/
│
├── tests/
│   ├── SchoolExamScheduler.Domain.Tests/
│   ├── SchoolExamScheduler.Application.Tests/
│   ├── SchoolExamScheduler.Infrastructure.Tests/
│   ├── SchoolExamScheduler.Scheduling.Tests/
│   └── SchoolExamScheduler.Integration.Tests/
│
└── README.md
```

Responsibilities:

**Domain**
- Entities
- Value objects
- Enumerations
- Core business rules
- Domain validation

**Application**
- Use cases
- Services
- Application interfaces
- Input validation
- Scheduling orchestration
- Reporting orchestration

**Infrastructure**
- SQLite
- EF Core
- Migrations
- Persistence implementations
- Backup and restore
- File exporting

**Scheduling**
- Optimization solver
- Hard constraints
- Fairness objective
- Scheduling result models
- Conflict detection
- Scheduling diagnostics

**Desktop**
- WPF views
- ViewModels
- Commands
- UI validation
- Navigation
- Charts and tables

Keep scheduling logic independent of the database and WPF interface.

Do not place business logic directly in WPF code-behind.

Use dependency injection.

Use asynchronous database and file operations where appropriate.

Use cancellation tokens for longer-running operations.

Do not introduce microservices, a backend web API, authentication infrastructure, or unnecessary network dependencies.

---

# 4. MAIN BUSINESS REQUIREMENTS

The school schedules examinations across multiple grades, subjects, dates, times, and venues.

Example:

Grade 8 Mathematics:
- Date: 10 December
- Time: 08:00–10:00
- Number of venues: 8
- Each venue requires one invigilator

Grade 9 English:
- Date: 10 December
- Time: 08:00–10:00
- Number of venues: 6
- Each venue requires one invigilator

Grade 10:
- Some students write Information Technology.
- Some students write Geography.
- Some students write Business Studies.
- Different subjects may have different durations.
- Different exams may occur simultaneously.
- Not all students in a grade necessarily take the same subject.

The user enters the exam timetable and venue allocations.

The software generates the teacher supervision roster.

**The software is not responsible for deciding which subject a student must write or generating individual student exam timetables.**

It only schedules teacher invigilation based on the examination information provided.

---

# 5. SQLITE DATABASE

Use Entity Framework Core with SQLite.

The database must be stored in an appropriate per-user application data directory, not inside a protected Program Files directory.

For example:

```text
%LocalAppData%\SchoolExamScheduler\
    school-exams.db
    logs\
    backups\
```

Use EF Core migrations.

Create and migrate the database on first launch through a controlled, error-handled startup process.

If migration fails, inform the user and do not launch into a partially functional state.

Ensure database files and backups are preserved during application upgrades.

Implement backup and restore functionality with appropriate validation and safeguards.

Do not delete or overwrite existing user data without explicit confirmation.

Enable foreign-key enforcement.

Use appropriate unique constraints, indexes, check constraints, and transactions.

Store calendar dates and wall-clock times using consistently defined types.

Store time intervals using half-open semantics:

```text
[start, end)
```

Two intervals overlap only when:

```text
startA < endB
AND
startB < endA
```

Handle DateOnly and TimeOnly conversions explicitly and test SQLite persistence behavior.

---

# 6. DOMAIN ENTITIES

Create the following entities and supporting configurations.

## 6.1 Teacher

Properties:

```text
Id
FirstName
LastName
EmployeeNumber (optional)
Email (optional)
IsActive
CanInvigilate
WorkloadWeight
MaximumDailyMinutes (optional)
MaximumPeriodMinutes (optional)
Notes
```

WorkloadWeight indicates the relative share of invigilation duties.

Default:

```text
WorkloadWeight = 1.0
```

Examples:

```text
Teacher A: 1.0
Teacher B: 1.0
Teacher C: 0.5
Teacher D: 1.5
```

A teacher with a weight of 0.5 should normally receive approximately half the workload of an otherwise equally available teacher with a weight of 1.0.

A weight of 1.5 represents an increased target, not permission to violate daily or period limits.

Weights must be positive and validated.

A teacher with no invigilation duties should be made ineligible using CanInvigilate rather than an invalid workload weight.

## 6.2 TeacherAvailability

Properties:

```text
Id
TeacherId
Date
StartTime
EndTime
IsAvailable
Reason
```

Allow:

- Full-day availability.
- Partial-day availability.
- Full-day unavailability.
- Partial-day unavailability.
- Leave.
- School duties or other scheduling restrictions.

Define a clear default availability policy.

Default assumption:

Teachers are available during the configured examination-day window unless they have an explicit unavailability record.

Explicit unavailability must take precedence over default availability.

Reject ambiguous contradictory records or define a deterministic precedence rule and test it.

## 6.3 Subject

```text
Id
Name
SubjectCode
IsActive
```

## 6.4 Venue

```text
Id
Name
Capacity
IsActive
Notes
```

Support different venue capacities.

## 6.5 Exam

```text
Id
SubjectId
Grade
ExamDate
StartTime
EndTime
Description
IsActive
```

Store times separately from the examination date, with clear validation.

For version one, examinations must start and finish on the same calendar date.

Reject cross-midnight exam times unless support is intentionally implemented later.

## 6.6 ExamVenue

```text
Id
ExamId
VenueId
ExpectedStudentCount
RequiredInvigilatorCount
```

Each examination may have multiple venues.

Each venue may need one or more teachers.

Prevent a venue from being scheduled for two overlapping exams.

Validate student counts against venue capacity.

## 6.7 InvigilationAssignment

```text
Id
RosterVersionId
ExamVenueId
TeacherId
StartTime
EndTime
IsManual
IsLocked
AssignmentStatus
Notes
```

Assignments must support split sessions.

Do not confuse an ExamVenue with an individual required invigilation position.

If a venue needs two simultaneous supervisors, the scheduler must cover two independent supervision positions throughout the exam.

Model required coverage explicitly in the scheduling domain, even if it is derived from ExamVenue.

## 6.8 RosterVersion

```text
Id
Name
CreatedOn
Status
SchedulingSeed
AlgorithmVersion
SettingsSnapshot
```

Suggested statuses:

```text
Draft
Incomplete
Validated
Finalized
```

Assignments belong to roster versions.

Never overwrite a finalized roster through automatic regeneration.

Maintain historical versions for review and export.

## 6.9 SchedulingSettings

Include:

```text
DefaultDayStartTime
DefaultDayEndTime
MinimumBreakMinutes
DefaultMaximumDailyMinutes
AllowSessionSplitting
MinimumSessionMinutes
MaximumSessionSegmentsPerPosition
SchedulingTimeIncrementMinutes
FairnessWeight
DailyBalanceWeight
DutyCountBalanceWeight
UnassignedTeacherPenalty
SolverTimeLimitSeconds
SchedulingSeed
```

Use sensible configurable defaults.

Do not silently insert breaks or extend exam times.

Store settings persistently.

---

# 7. USER INTERFACE

Create a polished WPF desktop interface.

Navigation:

```text
Dashboard
Teachers
Teacher Availability
Subjects
Venues
Examinations
Generate Roster
Review Roster
Reports
Settings
```

Use CommunityToolkit.Mvvm.

Implement:

- Searchable tables.
- Sorting.
- Filtering.
- Data validation.
- Date pickers.
- Time editing controls.
- Clear error messages.
- Confirmation dialogs for destructive actions.
- Loading indicators.
- Progress and cancellation for roster generation.
- Responsive layout when the window is resized.

The UI should work at common laptop resolutions, including 1920x1080 and 1366x768.

Avoid unnecessarily complex custom visual controls.

## Teacher Screen

Support:

- Create teacher.
- Edit teacher.
- Deactivate teacher.
- Configure workload weight.
- Configure maximum daily and total supervision minutes.
- Configure eligibility.
- View allocated workload.
- View teacher duties.

## Availability Screen

Support:

- Mark full dates unavailable.
- Mark partial-day unavailability.
- Edit and remove availability entries.
- View availability in calendar and table formats.

## Subject Screen

Support subject creation, editing, and activation/deactivation.

## Venue Screen

Support venue names, capacities, and availability.

## Examination Screen

The user must be able to create an examination by entering:

```text
Grade
Subject
Date
Start time
End time
Venues
Number of students per venue
Required teachers per venue
```

Allow multiple examination venues to be added in a single form.

Provide an examination list and calendar.

Allow copying an existing examination.

Validate examination configuration before saving.

## Generation Screen

Provide:

```text
Generate Roster
Validate Inputs
Cancel Generation
```

Display:

- Number of examinations.
- Total venue positions.
- Total required supervision minutes.
- Eligible teachers.
- Expected constraints.
- Generation status.
- Solver result status.

Do not freeze the WPF interface while generating.

---

# 8. AUTOMATIC SCHEDULING ENGINE

This is the most important part of the application.

Implement a constraint-aware optimization engine.

**Do not build a simple first-available-teacher or alphabetical assignment algorithm.**

The scheduler must consider the complete examination period.

## 8.1 Scheduling Input

Design a clean input model:

```csharp
public sealed record SchedulingRequest
{
    public required IReadOnlyList<SchedulingTeacher> Teachers { get; init; }

    public required IReadOnlyList<SchedulingExam> Exams { get; init; }

    public required IReadOnlyList<SchedulingVenue> Venues { get; init; }

    public required SchedulingConfiguration Configuration { get; init; }

    public IReadOnlyList<LockedAssignment> LockedAssignments { get; init; }
        = [];
}
```

You may improve the structure if required.

The engine must be independently testable.

## 8.2 Scheduling Output

Return a structured result:

```csharp
public sealed record SchedulingResult
{
    public required SchedulingStatus Status { get; init; }

    public required IReadOnlyList<ScheduledAssignment> Assignments
        { get; init; }

    public required IReadOnlyList<SchedulingIssue> Issues
        { get; init; }

    public required FairnessStatistics Fairness
        { get; init; }
}
```

Distinguish:

```text
Optimal
Feasible
Infeasible
TimeLimitWithoutSolution
Cancelled
InvalidInput
SolverError
```

Use accurate statuses based on what the solver actually reports.

Never call a feasible result mathematically optimal unless optimality is proven.

---

# 9. HARD SCHEDULING CONSTRAINTS

These constraints are mandatory.

1. A teacher cannot be assigned to overlapping supervision sessions.

2. A teacher cannot be assigned during an unavailable period.

3. Every exam venue requires the configured number of invigilators for its entire duration.

4. Each required invigilator position must have continuous coverage.

5. A teacher cannot simultaneously cover multiple required positions, even within the same venue.

6. A teacher cannot exceed configured daily supervision limits.

7. A teacher cannot exceed configured examination-period supervision limits.

8. Inactive teachers cannot be assigned.

9. Teachers with CanInvigilate = false cannot be assigned.

10. A teacher must have the configured minimum rest period between distinct supervision duties.

11. Manual locked assignments must be preserved exactly when regenerating the roster, unless the user explicitly unlocks them.

12. A teacher must not receive an assignment outside the examination start/end interval.

13. Split sessions must respect the configured minimum session length.

14. Split sessions must not leave coverage gaps.

15. Maximum split-segment limits must be respected.

16. Two conflicting exams cannot use the same physical venue simultaneously.

17. Unavailable venues cannot be scheduled.

18. The engine must reject invalid input rather than trying to schedule around corrupt data.

**If no feasible complete roster exists, do not violate a hard constraint.**

Return an infeasible result with useful diagnostics.

The application may display a partial diagnostic allocation, but it must not present it as a complete validated roster.

---

# 10. SESSION SPLITTING

Teachers may share supervision of the same examination.

Example:

```text
Grade 8 Mathematics

Date: 10 December
Venue: A1
Exam: 08:00–10:00
Required invigilators: 1
```

Potential allocation:

```text
Teacher A: 08:00–09:00
Teacher B: 09:00–10:00
```

This provides continuous supervision across the full examination.

Another example:

```text
Grade 10 Information Technology

Exam: 08:00–11:00
Required invigilators: 2
```

Two independent positions require continuous coverage.

An acceptable split roster might look like:

```text
Position 1:
Teacher A: 08:00–09:30
Teacher B: 09:30–11:00

Position 2:
Teacher C: 08:00–11:00
```

There must always be at least two assigned supervisors throughout the examination.

## Session splitting policy

Splitting is enabled.

However, splitting every examination into unnecessarily small pieces would create a complicated and impractical roster.

Therefore:

- Make session splitting configurable.
- Prefer sensible segment durations.
- Minimize unnecessary handovers.
- Enforce minimum segment lengths.
- Enforce maximum segment counts.
- Require continuous coverage.
- Respect teacher availability.
- Respect minimum breaks.
- Include splitting in fairness optimization.

Suggested initial configuration:

```text
Time increment: 15 minutes
Minimum session length: 60 minutes
Maximum segments per supervision position: 3
```

These are configurable defaults, not hardcoded universal rules.

Choose a documented policy for examination times not aligned to 15-minute increments.

Either support exact exam boundaries or reject inputs with a clear validation message.

Never silently truncate exam time.

A handover should be represented as one assignment ending exactly when the next starts. Do not double-count shared boundaries.

---

# 11. WEIGHTED FAIRNESS

The objective is fair supervision time across the entire exam period.

Measure teacher workload primarily in minutes.

Do not use exam count as the primary metric.

## Basic example

```text
Teacher A
Weight: 1.0
Availability: Full period

Teacher B
Weight: 1.0
Availability: Full period

Teacher C
Weight: 0.5
Availability: Full period
```

If the school needs 1,000 total teacher-minutes, approximate targets are:

```text
Teacher A: 400 minutes
Teacher B: 400 minutes
Teacher C: 200 minutes
```

Actual allocations must respect constraints, session durations, and availability.

Targets are optimization goals, not hard requirements.

## Availability-adjusted targets

A teacher available for only a small part of the examination period should not automatically receive the same workload as someone available throughout.

Create a mathematically defined capacity measure using eligible available time and configured daily/period limits.

Do not simply multiply weights by the total number of calendar days.

A recommended starting model is:

```text
EffectiveCapacity =
    min(
        total eligible available minutes,
        applicable maximum supervision capacity
    )

FairnessShare =
    WorkloadWeight × EffectiveCapacity
```

The exact target model must account for daily maximums and other configured limits.

Do not count the same available minute twice.

For the target calculation, account for intervals of actual examination demand so that unrelated free time does not artificially inflate a teacher's fair share.

Document any approximations.

Calculate initial workload targets from normalized fairness shares.

The optimization engine should then minimize workload imbalance subject to hard constraints.

## Fairness objectives

Prioritize:

1. Produce a complete feasible roster.
2. Minimize the maximum normalized workload imbalance.
3. Minimize total deviation from workload targets.
4. Avoid leaving eligible teachers unused unnecessarily.
5. Balance workload across examination days.
6. Reduce unnecessary split-session handovers.
7. Reduce excessive fragmentation of teacher duties.

Use a documented lexicographic objective or carefully scaled weighted objective.

Hard constraints must never be represented only as soft penalties.

Prevent integer overflow in optimization objective calculations.

Use deterministic tie-breaking and configurable random seeds.

## Fairness report

Calculate:

```text
Teacher name
Workload weight
Available eligible minutes
Target supervision minutes
Actual assigned supervision minutes
Difference from target
Percentage of target
Number of assignments
Number of days assigned
```

Include:

- Total workload minutes.
- Average.
- Median.
- Minimum.
- Maximum.
- Standard deviation.
- Maximum normalized deviation.
- Number of eligible teachers with zero assignments.

Use appropriate handling when a target is zero.

Explain when perfect fairness cannot be achieved because of availability and constraints.

---

# 12. COMPLETE PERIOD OPTIMIZATION

This requirement is critical.

Do not generate a roster one examination at a time without considering future examinations.

Example:

Teacher A is available Monday only.

Teacher B is available Monday and Tuesday.

If both can cover Monday, but Tuesday requires the only available teacher B, the scheduling algorithm should consider reserving B for Tuesday.

The scheduling engine must evaluate the entire selected examination period.

The engine should not systematically favor earlier teachers in the database.

Test that shuffling the order of teachers in the input does not systematically change the fairness quality.

Exact allocations may differ, but total fairness should remain comparable.

---

# 13. INFEASIBLE SCHEDULING

Sometimes the available teachers cannot cover all examinations.

Example:

```text
Simultaneous required positions: 12
Available qualified teachers: 10
```

The scheduler must identify the shortage.

Return useful information:

```text
Date
Time window
Exam
Grade
Subject
Venue
Required invigilators
Available eligible teachers
Uncovered position(s)
Explanation
```

Where possible, identify:

- Too few teachers.
- Availability conflicts.
- Daily maximum limits.
- Minimum break restrictions.
- Locked assignment conflicts.
- Session splitting restrictions.
- Venue conflicts.

Do not falsely claim to know the unique root cause when multiple constraints interact.

Suggest possible user actions without automatically relaxing hard constraints.

---

# 14. MANUAL ROSTER EDITING

After generation, the user must be able to:

- Replace a teacher.
- Swap teachers.
- Split an assignment.
- Combine adjacent assignments where valid.
- Lock an assignment.
- Unlock an assignment.
- Remove an assignment from a draft.
- Regenerate unlocked duties.
- Revalidate the entire roster.
- View fairness changes.

All changes must pass hard-constraint validation.

Use a transaction when saving a set of changes.

Preserve finalized roster versions.

Provide clear warnings when manually edited rosters have missing coverage.

Do not save invalid drafts as finalized rosters.

Maintain basic audit records recording what changed and when.

---

# 15. ROSTER VIEWS

Implement three principal views.

## 15.1 Examination Roster

Group by:

```text
Date
Grade
Subject
Venue
Time
Assigned teacher
```

Display split sessions separately.

Example:

```text
10 December
Grade 8 Mathematics
Venue A1

08:00–09:00 Teacher A
09:00–10:00 Teacher B
```

## 15.2 Teacher Roster

Select a teacher and display all duties.

Example:

```text
Teacher: John Smith

10 December
08:00–10:00
Grade 8 Mathematics
Venue A1

11 December
09:00–10:30
Grade 10 Geography
Venue C3
```

Show total workload.

## 15.3 Calendar View

Allow viewing all duties by date and time.

Provide filtering by:

- Teacher.
- Grade.
- Subject.
- Venue.
- Date.

---

# 16. DASHBOARD

Display:

```text
Total Teachers
Eligible Teachers
Total Exams
Total Venues Used
Required Teacher-Hours
Assigned Teacher-Hours
Number of Assignments
Incomplete Coverage
Teachers With Zero Assignments
Average Workload
Minimum Workload
Maximum Workload
Fairness Statistics
```

Create a clear teacher workload visualization.

Show both actual and target minutes.

Highlight large deviations from targets.

Do not use misleading statistics that compare teachers with significantly different availability without normalization.

---

# 17. EXCEL EXPORT

Implement Excel export using ClosedXML or an equivalent suitable library.

Export:

1. Full examination roster.
2. Teacher-specific rosters.
3. Teacher workload summary.
4. Unfilled assignments.
5. Venue roster.

Requirements:

- Clear column headings.
- Dates and times formatted correctly.
- Separate useful worksheets.
- Auto-sized or appropriately sized columns.
- Frozen header rows where useful.
- Filters where appropriate.
- Workload totals.
- Sensible print layouts.

The export must not require Excel to be installed.

---

# 18. PDF EXPORT

Implement PDF generation.

Support:

- Full exam roster.
- Individual teacher roster.
- Workload report.
- Venue roster.

Requirements:

- A4 page size.
- Landscape orientation where required.
- Clear table formatting.
- Page numbers.
- Repeated table headings.
- No clipped text.
- Print-friendly layout.
- School name configurable in settings.
- Examination period in report headings.

PDF export must work offline.

---

# 19. DATABASE BACKUPS

Create local backup and restore functionality.

Requirements:

- Manual backup.
- Optional automatic backup before significant changes.
- Restore from backup.
- Validate backup integrity.
- Warn before replacing the active database.
- Keep a recovery copy where appropriate.
- Never corrupt the active database if restore fails.

Use a SQLite-safe backup approach.

Do not assume that copying a database file while an active connection is writing to it is always safe.

Test backup and restore using real SQLite database files.

---

# 20. UNIT TESTING REQUIREMENTS

Create comprehensive unit tests for domain logic.

Test:

- Valid examination times.
- Invalid examination times.
- Venue capacity validation.
- Duplicate venue conflicts.
- Teacher eligibility.
- Teacher weights.
- Daily maximum limits.
- Examination-period maximum limits.
- Availability rules.
- Partial-day unavailability.
- Interval overlap detection.
- Minimum breaks.
- Session durations.
- Session splitting.
- Continuous coverage.
- Multiple invigilators per venue.
- Fairness calculations.
- Zero target handling.
- Manual assignment validation.
- Roster finalization rules.

Use parameterized tests where appropriate.

Tests must cover boundaries, not merely common successful inputs.

---

# 21. SCHEDULING ENGINE TESTS

Create a dedicated test suite covering realistic scheduling conditions.

At minimum, implement the following scenarios.

## Test 1 — One examination

```text
1 exam
1 venue
1 teacher
2-hour duration
```

Expected:
- One valid complete assignment.
- No conflicts.
- 120 assigned minutes.

## Test 2 — Multiple venues

```text
1 exam
8 venues
8 available teachers
2-hour duration
```

Expected:
- Eight simultaneous teachers assigned.
- All venues covered.
- No duplicate assignments.

## Test 3 — Insufficient teachers

```text
1 exam
8 venues
5 available teachers
2-hour duration
```

Expected:
- No false complete schedule.
- Infeasibility correctly reported.

## Test 4 — Overlapping exams

```text
Exam A: 08:00–10:00
Exam B: 09:00–11:00
```

Expected:
- A teacher cannot supervise both entire exams.
- Splitting is only allowed if all hard constraints are respected.

## Test 5 — Teacher leave

A teacher is unavailable on the exam date.

Expected:
- No assignments during unavailable times.

## Test 6 — Partial availability

```text
Teacher available:
08:00–09:30

Exam:
08:00–11:00
```

Expected:
- The teacher can only cover an appropriate valid segment.
- The remainder must be covered by another teacher.

## Test 7 — Weighted fairness

```text
Teacher A weight 1.0
Teacher B weight 1.0
Teacher C weight 0.5
```

Expected:
- Workload distribution moves toward 2:2:1 where constraints permit.
- Verify fairness metrics numerically.

## Test 8 — Whole-period fairness

Several exams occur across ten days.

Expected:
- Workload is balanced across the full period, not independently per day.

## Test 9 — No alphabetical bias

Provide equivalent teachers in different input orders.

Expected:
- No systematic starvation.
- Comparable fairness objective values.

## Test 10 — Split examination

```text
1 exam
1 venue
08:00–10:00
2 teachers
```

Expected:
- Valid split coverage where enabled.
- No gaps.

## Test 11 — Two required invigilators

```text
1 exam
1 venue
2 required teachers
```

Expected:
- Two independent simultaneous coverage positions.
- No double counting.

## Test 12 — Locked assignments

Generate a roster with some assignments locked.

Expected:
- Locked assignments remain unchanged during regeneration.
- Conflicting locked assignments are reported.

## Test 13 — Minimum breaks

```text
Exam A: 08:00–10:00
Exam B: 10:00–12:00
Minimum break: 30 minutes
```

Expected:
- The same teacher cannot be assigned directly from A to B.

## Test 14 — Daily maximum

A teacher has a 180-minute daily limit.

Expected:
- Total supervision minutes never exceed 180 on one date.

## Test 15 — No eligible teachers

Expected:
- Clear infeasibility result.
- No crash.

## Test 16 — Empty examination timetable

Expected:
- Valid empty result.
- No assignments.
- No divide-by-zero errors.

## Test 17 — Determinism

Use identical input data and seed.

Expected:
- Same solution under the documented deterministic solver configuration.
- If parallel solver nondeterminism prevents this guarantee, configure deterministic execution for the test.

## Test 18 — Large school

Generate:

```text
75 teachers
Multiple grades
Multiple subjects
Multiple venues
Multiple examination dates
Overlapping sessions
Mixed availability
Weighted workload targets
```

Expected:
- All hard constraints satisfied for feasible scenarios.
- Scheduling finishes within a configurable performance budget in the test environment.
- Fairness is measurably better than a deliberately simple feasible baseline on representative datasets, where improvement is possible.

Do not rely on a strict performance time threshold that makes tests flaky on slower machines.

## Test 19 — Impossible conflicting locked duties

Expected:
- Reject or report invalid input.
- Never silently remove the lock.

## Test 20 — Multiple invigilators with split sessions

Expected:
- Each required position independently has full coverage.

Add further tests based on discovered edge cases.

---

# 22. PROPERTY-BASED AND INVARIANT TESTS

Beyond fixed test cases, implement randomized or property-based tests.

Generate combinations of:

- Teachers.
- Exams.
- Overlapping sessions.
- Venue requirements.
- Teacher availability.
- Workload weights.
- Maximum workload limits.
- Split-session configurations.

For every generated roster, run an independent validation routine.

Verify:

```text
No teacher overlaps
No unavailable teacher assignments
No exceeded limits
No coverage gaps
No insufficient simultaneous venue coverage
No invalid exam boundaries
No violations of minimum breaks
Locked assignments preserved
```

The validator must not simply repeat the optimizer's assumptions.

Use an independently implemented validation path so errors in the scheduling model can be detected.

Save deterministic seeds for failing randomized cases to permit reproduction.

---

# 23. INTEGRATION TESTS

Use real temporary SQLite databases.

Do not substitute EF Core InMemory for SQLite-specific integration tests.

Test:

- Database initialization.
- EF migrations.
- CRUD operations.
- Teacher persistence.
- Subject persistence.
- Venue persistence.
- Examination persistence.
- Assignment persistence.
- Transaction rollback.
- Roster versioning.
- Backup and restore.
- Referential integrity.
- Deletion restrictions.
- Settings persistence.
- Database reopening after application restart.
- Excel export.
- PDF export.

Validate exported file contents where practical.

Verify generated spreadsheets contain the expected rows, dates, times, worksheets, and totals.

Verify generated PDFs are nonempty, structurally valid, and contain expected report content where reliable extraction is available.

---

# 24. WPF TESTING

Implement unit tests for ViewModels.

Test:

- Form validation.
- Add/edit commands.
- Saving behavior.
- Navigation state.
- Loading state.
- Cancellation.
- Error presentation.
- Generate roster command.
- Manual assignment editing.
- Export commands.

Avoid requiring a full visual UI for ViewModel unit tests.

If practical, add a Windows-only smoke test that launches the application and verifies successful startup and critical navigation.

Clearly separate automated UI verification from visual/manual testing.

---

# 25. TEST DATA GENERATOR

Create realistic seeded demonstration data.

Include:

```text
75 teachers
Grades 8–12
Multiple subjects
Multiple examination dates
Multiple venues
Variable exam durations
Teacher leave
Partial availability
Different workload weights
Overlapping exams
```

Use fixed seeds for repeatability.

Ensure the demonstration dataset includes at least one known feasible scheduling scenario.

Also create deliberately infeasible scenarios.

Do not populate a real user's database with demo data automatically without confirmation.

Provide a clear option to generate sample data.

---

# 26. IMPLEMENTATION PHASES

Execute in the following order.

## Phase 1 — Solution Setup

- Create the solution.
- Create projects.
- Configure references.
- Add dependencies.
- Configure dependency injection.
- Configure logging.
- Create basic WPF application startup.
- Create test projects.

**Gate:** Full solution builds and setup tests pass.

## Phase 2 — Domain Models

- Implement entities.
- Implement value objects.
- Implement validation.
- Implement availability and interval rules.
- Implement unit tests first.

**Gate:** All domain tests pass.

## Phase 3 — SQLite Persistence

- Configure EF Core.
- Create migrations.
- Implement persistence services.
- Implement CRUD operations.
- Implement transactions.
- Implement integration tests.

**Gate:** All existing tests pass.

## Phase 4 — Scheduling Input Validation

- Build scheduling request models.
- Implement validation.
- Implement independent conflict validation.
- Add all applicable unit tests.

**Gate:** All tests pass.

## Phase 5 — Basic Feasible Scheduler

- Implement solver integration.
- Model required coverage positions.
- Add hard constraints.
- Support overlapping exams.
- Support teacher availability.
- Support daily and period limits.
- Support minimum breaks.
- Support locked assignments.

**Gate:** All scheduling feasibility tests pass.

## Phase 6 — Session Splitting

- Implement configurable splitting.
- Implement exact continuous coverage.
- Implement minimum segment duration.
- Implement maximum segment limits.
- Implement handover rules.
- Add split-session tests.

**Gate:** All tests pass.

## Phase 7 — Weighted Fairness Optimization

- Implement teacher capacity calculations.
- Implement weighted targets.
- Implement fairness objective.
- Implement whole-period optimization.
- Implement unbiased deterministic tie-breaking.
- Implement fairness statistics.
- Add fairness and performance tests.

**Gate:** All tests pass.

## Phase 8 — Teacher and Availability UI

- Teacher management.
- Availability calendar.
- Workload settings.
- ViewModel tests.

**Gate:** All tests pass.

## Phase 9 — Examination and Venue UI

- Subjects.
- Venues.
- Examinations.
- Multiple venue allocation.
- Validation.

**Gate:** All tests pass.

## Phase 10 — Roster Generation UI

- Scheduler configuration.
- Generate roster.
- Progress/cancellation.
- Solver result display.
- Error diagnostics.

**Gate:** All tests pass.

## Phase 11 — Manual Editing

- Manual teacher replacement.
- Session splitting.
- Assignment locking.
- Roster regeneration.
- Independent validation.
- Roster versioning.

**Gate:** All tests pass.

## Phase 12 — Dashboard and Reporting

- Workload dashboard.
- Teacher roster.
- Examination roster.
- Calendar view.
- Fairness reports.

**Gate:** All tests pass.

## Phase 13 — Exports

- Excel exports.
- PDF exports.
- Export tests.

**Gate:** All tests pass.

## Phase 14 — Backups and Settings

- Persistent settings.
- Backups.
- Restore.
- Error recovery.
- Integration tests.

**Gate:** All tests pass.

## Phase 15 — Final Validation and Publishing

- Run complete automated tests.
- Run randomized invariant tests.
- Verify migration scenarios.
- Verify offline execution.
- Publish the Windows application.
- Verify required native dependencies are included.
- Confirm the application starts without development tools or a database server.
- Run startup and critical workflow smoke tests where the environment permits.
- Document remaining manual acceptance checks.

**Gate:** Full build, tests, and available deployment checks pass.

---

# 27. QUALITY REQUIREMENTS

The application must have:

- Reliable error handling.
- Structured logging.
- User-friendly error messages.
- No silent scheduling failures.
- No hardcoded teacher count.
- No hardcoded exam dates.
- No fixed grade restrictions.
- No unnecessary internet dependency.
- No fake persistence.
- No placeholder buttons.
- No incomplete navigation.
- No unimplemented commands presented as working features.

Use clean, maintainable C#.

Avoid overly large classes.

Separate scheduling logic from UI.

Use clear naming conventions.

Document non-obvious optimization decisions.

Do not overengineer basic CRUD functionality.

---

# 28. FINAL ACCEPTANCE SCENARIO

The completed application must support the following workflow.

1. Launch the Windows application.

2. Create the school configuration.

3. Add 75 teachers manually.

4. Assign different workload weights.

5. Mark several teachers unavailable on specific dates.

6. Create examination venues.

7. Create the examination timetable.

8. Configure the number of supervisors required per venue.

9. Generate an invigilation roster across the entire examination period.

10. Review the workload distribution.

11. Verify there are no teacher overlaps or coverage gaps.

12. Manually change selected assignments.

13. Lock selected assignments.

14. Regenerate unlocked assignments.

15. Confirm locked assignments remain unchanged.

16. View each teacher's personal timetable.

17. Export the complete roster to Excel.

18. Export individual teacher rosters to PDF.

19. Close the application.

20. Reopen the application.

21. Verify that all data and roster versions remain available.

22. Create and restore a database backup.

All automated acceptance tests and applicable validation gates must pass.

---

# 29. CODEX EXECUTION INSTRUCTIONS

Before writing application code:

1. Inspect the repository.
2. Inspect the available .NET SDKs.
3. Determine compatible package versions.
4. Check Windows-specific build and test capabilities.
5. Create a concise implementation plan.
6. Record important architectural decisions.
7. Begin Phase 1.

Do not stop merely after planning.

Implement the complete project phase by phase.

At the end of every phase, run:

```powershell
dotnet restore SchoolExamScheduler.sln

dotnet build SchoolExamScheduler.sln `
    --configuration Release `
    --no-restore

dotnet test SchoolExamScheduler.sln `
    --configuration Release `
    --no-build
```

Ensure the commands actually execute the intended test projects.

A command returning exit code 0 without discovering any expected tests does not count as successful test verification.

Report the number of tests discovered and executed.

When running specific test projects or configurations, use the applicable supported .NET CLI commands.

Treat compiler warnings and analyzer warnings seriously. Fix warnings introduced by the project rather than disabling them globally.

After implementing each phase, report:

```text
PHASE:
IMPLEMENTED:
TESTS EXECUTED:
TESTS PASSED:
TESTS FAILED:
BUILD RESULT:
KNOWN LIMITATIONS:
NEXT PHASE:
```

Do not move to the next phase until:

```text
Build succeeded
AND
All applicable tests passed
AND
No mandatory validation remains unresolved
```

If a test fails, fix it first.

If a blocker cannot be resolved, stop and explain precisely what prevents further implementation.

Do not mark a phase complete merely because the source code was written.

---

# 30. FINAL DELIVERABLES

Deliver:

1. Complete Visual Studio solution.
2. Working WPF desktop application.
3. SQLite database persistence.
4. Database migrations.
5. Scheduling optimization engine.
6. Weighted fairness calculations.
7. Session splitting.
8. Teacher availability management.
9. Manual roster editing.
10. Dashboard and reporting.
11. Excel and PDF exports.
12. Database backups.
13. Comprehensive automated tests.
14. Example demonstration dataset.
15. README with setup and usage.
16. Architecture and scheduling algorithm documentation.
17. Windows publishing instructions.
18. Publishable Windows application output.
19. Final test results and known limitations.

**Begin implementing the project now. Follow every phase in order. Never continue past a failed build or test gate.**
