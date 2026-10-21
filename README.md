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
- **New version:** change `<Version>` in the csproj. The bundle manifest, the setup and the version dialog all follow it.

## Release

Pushing a tag that matches the csproj version, such as `v10.0.0`, runs [`.github/workflows/release.yml`](.github/workflows/release.yml). It builds the setup and attaches it, with the bundle zip, to a GitHub release.

## Shipped dependencies

- **EPPlus 4.5.3.3** for every year (LGPL-3.0).
- **Revit 2025/2026** also ship `Microsoft.Extensions.Configuration*` 7.0 (MIT). On .NET 8, EPPlus reads its settings through these. Revit 2025 has the core 7.0 assemblies but not the Json or file-provider ones. Where Revit has its own copy, Revit's is used.
- **Revit 2027** gets all of these from the ASP.NET Core runtime that Revit loads.

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licence

[MIT](LICENSE).
