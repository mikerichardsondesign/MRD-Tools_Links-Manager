using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using static MRDTools.Checks.Checker;
using static MRDTools.Checks.Samples;

namespace MRDTools.Checks
{
    /// <summary>
    /// The help page's screenshots, rendered from the real windows with the made-up projects. Their links' files are
    /// shown as found, as a real project's would be, except those Revit couldn't find when the project was saved.
    /// </summary>
    static class Screenshots
    {
        /// <summary>The screenshots the help page uses, by file name.</summary>
        public static readonly string[] HelpImages = { "links-manager.png", "links-manager-single.png", "find-replace.png" };

        public static void Render(string outDir)
        {
            LinksManagerWindow batch = Batch(outDir);
            Single(outDir);
            FindReplace(outDir, batch);
        }

        /// <summary>Three projects on the Revit tab, with an IFC link added to Tower A. Returns the window, left open.</summary>
        static LinksManagerWindow Batch(string outDir)
        {
            // A local copy, a model saved in an earlier version, and models that aren't workshared, as Revit would read them
            var window = new LinksManagerWindow
            {
                Width = 1440,
                Height = 820,
                readFileInfo = file =>
                    file.EndsWith("Structure.rvt") ? new LinkFileInfo("2025", true, false, true, true, false)
                    : file.EndsWith("MEP.rvt") ? new LinkFileInfo("2024", true, true, false, false, false)
                    : file.EndsWith("Site.rvt") || file.EndsWith(".ifc.rvt") ? new LinkFileInfo("2025", false, false, false, true, false)
                    : new LinkFileInfo("2025", true, true, false, true, false)
            };
            ShowOffscreen(window);
            foreach (var (path, rows, error) in BatchProjects())
            {
                if (path == TowerA) rows.Insert(3, Link(TowerA, "301204", LinkKind.Revit, @"..\Shared\Survey\Site Survey.ifc.rvt", "Relative"));
                window.AddProject(path, rows, error);
            }
            SetLastSaved(window, "22-Oct-2026 16:41", "21-Oct-2026 09:12", "20-Oct-2026 17:55");
            window.RefreshAll();
            Pump(window);

            List<LinkRow> revit = window.LinksGrid.Items.Cast<LinkRow>().ToList();
            revit[1].Repath(@"C:\Projects\Riverside\Links\RVT Links - Rev C\RVT Link B.rvt");
            revit[2].DesiredLoaded = false;
            window.LinksGrid.SelectedItem = revit[4];
            Check(WaitFor(() => window.PendingFileChecks == 0), "the screenshot's links have been looked for");
            foreach (LinkRow row in window.Links) Pretend(row, row.OriginalState != LinkConstants.StateNotFound);
            Check(WaitFor(() => window.FileInfoDone), "the screenshot's Revit links' details are read");
            Checker.Render(window, Path.Combine(outDir, "links-manager.png"));

            // Not for the help page: a look at the window at its smallest
            window.Width = window.MinWidth;
            Checker.Render(window, Path.Combine(outDir, "min-width.png"));
            return window;
        }

        /// <summary>One project, on the CAD tab.</summary>
        static void Single(string outDir)
        {
            var window = new LinksManagerWindow { Width = 1440, Height = 820 };
            ShowOffscreen(window);
            var (path, rows) = SingleProject();
            window.AddProject(path, rows, null);
            SetLastSaved(window, "22-Oct-2026 16:41");
            window.KindTabs.SelectedIndex = 1;
            window.RefreshAll();
            rows[4].Repath(@"C:\Projects\Testing\Links\DWG Links - Copy\Drawing1.dwg");
            rows[6].DesiredPathType = LinkConstants.PathTypeAbsolute;
            rows[8].DesiredLoaded = false;
            Check(WaitFor(() => window.PendingFileChecks == 0), "the single project's links have been looked for");
            foreach (LinkRow row in rows) Pretend(row, row.OriginalState != LinkConstants.StateNotFound);
            Checker.Render(window, Path.Combine(outDir, "links-manager-single.png"));
        }

        /// <summary>Find and Replace, looking in the Revit tab of the batch.</summary>
        static void FindReplace(string outDir, LinksManagerWindow batch)
        {
            var dialog = new FindReplaceWindow(new List<LinkRow>(), batch.RowsOnTab(), batch.Links.Where(r => r.IsEditable).ToList(), "Revit");
            ShowOffscreen(dialog);
            dialog.FindBox.Text = @"C:\Projects\Shared";
            dialog.ReplaceBox.Text = @"\\fileserver\Projects\Shared";
            Checker.Render(dialog, Path.Combine(outDir, "find-replace.png"));
            dialog.Close();
        }

        /// <summary>Sets the projects' Last Saved dates, which are otherwise read from their (missing) files.</summary>
        static void SetLastSaved(LinksManagerWindow window, params string[] dates)
        {
            FieldInfo field = typeof(ProjectRow).GetField("<LastSaved>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var projects = ((IEnumerable<ProjectRow>)window.ProjectsGrid.ItemsSource).ToList();
            for (int i = 0; i < projects.Count && i < dates.Length; i++) field.SetValue(projects[i], dates[i]);
        }
    }
}
