# External Access Links Manager

A Revit add-in by Mike Richardson Design for reviewing, repathing, loading and unloading the links in Revit projects without opening them. One project builds it for Revit 2024–2027.

## Install

Download `LinksManager-<version>-Setup.exe` from the latest [release](../../releases) and run it. It installs for you only, needs no admin rights, and covers every Revit year from 2024 to 2027.

The setup isn't digitally signed, so expect two one-off prompts:

- **Windows SmartScreen:** "Windows protected your PC". Choose **More info**, then **Run anyway**.
- **Revit:** "Security – Unsigned Add-In" the first time each Revit year loads it. Choose **Always Load**.

The add-in appears as **Links Manager** in the **MRD Tools** panel on the **Add-Ins** tab. To remove it, uninstall **External Access Links Manager** from **Settings › Apps**.

To install without the setup, unzip `External Access Links Manager.bundle.zip` from the same release into `%APPDATA%\Autodesk\ApplicationPlugins`, with Revit closed.

## Build

| Revit | Configuration | Framework |
|---|---|---|
| 2024 | `Release R24` | net48 |
| 2025 | `Release R25` | net8.0-windows |
| 2026 | `Release R26` | net8.0-windows |
| 2027 | `Release R27` | net10.0-windows |

Pick the configuration in the Visual Studio toolbar, or from the `MRD-Tools` folder:

```
dotnet build MRDTools.csproj -c "Release R27"
```

The Revit API comes from the `Nice3point.Revit.Api` NuGet packages, so a year builds without that Revit installed. Each year restores into its own `obj\R20xx\` folder.

## Checks

`tests\MRD-Tools.Checks` checks everything that doesn't need Revit: the link model, the text report, the Excel export and import, looking for linked files, the remembered folders, and the windows themselves, which it opens off screen and drives through their actions. From the repo root:

```
dotnet run --project tests\MRD-Tools.Checks
```

It builds the add-in's default (Revit 2027) version, which compiles the same code as the other years, runs every check, prints any that fail and ends with a count. It shows the Windows folder picker for a moment and presses its buttons, so run it at a desktop. Its test files and screenshots of the windows go in `%TEMP%\MRD-Tools.Checks`, or the folder after `--out`.

- **Help page screenshots:** `--update-help` also copies the help page's screenshots into `bundle\Contents\Resources`. They're rendered from the real windows with made-up projects.
- **What needs Revit:** reading and writing a project's links (TransmissionData) and the Saved In details (BasicFileInfo) only run inside Revit, so test those in Revit itself.

## Stage and package

From the `MRD-Tools` folder:

```
dotnet build MRDTools.csproj -t:Deploy
```

This builds all four years in Release, stages them in `C:\Temp\LinksManager-Deploy\Public\` and builds the per-user setup with [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup --scope user`). It never writes to a live add-in folder, needs no admin rights, and isn't part of a normal build.

```
C:\Temp\LinksManager-Deploy\Public\
  ApplicationPlugins\External Access Links Manager.bundle\     copies into %APPDATA%\Autodesk\ApplicationPlugins
  External Access Links Manager.bundle.zip
  LinksManager-<version>-Setup.exe
```

- **Silent install:** `<setup>.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
- **One copy per computer:** if the add-in is already installed for all users, the setup stops, because Revit would refuse the duplicate.
- **Trying a setup safely:** `<setup>.exe /TestRoot=<an empty folder>` installs the bundle under that folder instead of Revit's; uninstall it from Apps afterwards.
- **Options:** `-p:StageRoot=D:\somewhere` stages elsewhere (the flavour's subfolder is emptied first). `-p:BundleConfiguration=Debug` stages Debug builds. `-p:BuildInstaller=false` skips the setup.
- **Other flavours:** set `ExtraDeployTargets` in `MRD-Tools\Deploy.local.props` (not committed) to a targets file of your own. For its `-p:Flavour=<name>` it adds `BundleRevitYear` items, each with the `Location` folder its bundle is staged under, and sets `SetupName` and `InstallerScript`. See the comment at the top of `MRD-Tools\Deploy.targets`.
- **New version:** change `<Version>` in the csproj. The bundle manifest, the help page, the setup and the version dialog all follow it.

## Release

Pushing a tag that matches the csproj version, such as `v10.0.0`, runs [`.github/workflows/release.yml`](.github/workflows/release.yml). It builds the setup and attaches it, with the bundle zip, to a GitHub release.

## Shipped dependencies

- **EPPlus 4.5.3.3** for every year (LGPL-3.0).
- **Revit 2025/2026** also ship `Microsoft.Extensions.Configuration*` 7.0 (MIT). On .NET 8, EPPlus reads its settings through these. Revit 2025 has the core 7.0 assemblies but not the Json or file-provider ones. Where Revit has its own copy, Revit's is used.
- **Revit 2027** gets all of these from the ASP.NET Core runtime that Revit loads.

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licence

[MIT](LICENSE).
