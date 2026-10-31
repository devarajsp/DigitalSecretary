# DigitalSecretary.Kit — shared helper kit

Small, pure, contract-free helpers that more than one feature needs (ENH-12, v2.6.0). Before the kit,
features copied these files because features may not reference each other; the "third copy" trigger
was reached with Similar Pictures and Similar Videos would have made a fourth.

| Type | What it does | Used by |
|---|---|---|
| `ByteSize` | "1.44 GB" - culture-free byte formatting | DeDupe Files, Storage Studio, Similar Pictures, Similar Videos |
| `PauseToken` | pause/resume gate a long loop waits on once per item | the same four |
| `FileBytes` | byte-for-byte file comparison (exact copies) | Similar Pictures, Similar Videos |
| `GrayImage`, `PictureOrientation`, `Orientations` | greyscale pixels, area-average resize, 8 turns/mirrors, sharpness | Similar Pictures, Similar Videos |
| `ImageHasher`, `Fingerprint` | pHash (DCT) + dHash, in 8 orientations | Similar Pictures, Similar Videos |
| `ReviewFolder`, `MoveJob`, `MoveBatch`, `RemoveAction` | move files to an undo-able review folder or the Recycle Bin; undo; last-batch file | Similar Pictures, Similar Videos |

## Rules
- **Not part of the plugin contract.** The host never references the kit, and no kit type crosses the
  host/feature boundary. A feature opts in with `<UseKit>true</UseKit>` in its `.csproj`
  (`src/Features/Directory.Build.props` adds the reference and a global `using DigitalSecretary.Kit;`).
- **Each plugin ships its own copy** of `DigitalSecretary.Kit.dll` and loads it in its own
  `PluginLoadContext`, so features stay independent and can be deleted one by one.
- **Pure helpers only.** No feature rules (e.g. "the keeper never moves" stays in each feature), no UI,
  no persistence location of its own. Every type is unit-tested under `tests/DigitalSecretary.UnitTests/Kit`
  or through the features that use it.
- Changing a kit type changes every feature that uses it: run `./build.ps1 -All` and keep it green.
