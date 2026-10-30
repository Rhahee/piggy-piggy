# Verification — 0.1.0.1

## Baseline scope

The initial baseline includes bank accounts, Income/Expense/Transfer with separate Admin Fees, both Income fee bases, Purpose allocations, persistent Period/Pay Cycle, Wishlist, Debt and Self Debt. The current application version is **0.1.0.1**, update epoch **3**, and document format **9**.

## Automated checks

The current suite contains **433 core checks** and **40 isolated WPF checks**. Coverage includes accounting and historical balances, required categories, both fee bases, manual Save/Open, format-1–8 compatibility, format-9 Wishlist/Debt links, reservations, debt repayments and corrections, persistent report periods, compact accessible ledger icons, ellipsis/tooltips, flexible numeric searches, updater refresh/retry and channel ordering. The 20 new WPF cases cover validation focus, scrolling, cancellation without staged data, and blank opening balances.

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

## Native window update — 0.1.0.1

On **2026-10-30**, all **433 core checks** and **20 isolated WPF checks** passed after switching the main window to standard Windows chrome. The self-contained Windows x64 EXE built successfully with FileVersion **0.1.0.1**. The source directory's build output remains separate from the checked-in code.

Native UI inspection confirmed the standard system title bar and window menu, maximize/restore on the current screen, the 32 × 32 Save control in the menu row, and the displayed version. A fictional format-9 document opened with eight transactions. Changing its report period displayed the unsaved **\*** marker; clicking the toolbar Save cleared it. A second period change triggered the existing Save / Don't Save / Cancel prompt when closing; Cancel preserved the open document, and Ctrl+S saved it. A final close exited normally.

The fixture's transaction identities, dates, amounts, fees and links were checked after saving and remained unchanged. No real financial document was used. Full Snap layout selection, interactive resize across DPI settings, and alternate monitor/taskbar configurations remain outside this check.

These checks were completed before the user subsequently approved publication of 0.1.0.1 with the scoped form polish below.

## Approved Piggy identity — 0.1.0.1

The approved magenta-and-white pig / gold coin / **Piggy** logo was copied without visual changes; its SHA-256 was checked against the selected concept. A matching standalone gold coin was generated with a transparent background and packaged into a Windows ICO containing 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixel frames. Windows successfully decoded the 16-pixel frame. Shell icon extraction from the final EXE returned a gold coin icon, and the EXE reported ProductName **Piggy** and FileVersion **0.1.0.1**.

All **433 core checks** and **20 isolated WPF checks** passed after the branding change. The final self-contained EXE was inspected in windowed and maximized states: the full approved logo, New and Open remained centered and visible; version/update controls remained at the bottom right. Open displayed the renamed Piggy document dialog with the coin icon, and Cancel returned to the start page. New opened an empty workspace, and File → Close returned to the logo screen. No real financial document was opened during these checks.

The embedded PNGs use explicit application-assembly resource URIs, so they also load when production windows are instantiated by the separate WPF check executable. Build output remains outside the source directory. The executable/archive naming contract, document format, update channel and published 0.1.0.0 assets are unchanged. See [branding notes](BRANDING.md) for the image-generation prompt and icon conversion command.

## Scoped form polish — 0.1.0.1

On **2026-10-30**, all **433 core checks** and **40 isolated WPF checks** passed. The Release build completed with zero warnings and errors, and a self-contained Windows x64 EXE was published locally. New modal checks submit invalid forms after focusing Submit, then verify that the relevant input receives keyboard focus. Cases include required/duplicate names, category/account choices, malformed and historical dates, amounts and fees, Income fee limits, transfer destinations, Debt/Lender/due-date fields, Self Debt repayment banks, Period ranges, and Wishlist fields. A shortened form verifies that the focused Debt Name scrolls into view. Rejected submissions preserve serialized document state; a blank opening balance still saves as zero.

Native EXE inspection confirmed consistent form spacing and the Category validation indicator on an empty Expense submission. A fictional eight-transaction format-9 document opened with the expected balance and Saved state; the long path was visibly truncated, while the status control exposed the complete wrapping tooltip text. The invalid transaction was canceled. The fixture was not saved. The model, financial calculations, document format and updater ordering were not changed by this polish.

## Published update — 0.1.0.1

On **2026-10-30**, [app-v0.1.0.1](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.0.1) was published from **0e2d0597330da9c387598c8cb433586fd1e275a7**. Both main and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/41060560645) succeeded; tag logs confirmed **433 core checks and 40 isolated WPF checks**. The downloaded draft ZIP matched its checksum asset and GitHub digest:

`add0bf8184be64c5f4bc8a384e23a9a57ce96a997e0230ea05657c3ad8982cd7`

The exact packaged EXE reported FileVersion **0.1.0.1**, ProductName **Piggy**, and the tagged source commit in ProductVersion. Its SHA-256 was `800510f305e02793865ace27043ea85f90afb0c76bad6bd3fce5df1afd46df47`. Native inspection confirmed the approved centered logo, New/Open controls, gold coin icon, native title bar, and displayed version. A fictional format-9 document opened with eight transactions and the expected balance. Hovering over its truncated status path displayed a wrapping tooltip containing the full 238-character location. Closing left the fixture SHA-256 unchanged.

Publication used the verified draft assets without replacement; public GitHub Latest identifies **app-v0.1.0.1**. The channel is promoted only after publication. Executable/archive names, update epoch 3 and document format 9 remain stable. A complete in-place replacement/restart is not part of this release check.

After channel promotion, the production `ReleaseChecker.CheckChannelAsync` returned **Available → 0.1.0.1** for installed **0.1.0.0**, including the correct checksum URL, and **UpToDate** for installed **0.1.0.1**. Separate public requests confirmed generation-3 channel metadata and the downloadable matching checksum.

## Limits

Alternate DPI/multiple monitors, taskbars on other screen edges, real-user financial documents, very large production datasets and live update installation require separate coverage. Native window behavior may vary by Windows version, theme and Snap settings. Automated checks do not replace inspection of the final portable EXE.

Screenshots, fictional documents and local test artifacts remain outside Git. The original project and private financial documents remain in their existing locations.
