# Verification — 0.1.0.2

## Baseline scope

The initial baseline includes bank accounts, Income/Expense/Transfer with separate Admin Fees, both Income fee bases, Purpose allocations, persistent Period/Pay Cycle, Wishlist, Debt and Self Debt. The current application version is **0.1.0.2**, update epoch **3**, and document format **9**.

## Automated checks

The current suite contains **435 core checks** and **45 isolated WPF checks**. Coverage includes accounting and historical balances, required categories, both fee bases, manual Save/Open, format-1–8 compatibility, format-9 Wishlist/Debt links, reservations, debt repayments and corrections, persistent report periods, compact accessible ledger icons, ellipsis/tooltips, flexible numeric searches, updater refresh/retry and channel ordering. The 20 new WPF cases cover validation focus, scrolling, cancellation without staged data, and blank opening balances.

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

## Local visual polish — 0.1.0.2 (unreleased)

On **2026-10-31**, all **40 isolated WPF checks** passed after the visual changes. Self-contained Windows x64 publishing succeeded. Native inspection confirmed square consistent ribbon commands, right-aligned Amount headers and values, centered ledger icons, the centered empty Transactions message, and a populated fictional format-9 document with eight transactions. A selected row used the stronger magenta tint while its current cell displayed a separate focus outline. No financial document was saved during inspection.

The empty-table overlay is bound to the production grid's HasItems, so search refreshes can show or hide it without rebuilding the workspace. Ribbon and ordinary button focus decoration no longer changes padding or border measurement. Existing font sizes, financial logic, document format and updater epoch remain unchanged. This scope does not include the separately identified date-picker month and sorting-on-search issues. No release, push or channel promotion was performed for this local build.

## Pre-release fixes — 0.1.0.2 (unreleased)

On **2026-10-31**, **435 core checks and 44 isolated WPF checks** passed. New tests open the production calendar popup and verify its historical input month, reopening after year/month navigation, and blank/invalid text preservation. Search tests cover ascending/descending numeric sorting, column arrows, empty-result recovery and empty-message visibility. A shown 1000 × 640 workspace verifies Ctrl+F focus and empty-message bounds. The focus timing failure found during that check was corrected by deferring focus until the rendered input is ready; the regression check now passes. The core suite covers Save/Open and now explicitly checks a simulated epoch-3 update from 0.1.0.1 to 0.1.0.2, plus equal/older-version rejection.

A fresh self-contained Windows x64 EXE published successfully and launched with the approved identity and version 0.1.0.2. Live update replacement/restart and alternate DPI/multiple-monitor tests remain pending; the public channel is still 0.1.0.1. No release or remote changes were made in this pre-release pass.

## Published update — 0.1.0.2

On **2026-10-31**, [app-v0.1.0.2](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.0.2) was published from **b64c3542d5982f9a0bd8d8925654eea389cca1ec**. Main CI and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/41214029557) succeeded; tag logs confirmed **435 core checks and 44 isolated WPF checks**. The downloaded ZIP matched its checksum asset and GitHub digest:

`f6e4bdd33e2246006de6bbfea1fb3904cc3701b186e562641dad6250832e11b5`

The exact packaged EXE reported FileVersion **0.1.0.2** and the tagged commit in ProductVersion; EXE SHA-256 was `c6a37784496118a276f8b1a64318e74837f6f6a04de8290c4c82dcd001284891`. Native inspection confirmed startup identity/version, square ribbon commands, the centered empty Transactions message and Ctrl+F caret in the search field. Only the isolated release copy was exercised; no real financial document was opened. The verified draft assets were published without replacement, then the generation-3 channel was promoted. A full in-place updater replacement/restart remains outside this check.
After promotion, the production release checker returned **Available → 0.1.0.2** for installed **0.1.0.1** with the correct checksum URL, and **UpToDate** for installed **0.1.0.2**. GitHub Latest identifies app-v0.1.0.2; a separate public download of its checksum matched the verified ZIP.

## Local high-DPI window fix — 0.1.0.2 (2026-11-09)

The main window now adapts its minimum size and restored bounds to the current monitor work area on startup, DPI changes and monitor moves. Smaller logical workspaces provide outer scrolling so content remains reachable. Version 0.1.0.2, document format 9 and update epoch 3 are retained.

All **435 core checks and 45 isolated WPF checks** passed. The new native-message regression exercises 96 → 120 → 144 → 192 → 96 DPI, checks physical window bounds against the monitor work area, and verifies that the status bar remains reachable through scrolling. In the isolated PerMonitorV2 harness, the 200% window that previously measured 2000 × 1280 now measures 1824 × 1044 within a 1920 × 1140 physical work area. Main-window and Transfer-dialog bounds fit at each tested scale. These transitions are simulated; the single physical monitor remains at 125%, and real mixed-DPI monitor migration remains unverified.

Before this patch, a separate live portable update from the exact app-v0.1.0.1 source build to the published 0.1.0.2 completed download, EXE replacement, restart and reopening of a fictional three-transaction document. The document SHA-256 was unchanged; no real financial document was used. This verifies that published update path, not an update to this local patch. No new release or channel promotion was performed.

## Release preparation — 0.1.1.0 (2026-11-09)

The application version is **0.1.1.0** (Feature Fix), retaining document format 9 and update epoch 3. Local publication passed **446 core checks and 45 isolated WPF checks**, including explicit 0.1.0.2 → 0.1.1.0 availability and equal/older-version rejection. The self-contained Windows x64 EXE published successfully. The previously user-tested web installer remains byte-identical, SHA-256 `a83d9f29e1a927830b2042c4f5d9601d6ccda1272fba025f737a00cb3c748ea3`.

The release is prepared with portable ZIP/checksum from tag CI plus the separately verified fixed web installer/checksum. Publication and channel promotion are performed after CI/package verification; final results are recorded below when completed.

## Published update — 0.1.1.0 (2026-11-09)

[app-v0.1.1.0](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.1.0) was published from **29896ce0f676e5140397ae8df6256754f219f839**. Both [main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/42557938423) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/42557938531) succeeded. Tag job logs confirmed **446 core checks and 45 isolated WPF checks**. The draft contained four assets: portable ZIP/checksum and fixed web installer/checksum. Each asset was downloaded from GitHub and matched its GitHub digest; both package checksum files matched their binaries before publication.

- Portable ZIP SHA-256: `6a3b0aefb9a718679496079bdcf40a38303c66fddc6a72c576fad5e6f069e212`.
- Packaged EXE SHA-256: `9988c258ff54a275735961e1119c955a2fa62e8a94ee11424875256dc78b21c6`.
- Fixed web installer SHA-256: `a83d9f29e1a927830b2042c4f5d9601d6ccda1272fba025f737a00cb3c748ea3` (unchanged from the previously user-tested installer).

The exact CI-packaged EXE reported **0.1.1.0** with the tagged commit in ProductVersion. Native inspection confirmed the version and a fictional three-transaction document. Open/Save preserved document data and persisted the current report period as expected. A separate core Save/Open check also passed with that fictional document.

The release was published as GitHub Latest before `update-channel.json` was promoted in **7c00b5f**. Anonymous public checksum downloads matched the verified assets. Production release checks returned **Available → 0.1.1.0** for 0.1.0.2 and **UpToDate** for 0.1.1.0. Both the current web installer resolver and the previously built fixed installer helper resolved 0.1.1.0 without rebuilding the launcher.

A separate exact published **0.1.0.2** client then performed a full live UI update: check, download, SHA-256 verification, EXE replacement, restart and reopening of the same fictional three-transaction document. The updated EXE hash matched the CI package and the document SHA-256 remained unchanged. No real financial document or user installation was used. Physical mixed-DPI monitor migration, Windows clean-machine installation and installer signing remain outside this verification.

Local evidence is retained under `qa/release-0.1.1.0/`, including CI logs, downloaded assets, package verification, live channel checks and the live update result; this evidence is ignored by Git.

## Unreleased document icon — 0.1.1.1 (2026-11-09)

Local application version **0.1.1.1** is prepared as a Minor Fix. `Assets/mpb.png` is the previously previewed grayscale coin and `Assets/mpb.ico` contains PNG-backed frames at 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels. The application icon remains gold. Local portable publication and the unreleased installer revision **1.0.0.1** compiled successfully; **446 core checks and 45 WPF checks passed**.

`Tests/Installer/Test-FileAssociation.ps1` passed **23 isolated checks**. It compiles the production registry/uninstall rules under a random QA extension/ProgID/AppId, replacing only network preparation with the local payload. Windows `AssocQueryString` resolved both the grayscale DefaultIcon and the correctly quoted Open command. Tests covered an initially unassigned extension, a pre-existing foreign default, a default changed after installation, installed ICO hash/nine frames, registration/icon removal at uninstall, and unchanged `.mpb` sentinel bytes. QA registrations were removed afterward; the user's actual `.mpb` association was not changed.

The public installer remains byte-identical (SHA-256 `a83d9f29e1a927830b2042c4f5d9601d6ccda1272fba025f737a00cb3c748ea3`), and `update-channel.json` still targets **0.1.1.0**. The unreleased installer is under `dist/unreleased/0.1.1.1/installer/`; it still downloads the active public application until a future release/channel promotion. New file-type registration requires the revised installer, since the existing application updater replaces only the EXE.

Evidence is retained in `qa/mpb-icon-0.1.1.1/` and the generated `qa/file-association-*/` folder. Actual Windows UserChoice selection, Explorer visual checks on a clean machine and physical double-click dispatch were not exercised by this QA script. No release, tag or remote push was performed for 0.1.1.1.

## Ownership fix and release preparation — 0.1.1.1 (2026-11-09)

The opt-out regression was first added to `Tests/Installer/Test-FileAssociation.ps1` and failed on the old installer at the assertion that a pre-existing Piggy default must survive opt-out uninstall. The fix records `DocumentDefaultOwner` under an AppId-scoped installer key only when setup takes the default. Uninstall captures this marker before registry cleanup, checks the owning installation path, and removes the extension default only when both ownership and current ProgID match. An opt-out reinstall retains the original marker.

After the fix, **37 isolated file-association checks passed**, including the original 23 cases plus pre-existing same-ProgID opt-out and owned opt-in → opt-out reinstall. The sequential local publish passed **448 core checks and 45 WPF checks**. The new channel tests cover 0.1.1.0 → 0.1.1.1 and rejection of equal/older releases. Installer revision **1.0.0.1** compiled successfully to `dist/releases/0.1.1.1/installer/`; its SHA-256 is `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`.

Evidence: `qa/release-0.1.1.1/association-red.log`, `association-green.log`, `local-publish-verified.log`, and `installer-build.log`. An initial overlapping local build failed due to an output DLL lock; the subsequent sequential publish and installer build succeeded. Public release assets and channel remain untouched until the verified tag package is published; final publication results are recorded below.
