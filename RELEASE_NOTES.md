# Piggy 0.1.0.2 — Ribbon and Ledger Polish

**Change type: Minor Fix.** Portable Windows x64 update from 0.1.0.1; document format 9 and updater epoch 3 are retained.

- Ribbon commands retain square 72 × 72 buttons, with consistent 32 × 32 icons, a four-unit icon/label gap, and a single 16-unit label line. Tooltips and accessible names retain complete command labels.
- Amount headers align right with numeric cells; existing centered Type, Allocation and Debt icons remain compact.
- Buttons distinguish hover, pressed, disabled and keyboard-focus states. Focus borders are overlaid to avoid shifting content. Selected navigation uses a white focus border on magenta.
- Ledger rows use separate hover and selection colors, with a visible keyboard-focus outline on the current cell.
- Overview and Transactions show centered explanatory messages inside empty table bodies. Messages automatically disappear when rows become available, including after search changes.

The font scale, financial calculations, manual Save behavior, document format 9 and updater epoch 3 remain unchanged. Date pickers now open the month/year from the input and reset stale calendar navigation on reopening. Blank or invalid date text is preserved while the calendar opens today without a stale selection. Transaction searches retain numeric/text sorting and header direction, including through empty results. Ctrl+F and the Search command focus the input after the rendered workspace is ready.

Verification on 2026-10-31: self-contained Windows x64 publish succeeded; all 435 core checks and 44 isolated WPF checks passed. Native EXE inspection covered empty and populated tables, ribbon labels, and selected/focused ledger cells using fictional data. The 1000 × 640 workspace and Ctrl+F focus were checked in WPF. Update detection from 0.1.0.1 to 0.1.0.2 passed with simulated channel/release responses; a live install/restart is still pending publication. Alternate DPI and multiple-monitor coverage remain pending. The public update channel is promoted only after the verified release assets are published.