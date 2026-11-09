# Piggy visual identity — 0.1.0.1

The approved mark is a smiling magenta-and-white pig eating a warm gold coin, above the rounded **Piggy** wordmark. The user approved this initial direction on 2026-10-30. It replaces the earlier window-grid concepts.

## Application assets

- `Assets/Brand/piggy-logo.png`: the approved full logo, copied without visual changes from the selected concept. Its white background matches the start page.
- `Assets/app.png`: a complete gold-coin-only icon with transparent surroundings, derived from the approved coin. The pig and wordmark are omitted for small-size readability.
- `Assets/app.ico`: PNG-backed icon frames at 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels. Run `./tools/Build-AppIcon.ps1` on Windows to rebuild it from `Assets/app.png`.

The logo and coin derivative were created with the built-in imagegen tool. ICO conversion only resizes and packages the PNG; it does not redraw the artwork. Existing Fluent UI command icons keep their existing license and appearance.

## Document icon — 0.1.1.1

- `Assets/mpb.png`: the approved grayscale preview of the application coin, generated using built-in Imagegen from `Assets/app.png`, with transparent surroundings.
- `Assets/mpb.ico`: matching document icon frames at 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels. Rebuild with `./tools/Build-AppIcon.ps1 -Source ./Assets/mpb.png -Destination ./Assets/mpb.ico`.
- The application icon stays gold. Only `.mpb` documents associated with Piggy use the grayscale icon; existing Windows default choices are respected.

The image edit prompt requested only a neutral grayscale recolor while preserving the original silhouette, rings, central sparkle, shading and transparent padding; no text or document-sheet badge was added. ICO conversion only packages/resizes the generated PNG.

## Startup layout and display name

The start screen is one centered vertical group: **logo → New → Open**. The New and Open buttons are both 240 × 44 WPF units, with a 12-unit gap. Version and update controls remain at the bottom right. Window decorations use native Windows chrome.

The visible application name is **Piggy**. The executable and release archive retain their existing names, **Piggy - Piggy.exe** and **Piggy-Piggy-VERSION-win-x64.zip**, to remain compatible with the already published 0.1.0.0 updater. Internal C# namespaces, the `piggy-piggy` repository, `.mpb` documents, and accounting rules remain unchanged.

This identity ships in **0.1.0.1**. Executable and archive names remain compatible with the published 0.1.0.0 updater.

## Coin derivative prompt

> Extract and complete ONLY THE GOLD COIN from this approved Piggy brand logo as a standalone application icon. Remove the pig, hooves, snout, lettering, and all background. Reconstruct the coin portions hidden by the pig so the output is ONE WHOLE clean circular coin, not bitten or occluded. Match the exact warm gold color family and friendly flat cartoon style of the existing coin: golden yellow main face, deeper ochre-gold outer edge, one pale golden inset ring and one centered four-point pale-gold sparkle emblem. Front-facing circular medallion with a simple thick rim and minimal facet highlights, balanced and perfectly round, no perspective or 3D extrusion. Make the center sparkle large and readable at 16px. Smooth crisp silhouette, no thin tiny detail, no text or currency symbol, no magenta, no pig, no hands, no extra coins. GENUINELY TRANSPARENT background outside the coin, preserving fully opaque solid gold inside the coin and its pale-gold features. Do not create a white square, white border or checkerboard artwork. Fill about 84 percent of a square canvas with the centered coin, leaving equal transparent padding. This is the matching coin-only derivative icon of the supplied approved logo, not a new brand direction.
