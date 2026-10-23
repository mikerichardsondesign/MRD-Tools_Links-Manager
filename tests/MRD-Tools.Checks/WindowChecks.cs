using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using static MRDTools.Checks.Checker;
using static MRDTools.Checks.Samples;

namespace MRDTools.Checks
{
    /// <summary>
    /// The links manager window, filled with the made-up batch of projects and driven through its actions. None of the
    /// links' files exist, so every one is found missing.
    /// </summary>
    static class WindowChecks
    {
        public static void Run(string outDir)
        {
            var window = new LinksManagerWindow();
            ShowOffscreen(window);
            DataGrid grid = window.LinksGrid;
            int Shown() { Pump(window); return grid.Items.Count; }
            DataGridColumn Column(string header) => grid.Columns.First(c => c.Header as string == header);
            int ColumnIndex(string header) => grid.Columns.IndexOf(Column(header));

            // --- Empty
            Check(window.Icon != null, "the window has its icon");
            Check(window.TryFindResource("PrimaryDarkBrush") is SolidColorBrush, "the theme is merged into the window");
            Check(Application.Current.Resources.Count == 0, "and not into the application, so Revit's windows aren't restyled");
            Check(window.ProjectsHint.Visibility == Visibility.Visible, "an empty list shows how to add projects");
            Check(!window.MakeChangesButton.IsEnabled && !window.TextReportButton.IsEnabled && !window.ExcelButton.IsEnabled, "nothing to do with no projects");
            Eq("REVIT (0)", window.TabRevit.Header, "empty tab header");
            Eq(typeof(LinksManagerWindow).Assembly.GetName().Version.ToString(3), window.VersionText.Text, "version from the assembly");
            Check(window.WriteAllLinks.IsChecked != true, "Write all links is clear to start with");
            Check(ColumnIndex("PATH ON NEXT OPEN") < ColumnIndex("LAST SAVED PATH"), "the path you change comes before the last saved path");
            Check(typeof(LinksManagerWindow).GetField("AbsolutePaths", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null,
                "the Repath with absolute paths checkbox is gone");
            Check(typeof(LinksManagerWindow).GetField("OpenReportAfter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null,
                "the Open a text report afterwards checkbox is gone");
            Check(ColumnIndex("FOUND") + 1 == ColumnIndex("SAVED IN") && ColumnIndex("SAVED IN") + 1 == ColumnIndex("PATH ON NEXT OPEN"),
                "Found, then Saved In, sit just before the path they describe");
            Eq("FileStatus", Column("FOUND").SortMemberPath, "and sorts by whether the file was found");

            // --- Three projects added
            foreach (var (path, rows, error) in BatchProjects()) window.AddProject(path, rows, error);
            window.RefreshAll();
            var projects = (IList<ProjectRow>)window.ProjectsGrid.ItemsSource;
            Eq(3, projects.Count, "three projects listed");
            Check(window.ProjectsHint.Visibility != Visibility.Visible, "the hint goes once projects are added");
            Eq("3 projects", window.ProjectCount.Text, "project count");
            ProjectRow riverside = projects[0];
            ProjectRow towerA = projects[1];
            ProjectRow towerB = projects[2];
            Eq("Links read", riverside.Status, "status after reading");
            Check(!towerB.Process && towerB.HasError && towerB.Status == "Couldn't be read: the file is in use",
                "an unreadable project is flagged and not processed");
            Eq(14, riverside.LinkCount, "Riverside's link count");
            Eq(6, towerA.LinkCount, "Tower A's link count");
            Eq("Riverside - Central.rvt", riverside.Name, "project name");
            Eq(@"C:\Projects\Riverside", riverside.Folder, "project folder");
            Eq("REVIT (7)", window.TabRevit.Header, "Revit tab count");
            Eq("CAD (7)", window.TabCad.Header, "CAD tab count");
            Eq("DWF (4)", window.TabDwf.Header, "DWF tab count");
            Eq("OTHER (2)", window.TabOther.Header, "Other tab count");
            Eq(7, Shown(), "the Revit tab shows both projects' Revit links");
            Eq(Visibility.Visible, window.ProjectColumn.Visibility, "Project column with two projects");
            Check(WaitFor(() => window.PendingFileChecks == 0), "every link's file is looked for");
            Eq("20 link(s) in 2 project(s) · 18 not found", window.LinksSummary.Text, "links summary, counting the files not found (these made-up ones aren't anywhere)");
            Check(window.ChangesBadge.Visibility != Visibility.Visible, "no changes yet");
            Check(window.MakeChangesButton.IsEnabled && window.TextReportButton.IsEnabled && window.ExcelButton.IsEnabled, "actions enabled");
            Check(window.LinkTools.IsEnabled, "link tools on the Revit tab");

            // --- Edits update the counts as they happen
            List<LinkRow> revitRows = grid.Items.Cast<LinkRow>().ToList();
            revitRows[1].Repath(@"C:\Projects\Riverside\Links\RVT Links - Rev C\RVT Link B.rvt");
            revitRows[2].DesiredLoaded = false;
            Eq(@"Links\RVT Links - Rev C\RVT Link B.rvt", revitRows[1].DesiredPath, "relative repath");
            Eq(2, riverside.ChangeCount, "the project's change count follows edits");
            Eq("2 CHANGES", riverside.ChangesText, "project change badge text");
            Check(riverside.HasChanges && !towerA.HasChanges, "only Riverside has changes");
            Check(window.ChangesBadge.Visibility == Visibility.Visible, "the footer badge shows");
            Eq("2 LINKS CHANGED", window.ChangesBadgeText.Text, "footer badge text");

            // --- Tabs filter the grid
            window.KindTabs.SelectedIndex = 1;
            Eq(7, Shown(), "CAD tab");
            Check(window.LinkTools.IsEnabled, "link tools on the CAD tab");
            window.KindTabs.SelectedIndex = 3;
            Eq(2, Shown(), "Other tab");
            Check(!window.LinkTools.IsEnabled, "no link tools on the Other tab");
            window.KindTabs.SelectedIndex = 0;
            Eq(7, Shown(), "back on the Revit tab");

            // --- Bulk edits: Unload All, then Reset Changes with nothing selected undoes the tab
            grid.SelectedItems.Clear();
            Click(window, "UnloadAll_Click");
            Check(revitRows.All(r => !r.DesiredLoaded), "Unload All unloads every link on the tab");
            Eq("7 LINKS CHANGED", window.ChangesBadgeText.Text, "badge after Unload All");
            Click(window, "Reset_Click");
            Check(!revitRows.Any(r => r.IsDirty), "Reset Changes with nothing selected undoes every change on the tab");
            Eq(@"Links\RVT Links\RVT Link B.rvt", revitRows[1].DesiredPath, "including the repath");
            Check(window.ChangesBadge.Visibility != Visibility.Visible, "the badge hides again");
            Click(window, "LoadAll_Click");
            Check(!revitRows.Any(r => r.IsDirty), "Load All changes nothing when everything's loaded");

            // --- Selected rows
            grid.SelectedItems.Add(revitRows[4]);
            Eq(1, window.SelectedRows().Count, "one link selected");
            Click(window, "UnloadSelected_Click");
            Check(!revitRows[4].DesiredLoaded && revitRows.Count(r => r.IsDirty) == 1, "Unload Selected changes only the selection");
            Eq(1, towerA.ChangeCount, "Tower A now has the change");
            Click(window, "Reset_Click");
            Check(!revitRows[4].IsDirty, "Reset Changes undoes the selected link");
            grid.SelectedItems.Clear();

            // --- Process decides which links show, and the Project column
            string name = window.DefaultWorkbookName();
            Check(Regex.IsMatch(name, @"^\d\d\.\d\d\.\d{4} \d\d-\d\d_links_multiple projects$"), $"Excel name for two projects: {name}");
            Eq(DateTime.Now.ToString("dd.MM.yyyy"), name.Substring(0, 10), "it starts with today's UK date");
            towerA.Process = false;
            Eq("Not processed", towerA.Status, "an unticked project says it isn't processed");
            Check(window.DefaultWorkbookName().EndsWith("_links_Riverside - Central"), "Excel name for one project");
            Check(towerB.Status.StartsWith("Couldn't be read"), "an unreadable project keeps its reason");
            Eq(4, Shown(), "unticking Process hides the project's links");
            Eq("REVIT (4)", window.TabRevit.Header, "tab counts follow Process");
            Eq(Visibility.Collapsed, window.ProjectColumn.Visibility, "one project shown: no Project column");
            towerA.Process = true;
            Eq("Links read", towerA.Status, "ticking it again shows its status");
            Eq(7, Shown(), "ticking it again shows them");

            // --- Other links can't be edited, Revit links can
            window.KindTabs.SelectedIndex = 3;
            LinkRow otherRow = grid.Items.Cast<LinkRow>().First();
            Check(EditCancelled(window, Column("PATH ON NEXT OPEN"), otherRow), "an Other link's path can't be edited");
            window.KindTabs.SelectedIndex = 0;
            Check(!EditCancelled(window, Column("PATH ON NEXT OPEN"), revitRows[0]), "a Revit link's path can be edited");

            TypingAPath(window, revitRows[0], Column("PATH ON NEXT OPEN"));
            PathTypeList(window, revitRows[4], outDir);

            // --- Removing a project takes its links with it
            window.RemoveProjects(new List<ProjectRow> { towerA });
            Eq(2, projects.Count, "two projects left");
            Eq(4, Shown(), "Tower A's links are gone");
            Check(WaitFor(() => window.PendingFileChecks == 0), "the edited links' files are looked for");
            Eq("14 link(s) in 1 project(s) · 13 not found", window.LinksSummary.Text, "summary after removing");
        }

        /// <summary>Whether the grid refuses to start editing the link's cell, as it does for links that can't be changed.</summary>
        public static bool EditCancelled(LinksManagerWindow window, DataGridColumn column, LinkRow row)
        {
            var edit = new DataGridBeginningEditEventArgs(column, new DataGridRow { Item = row }, new RoutedEventArgs());
            CallPrivate(window, "LinksGrid_BeginningEdit", window.LinksGrid, edit);
            return edit.Cancel;
        }

        /// <summary>Typing a path: committing keeps it, cancelling (Esc) leaves the link as it was.</summary>
        static void TypingAPath(LinksManagerWindow window, LinkRow typed, DataGridColumn pathColumn)
        {
            DataGrid grid = window.LinksGrid;
            string before = typed.DesiredPath;
            bool TypeIntoPath(string text, bool commit)
            {
                grid.SelectedItem = typed;
                grid.CurrentCell = new DataGridCellInfo(typed, pathColumn);
                Pump(window);
                if (!grid.BeginEdit()) return false;
                Pump(window);
                var editor = PrivateField<TextBox>(window, "pathEditor");
                if (editor == null) return false;
                editor.Text = text;
                if (commit) grid.CommitEdit(DataGridEditingUnit.Cell, true);
                else grid.CancelEdit(DataGridEditingUnit.Cell);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                Pump(window);
                return true;
            }
            Check(TypeIntoPath(@"C:\Projects\Shared\Site\Typed.rvt", commit: false), "the path cell opens for typing");
            Eq(before, typed.DesiredPath, "cancelling a typed path leaves the link as it was");
            Check(TypeIntoPath(@"C:\Projects\Shared\Site\Typed.rvt", commit: true), "the path cell opens again");
            Eq(@"C:\Projects\Shared\Site\Typed.rvt", typed.DesiredPath, "committing a typed path changes the link");
            Eq("Typed.rvt", typed.DesiredName, "and its name");
            Check(typed.IsDirty, "and marks it changed");
            typed.ResetPath();
            grid.SelectedItems.Clear();
        }

        /// <summary>The Path Type list in a row: picking from it converts the link's path.</summary>
        static void PathTypeList(LinksManagerWindow window, LinkRow structure, string outDir)
        {
            DataGrid grid = window.LinksGrid;
            Pump(window);
            var list = FindChild<ComboBox>(grid, c => c.DataContext == structure);
            Check(list != null && list.Visibility == Visibility.Visible, "a Revit link has a Path Type list");
            if (list == null) return;
            Eq("Relative", list.SelectedItem, "the list shows the link's type");
            list.SelectedItem = "Absolute";
            Eq(@"C:\Projects\Shared\Structure\Structure.rvt", structure.DesiredPath, "picking Absolute converts the path");
            Check(structure.IsDirty, "and marks the link changed");
            list.SelectedItem = "Relative";
            Eq(@"..\Shared\Structure\Structure.rvt", structure.DesiredPath, "picking Relative converts it back");
            Check(!structure.IsDirty, "so it's no longer changed");

            list.IsDropDownOpen = true;
            Pump(window);
            var popup = (Popup)list.Template.FindName("PART_Popup", list);
            Check(popup.IsOpen, "the list opens");
            RenderElement((FrameworkElement)popup.Child, Path.Combine(outDir, "path-type-list.png"));
            list.IsDropDownOpen = false;

            window.KindTabs.SelectedIndex = 3;
            Pump(window);
            LinkRow firstOther = grid.Items.Cast<LinkRow>().First();
            var otherList = FindChild<ComboBox>(grid, c => c.DataContext == firstOther);
            Check(otherList == null || otherList.Visibility != Visibility.Visible, "an Other link has no Path Type list");
            window.KindTabs.SelectedIndex = 0;
            Pump(window);
        }
    }
}
