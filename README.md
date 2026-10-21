# External Access Links Manager

Revit add-in by Mike Richardson Design for reviewing, repathing, loading and unloading links in Revit projects without opening them. One project builds it for Revit 2024–2027.

## Build

| Revit | Configuration | Framework |
|---|---|---|
| 2024 | `Release R24` | net48 |
| 2025 | `Release R25` | net8.0-windows |
| 2026 | `Release R26` | net8.0-windows |
| 2027 | `Release R27` | net10.0-windows |

Pick the configuration in the Visual Studio toolbar, or from the command line:

```
dotnet build MRD-Tools/MRDTools.csproj -c "Release R27"
```

The Revit API comes from the `Nice3point.Revit.Api` NuGet packages, so a year builds without that Revit installed. Each year restores into its own `obj\R20xx\` folder.

## Bundle and deploy

```
dotnet build MRD-Tools/MRDTools.csproj -t:Deploy
```

This builds all four years in Release and stages them in `C:\Temp\LinksManager-Deploy`, laid out like an ApplicationPlugins folder. It never writes to a live add-in folder, needs no admin rights, and isn't part of a normal build.

```
C:\Temp\LinksManager-Deploy\
  External Access Links Manager.bundle\       copy into an ApplicationPlugins folder
  External Access Links Manager.bundle.zip    the same bundle zipped, for the App Store
```

- **Per-user install:** with Revit closed, delete any old `External Access Links Manager.bundle` from `%APPDATA%\Autodesk\ApplicationPlugins`, then copy the staged folder in. Revit 2024–2027 all read that folder.
- **Duplicates:** remove any machine-wide copy first, from `C:\ProgramData\Autodesk\ApplicationPlugins` (Revit 2024–2026) or `C:\Program Files\Autodesk\ApplicationPlugins` (Revit 2027). Otherwise Revit sees two add-ins with the same AddInId. The Deploy target warns when it finds one.
- **Options:** `-p:StageRoot=D:\somewhere` stages elsewhere (a folder of its own: it is emptied first). `-p:BundleConfiguration=Debug` stages Debug builds. `-p:DeployRoot="<an ApplicationPlugins folder>"` writes straight to a live folder, with no zip and nothing deleted.
- **New version:** change `<Version>` in the csproj. `PackageContents.xml` and the version dialog pick it up.

The full step-by-step is in the Deploy Runbook.

## Shipped dependencies

- **EPPlus 4.5.3.3** for every year.
- **Revit 2025/2026** also ship `Microsoft.Extensions.Configuration*` 7.0. On .NET 8, EPPlus reads its settings through these. Revit 2025 has the core 7.0 assemblies but not the Json or file-provider ones. Where Revit has its own copy, Revit's is used.
- **Revit 2027** gets all of these from the ASP.NET Core runtime that Revit loads.
