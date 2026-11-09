# Release guide

## Baseline and numbering

The initial development baseline is **0.1.0.0**. Version components are **Release.Feature.Fix.Minor**. Incrementing one component resets every component to its right to zero. Confirm the change classification and proposed version with the user before publishing.

- Minor adjustment: 0.1.0.1.
- Feature fix: 0.1.1.0.
- New feature: 0.2.0.0.
- Stable release: 1.0.0.0.

Use **app-v** followed by the exact four-part application version for tags. The baseline tag is **app-v0.1.0.0**. Asset names use the same version: **Piggy-Piggy-0.1.0.0-win-x64.zip** and its **.sha256** file.

## Documents and update channel

The baseline retains **document format 9** and **UpdateEpoch=3**. These are independent of the displayed application version. Keep the epoch at 3 for ordinary future updates. Do not reset it when simplifying release history. Existing compatibility with document formats 1–8 remains supported; opening a file does not rewrite it until Save.

**update-channel.json** names the active published tag. Clients validate the channel, exact release identity, publication status, asset URLs, SHA-256 checksum and EXE version. Within the current epoch, version comparison is numeric and older/equal versions are not offered. Fix forward rather than raising the epoch to force an ordinary rollback.

## Publishing

The optional web installer is built separately with `build-installer.ps1`. It follows the existing update channel at install time and does not need rebuilding for each ordinary app release. Keep the exact ZIP/checksum naming and three-file payload contract in `docs/INSTALLER.md`. A new launcher is required for incompatible protocol/install changes. Do not silently replace an already published installer asset; version a changed launcher when publishing it.

1. Run core tests and isolated WPF checks. Build and inspect the portable EXE using fictional data, including Save/Open compatibility. `publish.ps1` runs the checks and produces the package.
2. Commit and push the reviewed changes. Create the matching **app-v** tag and wait for successful main/tag CI. The tag workflow creates a draft release with ZIP and checksum assets.
3. Verify the draft package and checksum, then publish it with **make_latest=true**.
4. After the assets are public, promote **update-channel.json** and verify that the installed client detects the correct update and can access its verified assets.
5. Keep README download links, release notes and verification evidence current.

Do not move published tags or replace published assets during normal releases. Archive and confirm any exceptional history cleanup separately. Do not reuse an existing tag to publish a different application binary.
