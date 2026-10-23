using System.Collections.Generic;
using System.IO;

namespace MRDTools.Checks
{
    /// <summary>
    /// Made-up links and projects. None of their files exist, except in TestFiles.
    /// </summary>
    static class Samples
    {
        public const string Host = @"C:\Proj\Host.rvt";

        /// <summary>A link of Host whose last saved and desired states are the same, as when just read.</summary>
        public static LinkRow Row(string id, LinkKind kind, string path, string pathType, string state) =>
            new LinkRow(Host, id, kind, path, pathType, state, "01-Jan-2026 10:00", path, pathType, state);

        // --- Projects for the window and the help page's screenshots
        public const string Saved = "20-Feb-2015 12:06";

        public static LinkRow Link(string project, string id, LinkKind kind, string path, string type, string state = "Loaded") =>
            new LinkRow(project, id, kind, path, type, state, state == "NotFound" ? "Date Not Found" : kind == LinkKind.Other ? "N/A" : Saved,
                path, type, state);

        public const string Riverside = @"C:\Projects\Riverside\Riverside - Central.rvt";
        public const string TowerA = @"C:\Projects\Tower A\Tower A - Central.rvt";
        public const string TowerB = @"C:\Projects\Tower B\Tower B - Central.rvt";

        /// <summary>Three projects: Riverside with links of every kind, Tower A with a few, and Tower B, which couldn't be read.</summary>
        public static List<(string path, List<LinkRow> rows, string error)> BatchProjects()
        {
            var riverside = new List<LinkRow>
            {
                Link(Riverside, "285101", LinkKind.Revit, @"C:\Projects\Shared\Site\RVT Link A.rvt", "Absolute"),
                Link(Riverside, "285102", LinkKind.Revit, @"Links\RVT Links\RVT Link B.rvt", "Relative"),
                Link(Riverside, "285103", LinkKind.Revit, @"Links\RVT Links\RVT Link C.rvt", "Relative"),
                Link(Riverside, "285104", LinkKind.Revit, @"Links\RVT Links\RVT Link D.rvt", "Relative", "NotFound"),
            };
            for (int i = 1; i <= 5; i++) riverside.Add(Link(Riverside, "2853" + i, LinkKind.Cad, $@"Links\DWG Links\Drawing{i}.dwg", "Relative"));
            for (int i = 1; i <= 4; i++) riverside.Add(Link(Riverside, "2854" + i, LinkKind.Dwf, $@"Links\DWF Links\Markup-100{i}.dwf", "Relative"));
            riverside.Add(Link(Riverside, "285501", LinkKind.Other, @"C:\ProgramData\Autodesk\RVT 2027\Libraries\UK\RevitKeynotes_Metric.txt", "Absolute"));

            var towerA = new List<LinkRow>
            {
                Link(TowerA, "301201", LinkKind.Revit, @"..\Shared\Structure\Structure.rvt", "Relative"),
                Link(TowerA, "301202", LinkKind.Revit, @"..\Shared\MEP\MEP.rvt", "Relative"),
                Link(TowerA, "301203", LinkKind.Revit, @"C:\Projects\Shared\Site\Site.rvt", "Absolute"),
            };
            for (int i = 1; i <= 2; i++) towerA.Add(Link(TowerA, "3013" + i, LinkKind.Cad, $@"..\Shared\CAD\Grid{i}.dwg", "Relative"));
            towerA.Add(Link(TowerA, "301501", LinkKind.Other, @"..\Shared\Standards\Assembly Codes.txt", "Relative"));

            return new List<(string, List<LinkRow>, string)>
            {
                (Riverside, riverside, null),
                (TowerA, towerA, null),
                (TowerB, null, "the file is in use"),
            };
        }

        /// <summary>One project with Revit, CAD, DWF and Other links, for the CAD tab's screenshot.</summary>
        public static (string path, List<LinkRow> rows) SingleProject()
        {
            const string project = @"C:\Projects\Testing\Project_2027_testing.rvt";
            var links = new List<LinkRow>();
            for (int i = 1; i <= 4; i++) links.Add(Link(project, "28510" + i, LinkKind.Revit, $@"Links\RVT Links\RVT Link {(char)('A' + i - 1)}.rvt", "Relative"));
            links.Add(Link(project, "285305", LinkKind.Cad, @"Links\DWG Links\Drawing1.dwg", "Relative"));
            links.Add(Link(project, "285858", LinkKind.Cad, @"Links\DWG Links\Drawing2.dwg", "Relative"));
            links.Add(Link(project, "286220", LinkKind.Cad, @"Links\DWG Links\Drawing3.dwg", "Relative"));
            links.Add(Link(project, "286582", LinkKind.Cad, @"Links\DWG Links\Drawing4.dwg", "Relative"));
            links.Add(Link(project, "286944", LinkKind.Cad, @"Links\DWG Links\Drawing5.dwg", "Relative", "NotFound"));
            for (int i = 1; i <= 4; i++) links.Add(Link(project, "2854" + i, LinkKind.Dwf, $@"Links\DWF Links\Markup-100{i}.dwf", "Relative"));
            links.Add(Link(project, "285501", LinkKind.Other, @"C:\ProgramData\Autodesk\RVT 2027\Libraries\UK\RevitKeynotes_Metric.txt", "Absolute"));
            links.Add(Link(project, "285502", LinkKind.Other, @"..\..\Standards\Assembly Codes.txt", "Relative"));
            return (project, links);
        }
    }

    /// <summary>
    /// A folder of real files, for the checks that look for links' files: a project folder whose Links\Here.rvt is there,
    /// and a Shared.dwg beside it, but nothing else.
    /// </summary>
    sealed class TestFiles
    {
        public TestFiles(string outDir)
        {
            // Windows hands back the long form of a folder (no ~1 short names, as the temp folder can have), as the
            // links manager will
            Root = Path.GetFullPath(Path.Combine(outDir, "found-test"));
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
            Directory.CreateDirectory(Path.Combine(Root, "Project", "Links"));
            Project = Path.Combine(Root, "Project", "Host.rvt");
            SecondProject = Path.Combine(Root, "Second", "Second.rvt");
            Here = Path.Combine(Root, "Project", "Links", "Here.rvt");
            Shared = Path.Combine(Root, "Shared.dwg");
            File.WriteAllText(Here, "");
            File.WriteAllText(Shared, "");
        }

        public string Root { get; }
        public string Project { get; }
        public string SecondProject { get; }

        /// <summary>The one Revit link file that's there, in the project's Links folder.</summary>
        public string Here { get; }

        /// <summary>A CAD file beside the project's folder.</summary>
        public string Shared { get; }

        public LinkRow Link(string project, string id, LinkKind kind, string path, string type) =>
            new LinkRow(project, id, kind, path, type, "Loaded", Samples.Saved, path, type, "Loaded");
    }
}
