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
dotnet msbuild MRD-Tools/MRDTools.csproj -t:Bundle
dotnet msbuild MRD-Tools/MRDTools.csproj -t:DeployLocal
```

- **Bundle** builds all four years and writes `artifacts\External Access Links Manager.bundle.zip`, the same layout for every install. The unzipped bundle is staged in `%LOCALAPPDATA%\MRD-Tools\bundle`.
- **DeployLocal** also installs it per-user in `%APPDATA%\Autodesk\ApplicationPlugins`, which Revit 2024–2027 all read. Close Revit first.
- Remove any machine-wide copy before testing a per-user one: `C:\ProgramData\Autodesk\ApplicationPlugins` (Revit 2024–2026) or `C:\Program Files\Autodesk\ApplicationPlugins` (Revit 2027). Otherwise Revit sees two add-ins with the same AddInId. DeployLocal warns when it finds one.
- To release a new version, change `<Version>` in the csproj. `PackageContents.xml` and the version dialog pick it up.

## Shipped dependencies

- **EPPlus 4.5.3.3** for every year.
- **Revit 2025/2026** also ship `Microsoft.Extensions.Configuration*` 7.0. On .NET 8, EPPlus reads its settings through these. Revit 2025 has the core 7.0 assemblies but not the Json or file-provider ones. Where Revit has its own copy, Revit's is used.
- **Revit 2027** gets all of these from the ASP.NET Core runtime that Revit loads.
