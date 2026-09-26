# Voidfall Internal Dashboard

The tool lives in this game repository at **Internal Dashboard/**.
Read [Tool Description.md](Tool%20Description.md) for scope, maintenance,
evidence limits and inclusion in authorized project commits/pushes.

Double-click **Open-Dashboard.cmd** for http://127.0.0.1:4326,
or open **dist/index.html** offline.

- Current Unity: cataloged cards/ranks, weapons/evolutions, enemy tiers/forms,
  bosses, maps, offer pools and all first-party image assets.
- Legacy Voidfall: five source versions, native source-rendered artwork,
  current comparisons, every card rank, source evidence and version history.
  Includes retired catalogs, sound definitions, challenges and micro-events.

Run **Refresh-Inventory.ps1** after game content/art changes. Run
**Refresh-Legacy.ps1** when original legacy sources change. Regeneration uses
Python, .NET and Node 24; `npm ci` supplies Canvas if needed. Viewing the
bundled static dashboard requires no installations.

Validate with `python -X utf8 validate.py` and
`python -X utf8 validate-legacy.py`. Missing means absent as a named catalog
entry; Present ID does not imply exact art/behavior parity. Migration choices
remain with Anas. Gameplay sources are never rewritten by this tool.
