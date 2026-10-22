# Windows acceptance — required before school deployment

These checks have not been run in the Linux cloud machine.

- Verify the Microsoft-signed Visual C++ redistributable is included, install it, and confirm OR-Tools can generate a roster without development tools.
- On a clean Windows x64 machine without the SDK, Excel or SQL Server, extract the entire package and launch the executable offline.
- Run `scripts/Test-WindowsStartup.ps1` under a dedicated test Windows account. Confirm the actual application window opens and all navigation works.
- Visually inspect 1366×768 and 1920×1080, common DPI scaling, text wrapping, time entry errors, keyboard tab order, grids, dialogs, workload bars and calendar filters. Check that no XAML binding errors appear in a debugger.
- Under an empty test account, create sample data, validate and generate. Review all duties, targets, coverage and errors. Cancel a generation. Confirm responsive interaction.
- Replace and swap valid duties; reject overlapping or unavailable replacements. Split at valid and invalid handovers. Combine compatible segments. Lock duties, regenerate, and verify exact lock preservation and the retained earlier version.
- Remove an unlocked duty and verify incomplete status; refuse finalization. Generate a complete version, finalize it, and verify edits/regeneration cannot overwrite it.
- Export and open full Excel, individual teacher PDF, venue PDF and workload PDF. Check dates, times, names, totals, page headers and printed A4 layout.
- Close and reopen; verify settings, teachers, exams, duties and versions. Create a backup, make a small change, restore with confirmation, reopen and verify recovery. Verify a corrupt backup cannot replace the active database.
- Install a newer binary folder while retaining LocalAppData; confirm migrations preserve existing records. Confirm startup stops cleanly when the database is inaccessible.

Record Windows version, display scaling, package checksum, results and any defects. Passing automated tests does not replace these checks.
