using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Threading;
using static MRDTools.Checks.Checker;

namespace MRDTools.Checks
{
    /// <summary>
    /// Whether each link's file is there, looked for in the background, with real files in a folder of the checks' own.
    /// </summary>
    static class FileCheckerChecks
    {
        public static void Run(TestFiles files)
        {
            // Which file a link looks for
            Eq(files.Here, files.Link(files.Project, "1", LinkKind.Revit, @"Links\Here.rvt", "Relative").FileToCheck(),
                "a relative path is looked for from the project's folder");
            Eq(files.Shared, files.Link(files.Project, "2", LinkKind.Cad, @"..\Shared.dwg", "Relative").FileToCheck(), @"and ..\ goes up from it");
            Eq(null, files.Link(files.Project, "3", LinkKind.Revit, "RSN://server/Projects/Central.rvt", "Server").FileToCheck(), "a Revit Server model isn't looked for");
            Eq(null, files.Link(files.Project, "4", LinkKind.Revit, "Autodesk Docs://Project/Model.rvt", "Absolute").FileToCheck(), "nor a cloud model");
            Eq(null, files.Link(files.Project, "5", LinkKind.Other, @"Links\Here.rvt", "Relative").FileToCheck(), "nor an Other reference");

            var checker = new FileChecker(Dispatcher.CurrentDispatcher);
            bool CheckerDone() => checker.Pending == 0;
            var here = files.Link(files.Project, "11", LinkKind.Revit, @"Links\Here.rvt", "Relative");
            var up = files.Link(files.Project, "12", LinkKind.Cad, @"..\Shared.dwg", "Relative");
            var full = files.Link(files.Project, "13", LinkKind.Cad, files.Shared, "Absolute");
            var gone = files.Link(files.Project, "14", LinkKind.Revit, @"Links\Gone.rvt", "Relative");
            var noFolder = files.Link(files.Project, "15", LinkKind.Dwf, @"Later\Markup.dwf", "Relative");
            var offShare = files.Link(files.Project, "16", LinkKind.Revit, @"\\no-such-server-mrd\share\A.rvt", "Absolute");
            var onServer = files.Link(files.Project, "17", LinkKind.Revit, "RSN://server/Projects/Central.rvt", "Server");
            var rows = new[] { here, up, full, gone, noFolder, offShare, onServer };

            var timer = Stopwatch.StartNew();
            foreach (LinkRow row in rows) checker.Check(row);
            Eq(LinkFileStatus.Checking, here.FileStatus, "a link is marked as being looked for straight away");
            Eq(LinkFileStatus.NotChecked, onServer.FileStatus, "a server model is marked as not looked for");
            Check(WaitFor(CheckerDone, 120), "the checks finish, even for a server that isn't there");
            Console.WriteLine($"  (file checks took {timer.Elapsed.TotalSeconds:0.0}s, with a server that isn't there)");
            Eq("Found,Found,Found,Missing,Missing,Missing,NotChecked", string.Join(",", rows.Select(r => r.FileStatus)), "found and missing files");
            Eq("Found: " + files.Here, here.FileStatusText, "the tooltip gives the full path found");
            Eq("Not found from this computer: " + Path.Combine(files.Root, "Project", "Links", "Gone.rvt"), gone.FileStatusText, "or not found");
            Eq("Revit Server and cloud models aren't looked for", onServer.FileStatusText, "a server model's tooltip says why");

            // A result for a file the link has since been pointed away from is ignored
            var moved = files.Link(files.Project, "18", LinkKind.Revit, @"Links\Here.rvt", "Relative");
            string firstFile = moved.BeginFileCheck();
            moved.DesiredPath = @"Links\Gone.rvt";
            string secondFile = moved.BeginFileCheck();
            moved.EndFileCheck(firstFile, true);
            Eq(LinkFileStatus.Checking, moved.FileStatus, "a result for a file the link has moved on from is ignored");
            moved.EndFileCheck(secondFile, false);
            Eq(LinkFileStatus.Missing, moved.FileStatus, "the result for the file it points at now is kept");

            // A folder that appears after a round of checks is found the next time
            Directory.CreateDirectory(Path.Combine(files.Root, "Project", "Later"));
            File.WriteAllText(Path.Combine(files.Root, "Project", "Later", "Markup.dwf"), "");
            checker.Check(noFolder);
            Check(WaitFor(CheckerDone), "the second round of checks finishes");
            Eq(LinkFileStatus.Found, noFolder.FileStatus, "a folder that appears later is found the next time");
        }
    }
}
