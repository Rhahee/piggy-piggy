# Piggy - Piggy 0.1.0.1 — Native Windows Window Frame

**Status: Unreleased.** Source and local build only. No release tag or GitHub Release has been created for this version, and `update-channel.json` has not been promoted. Publication requires separate approval.

**Change type: Minor Fix.** This update replaces the main window's custom title bar and frame with the standard Windows window frame.

## What changed

- Windows now draws the title bar, border and minimize/maximize/close buttons. Their appearance follows the operating system's window decoration settings.
- Windows handles resizing, maximize/restore and Snap. Snap features depend on the installed Windows version and settings.
- The custom maximize work-area override was removed so it does not compete with the native frame. Monitor-aware dialog sizing remains available.
- The **Save** icon is now at the right of the **File / Home / Data / View** row. It remains a 32 × 32 button with the accessible name **Save** and tooltip **Save (Ctrl+S)**.
- The document name and unsaved **\*** marker remain in the native title bar. **Ctrl+S**, **File → Save**, and the unsaved-changes confirmation still use the existing Save flow.

The application content retains its magenta ribbon theme. This change affects window decoration and Save button placement; it does not introduce an automatic light/dark theme for the application content.

## Compatibility and installation

The application remains portable C#/WPF for Windows x64. Document format **9**, compatibility with formats **1–8**, and update epoch **3** are retained. Existing `.mpb` documents can be opened directly. Financial calculations and transaction data are unchanged.

For a manual update, save and close the running application, extract the new portable package into a writable folder, then run **Piggy - Piggy.exe** and open your `.mpb` document. The in-app updater can offer this version after its release package, checksum and channel metadata have been published.

Validation results and remaining coverage limits are recorded in [docs/VERIFICATION.md](docs/VERIFICATION.md).
