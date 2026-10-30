# Verification — 0.1.0.0

## Baseline scope

The initial baseline includes bank accounts, Income/Expense/Transfer with separate Admin Fees, both Income fee bases, Purpose allocations, persistent Period/Pay Cycle, Wishlist, Debt and Self Debt. The version is **0.1.0.0**, update epoch **3**, and document format **9**.

## Automated checks

The baseline suite contains **433 core checks** and **20 isolated WPF checks**. Coverage includes accounting and historical balances, required categories, both fee bases, manual Save/Open, format-1–8 compatibility, format-9 Wishlist/Debt links, reservations, debt repayments and corrections, persistent report periods, compact accessible ledger icons, ellipsis/tooltips, flexible numeric searches, updater refresh/retry and channel ordering.

The WPF checks instantiate production controls in an isolated STA process. They check behavior and measurement, not rendered pixels. Update HTTP cases use simulated responses unless a live check is explicitly recorded.

## Piggy - Piggy baseline

The C#/WPF source was copied into a separate, clean directory from the 0.1.0.0 baseline, then renamed to **Piggy - Piggy**. The repository is **Rhahee/piggy-piggy**, the executable is **Piggy - Piggy.exe**, and update packages use **Piggy-Piggy-VERSION-win-x64.zip**. Internal C# namespaces remain stable; financial document fields and calculations are unchanged.

The new repository starts independently. Previous Git history, experimental toolkits, build caches, release backups and private `.mpb` documents are not included. Test and build output can be directed outside the source directory using the .NET SDK's `--artifacts-path` option.

On **2026-10-30**, all **433 core checks** and **20 isolated WPF checks** passed from the renamed source. A self-contained Windows x64 EXE built successfully with FileVersion **0.1.0.0** and ProductName **Piggy - Piggy**. Native UI inspection confirmed the new name on the title bar, start screen and Open document dialog, the `.mpb` filter, and the displayed version. Canceling Open returned to the start screen without loading or changing a financial document.

Release and checksum requests are restricted to the new repository. Automated updater cases use simulated responses; a complete live update installation requires published releases in this repository.

## Limits

Alternate DPI/multiple monitors, real-user financial documents, very large production datasets and live update installation are not re-exercised by a branding change. Automated checks do not replace inspection of the final portable EXE.

Screenshots, fictional documents and local test artifacts remain outside Git. The original project and private financial documents remain in their existing locations.
