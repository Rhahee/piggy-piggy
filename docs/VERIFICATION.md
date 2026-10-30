# Verification — 0.1.0.1

## Baseline scope

The initial baseline includes bank accounts, Income/Expense/Transfer with separate Admin Fees, both Income fee bases, Purpose allocations, persistent Period/Pay Cycle, Wishlist, Debt and Self Debt. The current application version is **0.1.0.1**, update epoch **3**, and document format **9**.

## Automated checks

The baseline suite contains **433 core checks** and **20 isolated WPF checks**. Coverage includes accounting and historical balances, required categories, both fee bases, manual Save/Open, format-1–8 compatibility, format-9 Wishlist/Debt links, reservations, debt repayments and corrections, persistent report periods, compact accessible ledger icons, ellipsis/tooltips, flexible numeric searches, updater refresh/retry and channel ordering.

The WPF checks instantiate production controls in an isolated STA process. They check behavior and measurement, not rendered pixels. Update HTTP cases use simulated responses unless a live check is explicitly recorded.

## Piggy - Piggy baseline

The C#/WPF source was copied into a separate, clean directory from the 0.1.0.0 baseline, then renamed to **Piggy - Piggy**. The repository is **Rhahee/piggy-piggy**, the executable is **Piggy - Piggy.exe**, and update packages use **Piggy-Piggy-VERSION-win-x64.zip**. Internal C# namespaces remain stable; financial document fields and calculations are unchanged.

The new repository starts independently. Previous Git history, experimental toolkits, build caches, release backups and private `.mpb` documents are not included. Test and build output can be directed outside the source directory using the .NET SDK's `--artifacts-path` option.

On **2026-10-30**, all **433 core checks** and **20 isolated WPF checks** passed from the renamed source. A self-contained Windows x64 EXE built successfully with FileVersion **0.1.0.0** and ProductName **Piggy - Piggy**. Native UI inspection confirmed the new name on the title bar, start screen and Open document dialog, the `.mpb` filter, and the displayed version. Canceling Open returned to the start screen without loading or changing a financial document.

Release and checksum requests are restricted to the new repository. Automated updater cases use simulated responses; live publication checks are recorded below.

## Published baseline — 0.1.0.0

On **2026-10-30**, [tag app-v0.1.0.0](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.0.0) was published from commit **cd9fa90d2b5659f7b2d44082c64a216325242422**. The [tag workflow](https://github.com/Rhahee/piggy-piggy/actions/runs/41049415007) passed **433 core checks** and **20 isolated WPF checks**. The downloaded ZIP contained only the EXE, README and icon license. Its SHA-256 matched both the checksum asset and GitHub's asset digest:

`00abeab64bb3f7e67719cad384324195da4b783150ad6c183135a2712b2bc540`

The exact packaged EXE reported FileVersion **0.1.0.0** and the baseline commit in ProductVersion. Native UI inspection confirmed the baseline custom title bar, displayed version, and successful opening of a fictional format-9 document with eight transactions. Opening and closing left the document bytes unchanged.

Public checks confirmed that GitHub Latest and the generation-3 channel point to **0.1.0.0**, with a matching downloadable checksum. The production release checker returned **UpToDate** for installed versions **0.1.0.0** and **0.1.0.1**, so the development build is not offered a downgrade. A complete in-place replacement/restart was not performed.

## Native window update — 0.1.0.1 (unreleased)

On **2026-10-30**, all **433 core checks** and **20 isolated WPF checks** passed after switching the main window to standard Windows chrome. The self-contained Windows x64 EXE built successfully with FileVersion **0.1.0.1**. The source directory's build output remains separate from the checked-in code.

Native UI inspection confirmed the standard system title bar and window menu, maximize/restore on the current screen, the 32 × 32 Save control in the menu row, and the displayed version. A fictional format-9 document opened with eight transactions. Changing its report period displayed the unsaved **\*** marker; clicking the toolbar Save cleared it. A second period change triggered the existing Save / Don't Save / Cancel prompt when closing; Cancel preserved the open document, and Ctrl+S saved it. A final close exited normally.

The fixture's transaction identities, dates, amounts, fees and links were checked after saving and remained unchanged. No real financial document was used. Full Snap layout selection, interactive resize across DPI settings, and alternate monitor/taskbar configurations remain outside this check.

This version is **unreleased** at the user's request. No tag or GitHub Release is created, and `update-channel.json` remains unchanged until publication is approved.

## Limits

Alternate DPI/multiple monitors, taskbars on other screen edges, real-user financial documents, very large production datasets and live update installation require separate coverage. Native window behavior may vary by Windows version, theme and Snap settings. Automated checks do not replace inspection of the final portable EXE.

Screenshots, fictional documents and local test artifacts remain outside Git. The original project and private financial documents remain in their existing locations.
