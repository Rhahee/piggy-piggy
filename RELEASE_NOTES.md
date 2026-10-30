# Piggy 0.1.0.1 — Native Windows Frame & Piggy Identity

**Status: Unreleased.** Source and local build only. No release tag or GitHub Release has been created for this version, and `update-channel.json` has not been promoted. Publication requires separate approval.

The published baseline is [0.1.0.0](docs/releases/0.1.0.0.md), available on [GitHub Releases](https://github.com/Rhahee/piggy-piggy/releases/tag/app-v0.1.0.0). Its package retains the baseline custom title bar.

**Change type: Minor Fix.** This update uses the standard Windows window frame and the approved Piggy visual identity. The version remains **0.1.0.1** as requested.

## What changed

- The visible application name is now **Piggy**, with the approved magenta-and-white pig eating a gold coin above the Piggy wordmark.
- A gold coin alone is used for the application and window icons, with transparent surroundings and icon frames from 16 to 256 pixels.
- The startup page now centers **logo → New → Open** vertically on a white background. New and Open use matching 240 × 44 buttons, separated by 12 units; the version/update indicator stays at the bottom right.
- Windows now draws the title bar, border and minimize/maximize/close buttons. Their appearance follows the operating system's window decoration settings.
- Windows handles resizing, maximize/restore and Snap. Snap features depend on the installed Windows version and settings.
- The custom maximize work-area override was removed so it does not compete with the native frame. Monitor-aware dialog sizing remains available.
- The **Save** icon is now at the right of the **File / Home / Data / View** row. It remains a 32 × 32 button with the accessible name **Save** and tooltip **Save (Ctrl+S)**.
- The document name and unsaved **\*** marker remain in the native title bar. **Ctrl+S**, **File → Save**, and the unsaved-changes confirmation still use the existing Save flow.

The application content retains its magenta ribbon theme. An automatic light/dark theme for the application content is not introduced.

## Compatibility and installation

The application remains portable C#/WPF for Windows x64. Document format **9**, compatibility with formats **1–8**, and update epoch **3** are retained. Existing `.mpb` documents can be opened directly. Financial calculations and transaction data are unchanged.

The executable remains **Piggy - Piggy.exe** and update archives remain **Piggy-Piggy-VERSION-win-x64.zip** so the published 0.1.0.0 updater can recognize future packages. The repository and internal namespaces retain their existing names. See [branding notes](docs/BRANDING.md) for asset provenance and icon regeneration.

For a manual update, save and close the running application, extract the new portable package into a writable folder, then run **Piggy - Piggy.exe** and open your `.mpb` document. The in-app updater can offer this version after its release package, checksum and channel metadata have been published.

Validation results and remaining coverage limits are recorded in [docs/VERIFICATION.md](docs/VERIFICATION.md).
