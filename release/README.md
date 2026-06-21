# Releases

User-downloadable builds of Digital Secretary.

## For users — download & run
Get the latest build from the **[Releases page](../../releases/latest)** (the `…-win-x64.zip` asset),
then:
- **Portable:** unzip anywhere and run `DigitalSecretary.exe`, **or**
- **Install:** run `Install.cmd` (adds Start Menu + Desktop shortcuts; `Uninstall.cmd` removes it).

The build is **self-contained** — no .NET install required. Windows 10/11, 64-bit.

## For maintainers — how releases are made
- **Binaries are NOT committed to git** (they're gitignored). They live only as **GitHub Release
  assets**, so the repo stays small.
- Build the artifact locally:
  ```powershell
  ./tools/package-release.ps1 -Version 2.0.0 -Runtime win-x64
  # -> release/DigitalSecretary-v2.0.0-win-x64.zip  (self-contained: exe + plugins + manual + installer)
  ```
- `tools/publish-github.ps1` builds this zip and uploads it to the matching GitHub Release.
- Bump the version in `tools/package-release.ps1` invocation + `CHANGELOG.md` for each release.

The zip contains: `DigitalSecretary.exe`, the `plugins/` folder, `User-Manual.html`,
`Install.cmd` / `Uninstall.cmd`, and `README.txt`.
