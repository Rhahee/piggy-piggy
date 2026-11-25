# Verification — 0.2.2.5

## Purchased wishlist goals

[Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45521856578) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45522059138) passed for commit `54b86f2`. The WPF test checked the purchase amount, remaining target, and the effect of correcting a purchase. Package staging verified the checksum and source version. A format 15 fixture with 125 transactions retained equivalent data after saving and reopening a copy.

The public update channel offered 0.2.2.5 to clients reporting 0.1.1.3 and 0.2.2.4, and reported 0.2.2.5 as up to date. The web installer resolved 0.2.2.5.

## Group detail popups

[Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45509508946) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45509757466) passed for commit `3a1c069`. The WPF group check opened a debt popup, verified its payment action and confirmed that closing it restored the group. The verified package contained the tagged executable. A format 15 fixture with 125 transactions reopened with equivalent data after saving a copy.

The public update channel offered 0.2.2.4 to versions 0.1.1.3 and 0.2.2.3 and reported 0.2.2.4 as up to date. The web installer resolved 0.2.2.4.

## Paid debt indicators

[Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45505698117) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45505950417) passed for commit `cc4dda7`. WPF checks covered the individual paid indicator, an entirely paid group, and an empty group. Package staging verified the ZIP checksum and executable source version. A format 15 fixture with 125 transactions reopened with equivalent data and its source hash was preserved.

The public update channel offered 0.2.2.3 to versions 0.1.1.3 and 0.2.2.2 and reported 0.2.2.3 as up to date. The web installer resolved 0.2.2.3.

## Grouping purchased items and settled debts

[Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45502273370) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45502578271) passed for commit `f7ef6c9`. The staged executable reported 0.2.2.2 and the tagged source commit. A format 14 fixture with 125 transactions reopened with equivalent data after saving to format 15. Its source hash was preserved.

The public update channel offered 0.2.2.2 to versions 0.1.1.3 and 0.2.2.1 and reported 0.2.2.2 as up to date. The web installer resolved 0.2.2.2.

## Standalone group creation

[Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45499518306) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45499798762) passed for commit `9fb2e4f`. The verified portable executable reported 0.2.2.1 and the tagged commit. A format 13 fixture with 125 transactions reopened with equivalent data after saving to format 14. The original fixture hash was preserved.

The public update channel offered 0.2.2.1 to versions 0.1.1.3 and 0.2.2.0 and reported 0.2.2.1 as up to date. The web installer resolved 0.2.2.1.

## Debt Groups release

Main CI run [45496526374](https://github.com/Rhahee/piggy-piggy/actions/runs/45496526374) and tag CI run [45496920766](https://github.com/Rhahee/piggy-piggy/actions/runs/45496920766) passed for commit `ba71f54`. Package staging verified the ZIP checksum and executable source version. A format 12 fixture with 125 transactions was saved separately in format 13 and reopened with equivalent data. The source fixture hash was preserved.

After channel promotion, the live updater offered 0.2.2.0 to clients reporting 0.1.1.3 and 0.2.1.2. A client reporting 0.2.2.0 returned UpToDate. The web installer resolved 0.2.2.0. Full installation and restart were not exercised in this check.

## Baseline scope

The initial baseline includes bank accounts, Income/Expense/Transfer with separate Admin Fees, both Income fee bases, Purpose allocations, persistent Period/Pay Cycle, Wishlist, Debt and Self Debt. The current application version is **0.2.2.5**, update epoch **3**, and document format **15**.

## Automated checks

The current suite contains **460 core checks** and **55 isolated WPF checks**. Coverage includes accounting and historical balances, required categories, both fee bases, manual Save/Open, legacy document compatibility, Wishlist/Debt links, Shared Budget allocations and purchase-date corrections, reservations, debt repayments and corrections, persistent report periods, accessible ledger icons, ellipsis/tooltips, flexible numeric searches, updater refresh/retry and channel ordering. WPF cases also cover theme resources, calendar navigation, Overview account filtering, validation focus, scrolling, cancellation without staged data, and blank opening balances.

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

## Published update — 0.1.1.1 (2026-11-09)

[app-v0.1.1.1](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.1.1) was published from **15bc34b1b54c557dd67cae63c7111e091fab175d**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/42619219260) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/42619219263) succeeded; tag logs confirmed **448 core checks and 45 WPF checks**. The separately built installer revision 1.0.0.1 includes the ownership fix validated by **37 isolated file-association checks**.

All four draft assets were downloaded from GitHub before publication, matched their GitHub digests, and the ZIP/installer matched their checksum files:

- Portable ZIP SHA-256: `f227fb1080f49caca8c1f7e2e77233969b2bdecfd8bf7af0bee4cfbee64289db`.
- Packaged EXE SHA-256: `cafbee0c5334bf12edce644117bfbed027423939c734a7df0110e628f6ee5e16`.
- Revised web installer SHA-256: `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`.

The exact packaged EXE reported **0.1.1.1** and the tagged source commit. It opened and saved the fictional three-transaction document without changing its canonical bytes. The release was published as GitHub Latest before channel promotion in **40f8663**. Anonymous public checksum downloads matched the verified assets. Production checks returned **Available → 0.1.1.1** for 0.1.1.0, **UpToDate** for 0.1.1.1, and the web installer resolver selected 0.1.1.1.

A separate exact public **0.1.1.0** client then completed a live UI update: download, EXE replacement, restart, and reopening of the same fictional document. The resulting EXE hash matched the CI package and the document SHA-256 remained unchanged. User financial documents and actual `.mpb` registrations were not touched.

The old public 0.1.1.0 installer remains unchanged (`a83d9f29e1a927830b2042c4f5d9601d6ccda1272fba025f737a00cb3c748ea3`). The active local `dist/installer/` now contains revision 1.0.0.1, with the old launcher preserved under `dist/archive/installer-1.0.0.0/`. Users must run the new installer once to register the grayscale document icon; EXE-only updates do not add installation/uninstallation registration rules.

Evidence is retained under `qa/release-0.1.1.1/`. UserChoice UI behavior on a clean Windows machine and digital signing remain outside this verification.

## Unreleased transaction layout and wheel scrolling — 2026-11-09

Classification: **Minor Fix**, proposed next version **0.1.1.2**. This is a local working-tree fix; application metadata and the public update channel remain **0.1.1.1**. No release, tag, push, or installer replacement was performed for this change.

The root ScrollViewer measured the entire window body without finite space, allowing the transaction grid to expand and taking the ribbon/footer offscreen. Navigation now sits outside page scrolling. Transactions use their own bounded DataGrid with row virtualization; the date is frozen at the left, and text/amount columns have usable minimum widths and proportional expansion. The bank list has its own vertical viewport. Wishlist/Debt retain horizontal access to their minimum-width split panes. Wheel input selects the nearest scrollable pane and hands off at its boundary, preserving ComboBox behavior and system wheel-line settings.

An initial regression run with 125 fictional transactions failed because the grid expanded instead of scrolling (`qa/ledger-scroll-fix/red.log`). After the fix, **448 core checks and 49 isolated WPF checks passed**. New checks cover virtualization, both wheel directions, full dates during horizontal scrolling, column widths, fixed navigation, long-bank-list scrolling, nested Overview wheel handoff, and a populated ledger across simulated native DPI transitions **96 → 120 → 144 → 192 → 96**. The populated DPI round trip exposed a WPF measure loop with rounded star/frozen columns; disabling layout rounding on the ledger resolved it. These are simulated WM_DPICHANGED transitions, not a physical multi-monitor drag test.

A self-contained Windows x64 build succeeded at `qa/ledger-scroll-fix/app/Piggy - Piggy.exe`. Native inspection opened a separate fictional document with 125 income/expense/transfer transactions and linked fees, then checked Transactions in restored and maximized windows. Injected mouse-wheel input moved dates/rows down and back up while the date column, headers, ribbon, tabs, and status stayed visible. Columns filled the available table width. The fictional document SHA-256 remained `009f55a09fa21bc4bb55c1ba949bfb2acf2f906c5389baf98a0f93317715e3cf` after opening and closing. No real financial document was opened. The EXE is an unreleased QA build; the existing public installer was not rebuilt or installed.

## Release preparation — 0.1.1.2

The user authorized publishing the transaction layout/scrolling fix as **0.1.1.2 (Minor Fix)**. The local release build passed **448 core checks and 49 WPF checks**, then produced a self-contained Windows x64 portable ZIP. Main CI for the earlier fix commit revealed that a small runner monitor clamps the requested wide test window; the width assertion now checks the greater of available space and column minimum widths, allowing intentional horizontal overflow while still rejecting unused space. Production behavior is unchanged by that test correction. Document format **9**, update epoch **3**, and web installer revision **1.0.0.1** remain unchanged. Publication and live-channel results are recorded after verification below.

## Published release — 0.1.1.2

[app-v0.1.1.2](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.1.2) was published from **18eed8e2cb3cf8e1b8a473225f937a8dc60e3879**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/42624121914) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/42624176797) succeeded. Tag logs confirmed **448 core checks and 49 WPF checks**, including the monitor-aware column-width assertion.

The draft ZIP/checksum were downloaded from GitHub and matched their asset digests. The production installer staging code verified the checksum, three-file package contract and EXE version. The EXE reported **0.1.1.2+18eed8e2cb3cf8e1b8a473225f937a8dc60e3879**. The same installer revision 1.0.0.1 and its original checksum were attached without rebuilding or modifying the binary.

- Portable ZIP SHA-256: `95cc057b5d82b5fb3300157c2995210743ff4c044504b52dc19b104769adb097`.
- Packaged EXE SHA-256: `4da9b3cbc080180b7405920bdb3a07500b50776313ab74f61366cb7290e18191`.
- Web installer SHA-256: `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`.

Native inspection of the exact GitHub package confirmed the transaction date column, column sizing, and mouse-wheel row scrolling while navigation stayed pinned. A fictional 125-transaction document also passed Save/Open with identical canonical bytes; its SHA-256 remained `009f55a09fa21bc4bb55c1ba949bfb2acf2f906c5389baf98a0f93317715e3cf` after native inspection. No real financial document or installed user application was opened or modified.

The release was published as GitHub Latest before channel promotion in **54d707f**. Anonymous public downloads of both checksum assets matched the verified copies. Once GitHub raw-channel propagation completed, production checks returned **Available → 0.1.1.2** for installed versions **0.1.1.0** and **0.1.1.1**, **UpToDate** for **0.1.1.2**, and the web installer resolver selected **0.1.1.2**. This verifies live discovery/resolution; a fresh install and complete in-place replacement/restart were not repeated for this release. Evidence is retained under `qa/release-0.1.1.2/`.
## Split-workspace fix — 0.1.1.5 (2026-11-16)

Wishlist and Debt now constrain the list panel to the workspace width while reserving the detail panel minimum, divider and margins. Narrowing the window after widening the divider keeps the right-hand controls within the viewport; widening the same layout again can restore its preferred list width. The existing horizontal workspace scroll remains available on smaller viewports.

All **448 core checks and 51 isolated WPF checks** passed. Two new production-window regressions cover Wishlist and Debt: a preferred list width of 500, window resize from 1500 to 1000, widening again, and simulated DPI transitions 100/125/150/200/100%. They verify detail bounds, minimum detail width, clean document state and unchanged fictional MPB bytes. The original Wishlist reproduction now measures the detail right edge at 773.6 inside a viewport of 785.6, rather than 896 outside that viewport. Edit Target Date, Mark as Purchased and Edit Date remain within horizontal reach.

This fix is prepared as application version **0.1.1.5**, with document format **9** and update epoch **3** retained. Physical mixed-DPI monitor migration remains unverified. Publication and channel verification are recorded separately below.
## Original publication — 0.1.1.5 (renumbered to 0.1.1.3 by user request)

On **2026-11-16**, [app-v0.1.1.5](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.1.5) was published from **2345c142a92ecc7d6399ce5c36708db7cbfe8f56**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/43793201535) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/43793412559) succeeded, including **448 core checks and 51 WPF checks**. The split-workspace checks account for monitor-clamped initial window width and verify scroll reachability of both detail edges when the logical viewport is narrower than the detail panel. Candidate tags 0.1.1.3 and 0.1.1.4 failed these new CI assertions and were never published or promoted; their tags were not moved or reused.

The exact GitHub draft ZIP and checksum were downloaded and matched both SHA-256 and GitHub asset digests. Production `InstallerPackage.Stage` verified the EXE version and three-file package contract. Native inspection opened the fictional Wishlist document in the packaged **0.1.1.5+2345c142a92ecc7d6399ce5c36708db7cbfe8f56** EXE, showed the Wishlist detail/actions and Saved state, and closed without changing the MPB SHA-256. Canonical Save/Open also preserved the fictional document bytes. No real financial document or installed user application was used.

- Portable ZIP SHA-256: `9a89bdf21bafe69a1e9dc8e7a8745a7851651e5019d8f1d2c83f25d6291828bc`.
- Packaged EXE SHA-256: `37c8799c3e5c31cee9cb87fc731993a23a1b88e0333e6bd2d989e5e0d3ad1ea8`.
- Web installer revision 1.0.0.1 SHA-256: `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524` (unchanged).
- Fictional MPB SHA-256: `0d9d934342e4248cfa72c18d2f67801aa95b476cb028f69366be092b3549c884`.

The verified draft assets were published as GitHub Latest before channel promotion in **dff895b**. Anonymous public downloads of both checksum assets matched the verified copies. Production channel checks returned **Available → 0.1.1.5** for installed **0.1.1.2**, **UpToDate** for **0.1.1.5**, and the unchanged web installer resolver selected **0.1.1.5**. This verifies live discovery/resolution; a fresh install and complete in-place replacement/restart were not repeated for this release. Physical mixed-DPI monitor migration remains outside this verification. Evidence is retained under `qa/release-0.1.1.5/`.
## User-approved renumbering — published 0.1.1.3 (2026-11-16)

The user explicitly requested sequential numbering, confirmation before future version changes, and replacing the published 0.1.1.5 with **0.1.1.3**, accepting that clients already on 0.1.1.5 will not automatically downgrade. The same verified split-panel code was rebuilt with version **0.1.1.3** from commit **0914104e76bbbe833484e5df8cfc28f23b89a642**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/43795059057) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/43795256311) succeeded with **448 core checks and 51 WPF checks** before publication.

The old candidate/published tag targets were backed up under `archive/2026-11-16/app-v0.1.1.3`, `archive/2026-11-16/app-v0.1.1.4` and `archive/2026-11-16/app-v0.1.1.5`. After main CI passed, the unused app-v0.1.1.3 candidate tag was replaced using a lease against its known previous SHA. The new [0.1.1.3 release](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.1.3) is public and GitHub Latest; 0.1.1.5 was withdrawn to a private draft, and the active app-v0.1.1.4/app-v0.1.1.5 tags were removed. The previous publication record above is historical and no longer identifies the active release.

- Portable ZIP SHA-256: `18a2d3f6d4f80b4408e3a81b37e69c42ae45010a40e4ada4703b2a171d1f3401`.
- Packaged EXE SHA-256: `6fab8288a823b02728df8f5db9bf45f578fa23d32aeec75b1c9c9b2b6442f04d`.
- Installer revision 1.0.0.1 remains byte-identical with SHA-256 `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`.

The GitHub draft ZIP and checksum matched their asset digests; production package staging verified SHA-256, the three-file contract and EXE version. Native inspection confirmed **v0.1.1.3** and the Saved fictional document. Its bytes remained unchanged, and canonical Save/Open passed. No installed user application or real financial document was changed.

Both checksum assets were downloaded anonymously after publication and matched the verified copies. Channel promotion in **c0083dc** was confirmed by production checks: installed **0.1.1.2 → Available 0.1.1.3**, **0.1.1.3 → UpToDate**, **0.1.1.5 → UpToDate** (numeric downgrade protection retained), and installer resolution **0.1.1.3**. Epoch 3 and document format 9 remain unchanged. Full update installation/restart and physical mixed-DPI monitor migration were not repeated. Evidence is under `qa/renumber-0.1.1.3/`.

## Published release — 0.2.0.0

[Shared Budget 0.2.0.0](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.2.0.0) was built from commit **0469c94c4d250301183184ff9899412c65d9629d**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/44724927987) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/44725243241) succeeded. Local verification passed **452 core and 52 WPF checks**, including manual goal allocation, purchase surplus, inter-group reallocation, and conversion of an existing planned wishlist.

The GitHub portable ZIP and checksum matched their asset digests. Production staging verified the package and EXE version. The staged EXE opened and closed a fictional format-9 document without modifying it. A separate Save/Open check verified equivalent document contents in format 11. ZIP SHA-256 is `bc92f4914ea954693041520742f2c2b762c879623f8d0622bd5af3ecf5d78ed7`. The attached installer is byte-identical to the 0.1.1.3 release asset. Its SHA-256 is `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`.

After publication and channel promotion in **20a1d21**, the production updater reported **Available → 0.2.0.0** for **0.1.1.3** and **0.1.1.5**, and **UpToDate** for **0.2.0.0**. The web installer resolver selected 0.2.0.0. Full installation and update restart were outside this release verification.

## Published release — 0.2.1.0

[Shared Budget Progress & Goal Deletion 0.2.1.0](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.2.1.0) was built from **48223adaca0c426678829e910367660fbda3211e**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/44728919742) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/44729118666) succeeded. Local verification passed **453 core and 52 WPF checks**, including progress based on active goals, two-account allocation release on deletion, and purchase correction after deletion.

Production staging verified the GitHub portable ZIP checksum and EXE version. The staged EXE opened and closed a fictional format-11 document without changing it. A separate Save/Open check preserved its contents in format 12. ZIP SHA-256 is `87949058010e7fb431af30571156ecd8eed3af7ee5adb726d39f170400a6434e`. The attached web installer matches the previous release asset at SHA-256 `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`.

After publication and channel promotion in **29d7906**, the updater found **0.2.1.0** from **0.1.1.3** and **0.2.0.0**, and reported **UpToDate** for **0.2.1.0**. The web installer resolver selected **0.2.1.0**. Full installation and update restart were outside this verification.

## Published release — 0.2.1.1

[Shared Budget Goals Table 0.2.1.1](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.2.1.1) was built from **ee3e59c428988cc62d7124a4499a72d2b1b0e50a**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/44731879700) and [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/44732054426) passed. The tag run passed **453 core and 52 WPF checks**, including the position of Unassigned balance and horizontal table scrolling in a narrow pane.

Production staging verified the GitHub portable ZIP and EXE version. The packaged application opened and closed a fictional format-12 document without rewriting it. A separate Save/Open check preserved its contents. The ZIP SHA-256 is `2d39075a4c9488793d088f0c72c2737efad2be0e6b6804f73beb4c49c5ff531f`. The web installer matches the previous release asset at SHA-256 `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`. Both public checksum downloads matched the verified copies.

After publication and channel promotion in **7aca7ab**, the production updater found **0.2.1.1** from **0.1.1.3** and **0.2.1.0**, and reported **UpToDate** for **0.2.1.1**. The web installer resolver selected **0.2.1.1**. Full installation and update restart were outside this verification.

## Local Shared Budget purchase-date fix — 0.2.1.2 (2026-11-23)

The user approved combining this fix with the existing local 0.2.1.2 UI work. Editing a Shared Budget purchase date now moves its automatic surplus returns and linked expense/admin fee together. The date dialog excludes these automatic returns from its neighboring-activity limits. Manual reallocations and historical balance validation still constrain the correction; direct edits of internal funding events remain prohibited.

The new regression failed on the original implementation. After the fix, **458 core checks and 55 isolated WPF checks** passed on the current local checkout. Five new core checks cover two-account surplus, earlier/later corrections, source immutability, manual reallocation limits, fee funding, purchases without surplus, format-12 Save/Open, repeated corrections and purchase reversal. Fictional data and temporary directories were used.

Document format remains **12**. Existing documents have no operation ID, so automatic returns are identified by the adjacent matching goal-to-unassigned pairs produced by Purchase. No application version metadata was changed by this fix. No commit, push, tag or release was performed. Native mouse interaction, full update installation/restart and physical mixed-DPI monitor migration were not repeated.

## Published release — 0.2.1.2

[Workspace & Purchase Date Fix 0.2.1.2](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.2.1.2) was built from **7105ecbdf7d0174dbc2efbc4fa254c32e19f098b**. [Main CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45101871486) passed before tag creation; [tag CI](https://github.com/Rhahee/piggy-piggy/actions/runs/45102066554) also passed. Local publication checks passed **458 core and 55 WPF checks**.

Production staging verified the GitHub draft ZIP checksum, three-file payload and EXE version/source. The packaged EXE opened and closed a fictional format-12 document with 125 transactions without changing its bytes. Save/Open preserved its contents. Native inspection confirmed Overview, the displayed version and bundled font; the local build also switched to the dark theme successfully. ZIP SHA-256 is `0c4e9d0f469edb12442137ef81ea6b087f4d7dd296c5ac7bf13e1bea4e3bd2fd`; EXE SHA-256 is `b7222e3406dc6366b74bdbf7f1e40d09b44739f39a2b70dd6dcf7e25238dfa94`.

The release was published as GitHub Latest on **2026-11-23**. The attached installer is unchanged at SHA-256 `b8729d291b2004a92bb965195620b5108824984da94f44ea0572c31ef517f524`. Both public checksum downloads matched the verified copies. Format 12 and epoch 3 remain unchanged. Full update installation/restart and physical mixed-DPI monitor migration were not repeated. Local evidence is under `qa/release-0.2.1.2/`.

After channel promotion in **ce71a1d**, live production checks returned **Available → 0.2.1.2** for **0.1.1.3** and **0.2.1.1**, and **UpToDate** for **0.2.1.2**. The web installer resolver selected **0.2.1.2**. No user installation was replaced during these checks.
