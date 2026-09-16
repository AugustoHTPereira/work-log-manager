# WorkLogManager — Windows installer

This folder contains everything needed to build a native Windows installer (`.exe`) for
WorkLogManager: a single self-contained executable that serves both the API and the
production front-end build, registered as a Windows Service, with a local SQLite
database that is created and migrated automatically on first run.

Building the installer is automated via GitHub Actions
(`.github/workflows/build-installer.yml`), which runs whenever a GitHub release is
published and attaches the generated `.exe` to that release. The manual process
described below remains available as a fallback (e.g. to build/test locally before
cutting a release).

## Prerequisites (Windows machine)

1. **.NET 10 SDK** — https://dotnet.microsoft.com/download
2. **Node.js** (LTS) and npm — https://nodejs.org
3. **Inno Setup 6** — https://jrsoftware.org/isinfo.php (installs `ISCC.exe`, the
   command-line compiler used by `build.ps1`)

## Building the installer

From a PowerShell prompt, in this `installer/` folder:

```powershell
./build.ps1 -Version 1.0.0
```

This script:

1. Publishes `WorkLogManager.Api` as a self-contained `win-x64` build into
   `installer/publish/api/`.
2. Builds the front-end (`app/`) production bundle and copies it into
   `installer/publish/api/wwwroot/`.
3. Compiles `WorkLogManager.iss` with Inno Setup (`ISCC.exe`), producing
   `installer/output/WorkLogManagerSetup-<version>.exe`.

`installer/publish/` and `installer/output/` are build artifacts, git-ignored — they are
never committed.

## What the installer does

- Installs the application to `Program Files\WorkLogManager` (or wherever the user picks
  in the wizard).
- Registers a Windows Service named `WorkLogManagerApi`, running under `LocalSystem`,
  set to start automatically with Windows.
- Starts the service. On first start, the application creates its SQLite database file
  at `%ProgramData%\WorkLogManager\worklogmanager.db` and applies pending EF Core
  migrations automatically — no manual database setup step.
- On **upgrade** (running the installer again over an existing installation): stops and
  re-registers the service, replaces the application binaries, but never touches
  `%ProgramData%\WorkLogManager\worklogmanager.db` (existing data is preserved). The
  next service start applies any new migrations over the existing data.
- On **uninstall**: stops and removes the service, and deletes **everything** — the
  installation directory and `%ProgramData%\WorkLogManager` (including the database
  file). This is a deliberate decision: the uninstaller leaves no residue on the
  machine. If you need to keep the data, back up
  `%ProgramData%\WorkLogManager\worklogmanager.db` manually before uninstalling.

## Publishing a release (automated)

1. Create a new release on GitHub (tag it, e.g. `v1.0.0`) and publish it.
2. The `Build Windows Installer` workflow (`.github/workflows/build-installer.yml`)
   picks up the `release: published` event, builds the installer on a `windows-latest`
   runner (installing the .NET SDK, Node.js and Inno Setup on the fly), and attaches
   the resulting `WorkLogManagerSetup-<x.y.z>.exe` to that same release automatically.
   The version stamped on the installer is derived from the release tag (a leading `v`
   is stripped, e.g. tag `v1.0.0` → installer version `1.0.0`).
3. The workflow can also be triggered manually (`workflow_dispatch`, from the Actions
   tab) to test the build pipeline without publishing a release; in that case the `.exe`
   is only uploaded as a workflow run artifact, not attached to any release.

**Known limitation**: this workflow was authored and reviewed for syntax correctness in
a development environment without access to a real GitHub Actions Windows runner — it
has not been executed end-to-end. Verify the first real run carefully (Actions tab logs)
and be ready to adjust action versions/`choco` availability if the runner image changes.

## Publishing a release (manual fallback)

If the automated workflow is unavailable, the installer can still be built and uploaded
by hand:

1. Run `./build.ps1 -Version <x.y.z>` on a Windows machine.
2. Test the generated `installer/output/WorkLogManagerSetup-<x.y.z>.exe` locally
   (install, exercise the app, upgrade over it, uninstall — see the test plan below).
3. Create a new release on GitHub and upload the `.exe` as a release asset.

## Manual test plan (Windows machine/VM — not automatable in this repository's CI)

- **Fresh install**: run the installer, confirm the wizard completes without CLI usage,
  confirm the Windows Service `WorkLogManagerApi` is running, and that
  `http://localhost:5000/` serves the app (fixed port, set explicitly via
  `ASPNETCORE_URLS` in the service's environment — see `WorkLogManager.iss`). Also
  confirm the desktop and Start Menu shortcuts were created and that double-clicking
  either one opens `http://localhost:5000/` in the default browser.
- **Upgrade preserves data**: install, create some data through the app (e.g. add an
  employee), build/run a newer version of the installer over the existing installation,
  and confirm the data is still there after the upgrade completes and the service
  restarts.
- **Uninstall removes everything**: uninstall via *Settings > Apps*, and confirm neither
  the installation directory nor `%ProgramData%\WorkLogManager` (nor the `.db` file)
  remain on disk.

## Known limitation of this repository's automated tooling

`WorkLogManager.iss`, `build.ps1` and `.github/workflows/build-installer.yml` are
written and manually reviewed for syntax, but none of them could be compiled or
executed in the development environment used to author them (macOS/Linux, without Inno
Setup, a Windows host, or the ability to trigger real GitHub Actions runs). They must be
validated end-to-end — the GitHub Actions workflow by watching its first real run in the
Actions tab, and the `.iss`/`build.ps1` scripts by a developer on a real Windows
machine — before being trusted for a release.
