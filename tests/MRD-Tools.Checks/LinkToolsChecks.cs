using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OfficeOpenXml;
using static MRDTools.Checks.Checker;

namespace MRDTools.Checks
{
    /// <summary>
    /// One window over real files: the Found and Saved In columns, locked links, Find and Replace, what Make Changes says
    /// it will write, and the Excel round trip. The steps run in order, each carrying on from the last.
    /// </summary>
    sealed class LinkToolsChecks
    {
        readonly TestFiles files;
        readonly string outDir;
        readonly LinksManagerWindow window = new LinksManagerWindow();
        readonly List<string> infoReads = new List<string>();

        // Host.rvt's links: one whose file is there, one whose isn't, a Revit Server model and a CAD file; and Second.rvt's one
        readonly LinkRow here;
        readonly LinkRow gone;
        readonly LinkRow server;
        readonly LinkRow cad;
        readonly LinkRow second;

        public LinkToolsChecks(TestFiles files, string outDir)
        {
            this.files = files;
            this.outDir = outDir;
            here = files.Link(files.Project, "21", LinkKind.Revit, @"Links\Here.rvt", "Relative");
            gone = files.Link(files.Project, "22", LinkKind.Revit, @"Links\Gone.rvt", "Relative");
            server = files.Link(files.Project, "23", LinkKind.Revit, "RSN://server/Projects/Central.rvt", "Server");
            cad = files.Link(files.Project, "24", LinkKind.Cad, files.Shared, "Absolute");
            second = files.Link(files.SecondProject, "31", LinkKind.Revit, @"Links\X.rvt", "Relative");

            // Revit's BasicFileInfo stood in for: every file read is a 2025 central model
            window.readFileInfo = file =>
            {
                infoReads.Add(file);
                return new LinkFileInfo("2025", true, true, false, true, false);
            };
        }

        public void Run()
        {
            ShowOffscreen(window);
            window.AddProject(files.Project, new List<LinkRow> { here, gone, server, cad }, null);
            window.AddProject(files.SecondProject, new List<LinkRow> { second }, null);
            window.RefreshAll();

            FoundColumn();
            LockedLinks();
            SavedIn();
            FindAndReplace();
            MakeChangesPreview();
            FindReplaceDialog();
            ExcelRoundTrip();
        }

        TextBlock FoundIcon(LinkRow row) =>
            FindChild<TextBlock>(window.LinksGrid, t => t.DataContext == row && t.ToolTip != null && t.FontFamily.Source == "Segoe MDL2 Assets");

        Color RowColour(LinkRow row) => ((SolidColorBrush)((DataGridRow)window.LinksGrid.ItemContainerGenerator.ContainerFromItem(row)).Background).Color;

        Color Brush(string key) => ((SolidColorBrush)window.FindResource(key)).Color;

        void FoundColumn()
        {
            Check(WaitFor(() => window.PendingFileChecks == 0), "the window looks for every link's file");
            Eq("5 link(s) in 2 project(s) · 2 not found", window.LinksSummary.Text, "the summary counts the files not found");
            Pump(window);
            Eq(TickGlyph, FoundIcon(here)?.Text, "a tick for a file that's there");
            Eq(WarningGlyph, FoundIcon(gone)?.Text, "a warning for one that isn't");
            Eq(Brush("ErrorBrush"), (FoundIcon(gone)?.Foreground as SolidColorBrush)?.Color, "in red");
            Eq(DashGlyph, FoundIcon(server)?.Text, "a dash for a server model");
        }

        /// <summary>A read-only link: a lock, no Loaded box to tick, and left out of the tab's actions.</summary>
        void LockedLinks()
        {
            var serverLock = FindChild<TextBlock>(window.LinksGrid, t => t.DataContext == server && t.Text == LockGlyph);
            Check(serverLock != null && serverLock.Visibility == Visibility.Visible, "a read-only link shows a lock");
            var hereLock = FindChild<TextBlock>(window.LinksGrid, t => t.DataContext == here && t.Text == LockGlyph);
            Check(hereLock == null || hereLock.Visibility != Visibility.Visible, "an editable one doesn't");
            var serverLoaded = FindChild<CheckBox>(window.LinksGrid, c => c.DataContext == server);
            Check(serverLoaded != null && !serverLoaded.IsEnabled, "a read-only link's Loaded box can't be changed");
            Check(!window.RowsOnTab().Contains(server), "and the tab's actions leave it out");
            DataGridColumn pathColumn = window.LinksGrid.Columns.First(c => c.Header as string == "PATH ON NEXT OPEN");
            Check(WindowChecks.EditCancelled(window, pathColumn, server), "nor can its path be typed into");
        }

        /// <summary>Saved In: read for Revit links whose files were found, once per file, and shown only on the Revit tab.</summary>
        void SavedIn()
        {
            Check(WaitFor(() => window.FileInfoDone), "the found Revit links' details are read");
            Eq("2025 · Central", here.FileInfo?.Text, "a found Revit link shows its version");
            Check(gone.FileInfo == null && cad.FileInfo == null, "a missing file, or a CAD link, has none");
            Eq(1, infoReads.Count, "only the found Revit file was read");
            Check(FindChild<TextBlock>(window.LinksGrid, t => t.DataContext == here && t.Text == "2025 · Central") != null, "it's shown in the Saved In column");
            Eq(Visibility.Visible, window.SavedInColumn.Visibility, "Saved In shows on the Revit tab");
            window.KindTabs.SelectedIndex = 1;
            Pump(window);
            Eq(Visibility.Collapsed, window.SavedInColumn.Visibility, "and not on the CAD tab");
            window.KindTabs.SelectedIndex = 0;
            Pump(window);

            // Row colours: red for a file that isn't there, which wins over a changed link's amber
            Check(Brush("ErrorSoftBrush") != Brush("AccentSoftBrush"), "the red row isn't the amber one");
            Eq(Brush("ErrorSoftBrush"), RowColour(gone), "a link whose file isn't found gets a red row");
            Eq(Brush("SurfaceBrush"), RowColour(here), "one that's found and unchanged stays white");
        }

        void FindAndReplace()
        {
            Eq(1, window.ReplaceInPaths(new List<LinkRow> { here, gone, server }, "gone", "Here", false), "Find & Replace changes only the paths with the text");
            Eq(@"Links\Here.rvt", gone.DesiredPath, "the replaced path");
            Check(WaitFor(() => window.PendingFileChecks == 0) && gone.FileStatus == LinkFileStatus.Found, "a replaced path is looked for again");
            Eq("5 link(s) in 2 project(s) · 1 not found", window.LinksSummary.Text, "and the summary follows");
            Check(WaitFor(() => window.FileInfoDone), "the replaced link's details are found");
            Eq("2025 · Central", gone.FileInfo?.Text, "a link pointed at a file already read gets its details");
            Eq(1, infoReads.Count, "without reading the file again");
            Eq("1 LINK CHANGED", window.ChangesBadgeText.Text, "the replacement counts as a change");
            Pump(window);
            Eq(Brush("AccentSoftBrush"), RowColour(gone), "found and changed, its row turns amber");
        }

        void MakeChangesPreview()
        {
            const string applies = "\n\nRevit applies the links the next time each project is opened.";
            Eq("Write 1 link to 1 project?\n\nHost.rvt: 1 changed link\n\n1 project with no changes is left as it is." + applies,
                window.WritePreview(false, out bool anyMissing), "Make Changes says what it will write first");
            Check(!anyMissing, "with nothing to warn about");

            second.DesiredLoaded = false;
            Pump(window);
            Eq(Brush("ErrorSoftBrush"), RowColour(second), "a changed link whose file isn't found stays red");
            const string oneMissing = "\n\n1 of these links points at a file that wasn't found from this computer (see the Found column).";
            Eq("Write 2 links to 2 projects?\n\nHost.rvt: 1 changed link\nSecond.rvt: 1 changed link" + oneMissing + applies,
                window.WritePreview(false, out anyMissing), "and warns of a link whose file wasn't found");
            Check(anyMissing, "flagging it, so the question defaults to No");
            Eq("Write 4 links to 2 projects?\n\nHost.rvt: 3 links, 1 changed\nSecond.rvt: 1 link, 1 changed" + oneMissing + applies,
                window.WritePreview(true, out anyMissing), "writing all counts every Revit, CAD and DWF link that can be changed");

            // A long list of projects is cut short, so the question stays on screen
            var many = new LinksManagerWindow();
            for (int i = 1; i <= 14; i++)
            {
                string path = $@"C:\Projects\Many\Project {i:00}.rvt";
                LinkRow row = Samples.Link(path, "1", LinkKind.Revit, @"Links\A.rvt", "Relative");
                many.AddProject(path, new List<LinkRow> { row }, null);
                row.DesiredLoaded = false;
            }
            string manyText = many.WritePreview(false, out _);
            Check(manyText.StartsWith("Write 14 links to 14 projects?") && manyText.Contains("Project 12.rvt: 1 changed link\n…and 2 more projects\n\n")
                && !manyText.Contains("Project 13.rvt"), "a long list of projects is cut short");
        }

        void FindReplaceDialog()
        {
            List<LinkRow> onTab = window.RowsOnTab();
            var every = new List<LinkRow> { here, gone, cad, second };
            var dialog = new FindReplaceWindow(new List<LinkRow>(), onTab, every, "Revit");
            ShowOffscreen(dialog);
            Check(dialog.TryFindResource("PrimaryDarkBrush") is SolidColorBrush, "the dialog has the theme");
            Check(!dialog.LookInSelected.IsEnabled && dialog.LookInTab.IsChecked == true, "with nothing selected it looks in the tab");
            Eq("Every link on the Revit tab (3)", dialog.LookInTab.Content, "the tab's links, counted, leaving out the server model");
            Eq("Every Revit, CAD and DWF link (4)", dialog.LookInAll.Content, "every link, counted");
            Eq("Type the text to find in the links' paths.", dialog.Preview.Text, "it asks for the text first");
            Check(!dialog.ReplaceButton.IsEnabled, "and can't replace nothing");
            dialog.FindBox.Text = @"links\";
            Eq("Found in 3 of the 3 paths.", dialog.Preview.Text, "it counts the paths with the text, ignoring case");
            Check(dialog.ReplaceButton.IsEnabled, "and allows Replace");
            dialog.MatchCase.IsChecked = true;
            Eq("None of these paths contain it.", dialog.Preview.Text, "Match case narrows it");
            Check(!dialog.ReplaceButton.IsEnabled, "to none, so there's no Replace");
            dialog.MatchCase.IsChecked = false;
            dialog.LookInAll.IsChecked = true;
            Eq("Found in 3 of the 4 paths.", dialog.Preview.Text, "looking in every link");
            Eq(4, dialog.ChosenRows.Count, "which it hands back");
            Render(dialog, Path.Combine(outDir, "find-replace-test.png"));
            dialog.Close();

            var oneSelected = new FindReplaceWindow(new List<LinkRow> { cad }, onTab, every, "Revit");
            Check(oneSelected.LookInSelected.IsChecked == true, "with links selected it looks in those");
            Eq("The selected links (1)", oneSelected.LookInSelected.Content, "counted");
            oneSelected.FindBox.Text = "shared";
            Eq("Found in the link's path.", oneSelected.Preview.Text, "one link, found");
        }

        /// <summary>Export, edit the workbook as someone in Excel might, and import it.</summary>
        void ExcelRoundTrip()
        {
            foreach (LinkRow l in window.Links)
            {
                l.ResetPath();
                l.ResetLoaded();
            }
            Check(WaitFor(() => window.PendingFileChecks == 0 && window.FileInfoDone), "the reset links are looked for again");
            string book = Path.Combine(outDir, "round-trip.xlsx");
            if (File.Exists(book)) File.Delete(book);
            LinkWorkbook.Save(book, window.Links);

            List<LinkWorkbook.WorkbookLink> unedited = LinkWorkbook.Read(book);
            Eq(5, unedited.Count, "every link is read back");
            Eq("5 links read from round-trip.xlsx.\n\nNone of them differ from the list. If you've edited the workbook in Excel, save it there first.",
                window.ApplyWorkbook(unedited, book), "an unedited workbook changes nothing");
            Check(!window.Links.Any(l => l.IsDirty), "so no link is changed");

            using (var package = new ExcelPackage(new FileInfo(book)))
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets["Links"];
                Check(((string)sheet.Cells["A1"].Value).Contains("IMPORT FROM EXCEL"), "the export says how to bring edits back");
                // A column added in front: the import finds columns by their headings
                sheet.InsertColumn(1, 1);
                sheet.Cells[2, 1].Value = "Notes";
                var col = new Dictionary<string, int>();
                for (int c = 1; c <= sheet.Dimension.End.Column; c++)
                    if (sheet.Cells[2, c].Value is string heading) col[heading] = c;
                int RowOf(string id) => Enumerable.Range(3, sheet.Dimension.End.Row - 2).First(r => (sheet.Cells[r, col["Link Id"]].Value as string) == id);

                sheet.Cells[RowOf("22"), col["Path on Next Open"]].Value = @"Links\Here.rvt";
                sheet.Cells[RowOf("21"), col["Link State"]].Value = "Unloaded";
                sheet.Cells[RowOf("21"), col["Link Id"]].Value = 21d;
                sheet.Cells[RowOf("24"), col["Path Type"]].Value = "relative";
                sheet.Cells[RowOf("23"), col["Path on Next Open"]].Value = "RSN://other/Central.rvt";
                int secondRow = RowOf("31");
                sheet.Cells[secondRow, col["Path on Next Open"]].Value = @"D:\Elsewhere\X.rvt";
                sheet.Cells[secondRow, col["Master File Path"]].Value = null;
                int extra = sheet.Dimension.End.Row + 1;
                sheet.Cells[extra, col["Link Id"]].Value = "99999";
                sheet.Cells[extra, col["Link Type"]].Value = "Rvt";
                sheet.Cells[extra, col["Master File Path"]].Value = files.Project;
                sheet.Cells[extra, col["Path on Next Open"]].Value = @"Links\Nowhere.rvt";
                sheet.Cells[extra + 1, col["Link Id"]].Value = "1";
                sheet.Cells[extra + 1, col["Project Name"]].Value = "Other.rvt";
                sheet.Cells[extra + 1, col["Master File Path"]].Value = @"C:\Projects\Elsewhere\Other.rvt";
                sheet.Cells[extra + 1, col["Path on Next Open"]].Value = @"Links\A.rvt";
                package.Save();
            }

            List<LinkWorkbook.WorkbookLink> edited;
            // Excel keeps the workbook open while it's being edited; it can still be read
            using (new FileStream(book, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) edited = LinkWorkbook.Read(book);
            Eq(7, edited.Count, "the edited workbook is read, even while it's open elsewhere");
            Eq("7 links read from round-trip.xlsx."
                + "\n\n4 links changed: 2 paths, 1 path type and 1 load state. They're highlighted, ready for MAKE CHANGES, and RESET CHANGES undoes them."
                + "\n\nLinks of 1 project that aren't in the list were left out: Other.rvt. Add them, then import again."
                + "\n\n1 row didn't match a link of the projects in the list."
                + "\n\n1 change to links that can't be changed here (Other, Revit Server, cloud and IFC links) was left out.",
                window.ApplyWorkbook(edited, book), "the import says what it changed and what it left out");
            Eq(@"Links\Here.rvt", gone.DesiredPath, "an edited path is set");
            Check(!here.DesiredLoaded, "an edited load state is set, with its id turned into a number by Excel");
            Check(cad.DesiredPath == @"..\Shared.dwg" && cad.DesiredPathType == "Relative", "an edited path type converts the path");
            Check(second.DesiredPath == @"D:\Elsewhere\X.rvt" && second.DesiredPathType == "Absolute",
                "a full path in a Relative row makes it Absolute, matched by project name without the hidden path");
            Eq("RSN://server/Projects/Central.rvt", server.DesiredPath, "a read-only link is left alone");
            Eq("4 LINKS CHANGED", window.ChangesBadgeText.Text, "the imported changes are counted");

            string notBook = Path.Combine(outDir, "not-links.xlsx");
            if (File.Exists(notBook)) File.Delete(notBook);
            using (var package = new ExcelPackage(new FileInfo(notBook)))
            {
                package.Workbook.Worksheets.Add("Budget").Cells["A1"].Value = "Item";
                package.Save();
            }
            try
            {
                LinkWorkbook.Read(notBook);
                Check(false, "a workbook that isn't a links export is refused");
            }
            catch (InvalidDataException ex)
            {
                Check(ex.Message.Contains("isn't a links workbook"), $"a workbook that isn't a links export is refused ({ex.Message})");
            }
        }
    }
}
