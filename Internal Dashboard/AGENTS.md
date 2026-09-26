# Internal Dashboard ownership

Read [Tool Description.md](Tool%20Description.md) before modifying this tool.

Keep this owner's development inventory inside the game repository. Include
relevant tool changes when Anas asks to commit/push the project. Generated
snapshots, source evidence and native artwork under `dist/` are intentional
checked-in deliverables; compiler/cache/runtime files stay ignored.

Do not modify gameplay or migrate old concepts while doing dashboard work.
Archive presence does not imply owner approval. Preserve uncertainty, source
IDs, paths, original art and all version/rank histories.

Run `Refresh-Inventory.ps1` after relevant game content/art changes. Run
`Refresh-Legacy.ps1` when original legacy source changes. Validate both
inventories and inspect the affected UI before handing off or including the
tool in an authorized commit/push. Frontend sources live in `dist/`.
