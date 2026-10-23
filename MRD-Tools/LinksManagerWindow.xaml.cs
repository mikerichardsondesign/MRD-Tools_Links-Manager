using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace MRDTools
{
    /// <summary>
    /// The links manager: one window for one project or many. Projects are read as they're added, their links are
    /// listed by kind and edited here, and Make Changes writes them back to the projects ticked to Process.
    /// </summary>
    public partial class LinksManagerWindow : Window
    {
        #region Fields

        readonly ObservableCollection<ProjectRow> projects = new ObservableCollection<ProjectRow>();
        readonly ObservableCollection<LinkRow> links = new ObservableCollection<LinkRow>();
        readonly Dictionary<string, ProjectRow> projectsByPath = new Dictionary<string, ProjectRow>(StringComparer.OrdinalIgnoreCase);
        readonly ListCollectionView linksView;

        // Where each kind of dialog last looked, kept between sessions
        readonly FolderMemory folders = FolderMemory.Load();

        // Looks for each link's file in the background, for the Found column
        readonly FileChecker fileChecker;

        // Reads a found Revit link's details for the Saved In column. A field, so the test harness can stand in for Revit.
        internal Func<string, LinkFileInfo> readFileInfo = RevitFileInfo.Read;

        // Revit links whose files were found, waiting for their details. Revit's API only works on this thread, so they're
        // read one at a time while the window is idle, each file once, and not at all once the window has closed.
        readonly Queue<LinkRow> infoQueue = new Queue<LinkRow>();
        readonly Dictionary<string, LinkFileInfo> infoByFile = new Dictionary<string, LinkFileInfo>(StringComparer.OrdinalIgnoreCase);
        bool infoScheduled;
        bool closed;

        // Set while many links are edited at once, so the counts are worked out once at the end
        bool bulkEdit;

        // The path being typed into a cell. It's written to the link only when the edit is committed, so Esc cancels it.
        TextBox pathEditor;

        #endregion

        #region Constructor

        public LinksManagerWindow()
        {
            InitializeComponent();
            Icon = WindowIcon.Load();

            // Open as large as the screen allows, up to a size where the paths have room, and no smaller than the minimum
            Rect work = SystemParameters.WorkArea;
            Width = Math.Max(MinWidth, Math.Min(1480, work.Width - 40));
            Height = Math.Max(MinHeight, Math.Min(880, work.Height - 40));
            VersionText.Text = typeof(LinksManagerWindow).Assembly.GetName().Version.ToString(3);
            WriteAllLinks.ToolTip = LinkConstants.TipWriteAllLinks;
            fileChecker = new FileChecker(Dispatcher);
            Closed += (sender, e) => closed = true;

            ProjectsGrid.ItemsSource = projects;
            linksView = new ListCollectionView(links) { Filter = IsShown };
            LinksGrid.ItemsSource = linksView;
            KindTabs.SelectedIndex = 0;

            RefreshAll();
        }

        #endregion

        #region State

        LinkKind SelectedKind => (KindTabs.SelectedItem as TabItem)?.Tag is LinkKind kind ? kind : LinkKind.Revit;

        bool IsProcessed(LinkRow row) => projectsByPath.TryGetValue(row.ProjectPath, out ProjectRow project) && project.Process;

        bool IsShown(object item) => item is LinkRow row && row.Kind == SelectedKind && IsProcessed(row);

        /// <summary>The links of the projects ticked to Process, in the order they were read.</summary>
        List<LinkRow> ProcessedLinks() => links.Where(IsProcessed).ToList();

        /// <summary>The editable links on the current tab, in the order the grid shows them.</summary>
        internal List<LinkRow> RowsOnTab() => linksView.Cast<LinkRow>().Where(r => r.IsEditable).ToList();

        internal List<LinkRow> SelectedRows() => LinksGrid.SelectedItems.OfType<LinkRow>().Where(r => r.IsEditable).ToList();

        /// <summary>Every link of every project in the list, in the order they were read.</summary>
        internal IReadOnlyList<LinkRow> Links => links;

        /// <summary>How many links' files are still being looked for.</summary>
        internal int PendingFileChecks => fileChecker.Pending;

        /// <summary>Re-filters the links grid, then brings every count, header and button up to date.</summary>
        internal void RefreshAll()
        {
            if (linksView == null) return;
            // Refresh isn't allowed while a cell is being edited
            LinksGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (linksView.IsEditingItem) linksView.CommitEdit();
            linksView.Refresh();
            RefreshCounts();
        }

        void RefreshCounts()
        {
            if (linksView == null) return;

            Dictionary<string, List<LinkRow>> byProject = links
                .GroupBy(l => l.ProjectPath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (ProjectRow project in projects)
            {
                List<LinkRow> own = byProject.TryGetValue(project.Path, out List<LinkRow> found) ? found : new List<LinkRow>();
                project.LinkCount = own.Count;
                project.ChangeCount = own.Count(l => l.IsDirty);
            }

            List<LinkRow> processed = ProcessedLinks();
            int Count(LinkKind kind) => processed.Count(l => l.Kind == kind);
            TabRevit.Header = $"REVIT ({Count(LinkKind.Revit)})";
            TabCad.Header = $"CAD ({Count(LinkKind.Cad)})";
            TabDwf.Header = $"DWF ({Count(LinkKind.Dwf)})";
            TabOther.Header = $"OTHER ({Count(LinkKind.Other)})";

            // One project is the single-project tool: no need for a Project column
            int projectsWithLinks = processed.Select(l => l.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            ProjectColumn.Visibility = projectsWithLinks > 1 ? Visibility.Visible : Visibility.Collapsed;
            // Only Revit links have a Revit version
            SavedInColumn.Visibility = SelectedKind == LinkKind.Revit ? Visibility.Visible : Visibility.Collapsed;

            ProjectCount.Text = projects.Count == 0 ? "" : projects.Count == 1 ? "1 project" : $"{projects.Count} projects";
            ProjectsHint.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshLinksSummary(processed, projectsWithLinks);

            int changed = processed.Count(l => l.IsDirty);
            ChangesBadgeText.Text = changed == 1 ? "1 LINK CHANGED" : $"{changed} LINKS CHANGED";
            ChangesBadge.Visibility = changed > 0 ? Visibility.Visible : Visibility.Collapsed;

            MakeChangesButton.IsEnabled = projects.Any(p => p.Process);
            TextReportButton.IsEnabled = ExcelButton.IsEnabled = processed.Count > 0;
            ImportButton.IsEnabled = links.Count > 0;
            LinkTools.IsEnabled = SelectedKind != LinkKind.Other && linksView.Count > 0;
        }

        /// <summary>How many links are listed, and how many of their files weren't found.</summary>
        void RefreshLinksSummary(List<LinkRow> processed, int projectsWithLinks)
        {
            int missing = processed.Count(l => l.FileStatus == LinkFileStatus.Missing);
            LinksSummary.Text = processed.Count == 0 ? ""
                : $"{processed.Count} link(s) in {projectsWithLinks} project(s)" + (missing > 0 ? $" · {missing} not found" : "");
        }

        void Link_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // A new path is looked for at once, even in a bulk edit; the counts wait for the edit to finish
            if (e.PropertyName == nameof(LinkRow.DesiredPath))
            {
                fileChecker.Check((LinkRow)sender);
            }
            else if (e.PropertyName == nameof(LinkRow.FileStatus))
            {
                var row = (LinkRow)sender;
                if (row.Kind == LinkKind.Revit && row.FoundFile != null) QueueFileInfo(row);
                if (bulkEdit) return;
                List<LinkRow> processed = ProcessedLinks();
                RefreshLinksSummary(processed, processed.Select(l => l.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            }
            else if (!bulkEdit && e.PropertyName == nameof(LinkRow.IsDirty))
            {
                RefreshCounts();
            }
        }

        void QueueFileInfo(LinkRow row)
        {
            infoQueue.Enqueue(row);
            ScheduleFileInfo();
        }

        void ScheduleFileInfo()
        {
            if (infoScheduled || closed || infoQueue.Count == 0) return;
            infoScheduled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ReadNextFileInfo));
        }

        /// <summary>Hands out details already read, then reads one more file, leaving the window free in between.</summary>
        void ReadNextFileInfo()
        {
            infoScheduled = false;
            while (infoQueue.Count > 0 && !closed)
            {
                LinkRow row = infoQueue.Dequeue();
                string file = row.FoundFile;
                if (file == null) continue;
                if (infoByFile.TryGetValue(file, out LinkFileInfo known))
                {
                    row.SetFileInfo(file, known);
                    continue;
                }

                LinkFileInfo info;
                try { info = readFileInfo(file); }
                catch (Exception ex) { info = LinkFileInfo.Unreadable(ex.Message); }
                infoByFile[file] = info;
                row.SetFileInfo(file, info);
                break;
            }
            ScheduleFileInfo();
        }

        /// <summary>Whether every found Revit link's details have been read.</summary>
        internal bool FileInfoDone => infoQueue.Count == 0 && !infoScheduled;

        void Project_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectRow.Process)) RefreshAll();
        }

        #endregion

        #region Projects

        /// <summary>Adds a project with the links read from it (null when it has none), or with the reason it couldn't be read.</summary>
        internal void AddProject(string path, List<LinkRow> rows, string error = null)
        {
            var project = new ProjectRow(path);
            if (error != null)
            {
                project.Process = false;
                project.SetStatus("Couldn't be read: " + error, isError: true);
            }
            else
            {
                project.SetStatus(rows == null || rows.Count == 0 ? "No links" : "Links read");
            }

            project.PropertyChanged += Project_PropertyChanged;
            projects.Add(project);
            projectsByPath[path] = project;
            foreach (LinkRow row in rows ?? new List<LinkRow>())
            {
                row.PropertyChanged += Link_PropertyChanged;
                links.Add(row);
                fileChecker.Check(row);
            }
        }

        /// <summary>Reads each project's links and adds it, skipping any already in the list.</summary>
        void ReadProjects(IEnumerable<string> paths)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                foreach (string path in paths)
                {
                    if (projectsByPath.ContainsKey(path)) continue;
                    List<LinkRow> rows = null;
                    string error = null;
                    try { rows = LinkScanner.Scan(path); }
                    catch (Exception ex) { error = ex.Message; }
                    AddProject(path, rows, error);
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
            RefreshAll();
        }

        internal void RemoveProjects(IEnumerable<ProjectRow> toRemove)
        {
            foreach (ProjectRow project in toRemove.ToList())
            {
                project.PropertyChanged -= Project_PropertyChanged;
                projects.Remove(project);
                projectsByPath.Remove(project.Path);
                foreach (LinkRow row in links.Where(l => string.Equals(l.ProjectPath, project.Path, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    row.PropertyChanged -= Link_PropertyChanged;
                    links.Remove(row);
                }
            }
            RefreshAll();
        }

        /// <summary>The Revit projects in a folder, leaving out backups such as name.0001.rvt.</summary>
        static List<string> ProjectsInFolder(string folder, bool includeSubfolders)
        {
            SearchOption option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.GetFiles(folder, "*.rvt", option)
                .Where(GetExtension.RvtExtension)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = LinkConstants.FilterRvt, Multiselect = true, Title = "Add Revit projects" };
            string start = FolderMemory.FirstExisting(folders.ProjectsFolder);
            if (start != null) dlg.InitialDirectory = start;
            if (dlg.ShowDialog(this) != true) return;
            RememberProjectsFolder(Path.GetDirectoryName(dlg.FileName));
            ReadProjects(dlg.FileNames);
        }

        void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            string folder = FolderPicker.Pick(this, "Select a folder of Revit projects", FolderMemory.FirstExisting(folders.ProjectsFolder));
            if (folder == null) return;
            RememberProjectsFolder(folder);

            List<string> found;
            try
            {
                found = ProjectsInFolder(folder, IncludeSubfolders.IsChecked == true);
            }
            catch (Exception ex)
            {
                Show("The folder couldn't be searched:\n" + ex.Message, "Add Folder");
                return;
            }
            if (found.Count == 0)
            {
                Show("No Revit projects were found in " + folder, "Add Folder");
                return;
            }
            ReadProjects(found);
        }

        void Remove_Click(object sender, RoutedEventArgs e)
        {
            List<ProjectRow> selected = ProjectsGrid.SelectedItems.OfType<ProjectRow>().ToList();
            if (selected.Count == 0)
            {
                Show(LinkConstants.MsgSelectProjects, "Remove");
                return;
            }
            int changed = selected.Sum(p => p.ChangeCount);
            if (changed > 0 && MessageBox.Show(this, $"{changed} changed link(s) in these projects haven't been written. Remove them anyway?",
                    "Remove", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            RemoveProjects(selected);
        }

        void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        void Window_Drop(object sender, DragEventArgs e)
        {
            if (!(e.Data.GetData(DataFormats.FileDrop) is string[] dropped)) return;

            var paths = new List<string>();
            try
            {
                foreach (string item in dropped)
                {
                    if (Directory.Exists(item)) paths.AddRange(ProjectsInFolder(item, IncludeSubfolders.IsChecked == true));
                    else if (File.Exists(item) && GetExtension.RvtExtension(item)) paths.Add(item);
                }
            }
            catch (Exception ex)
            {
                Show("The dropped folder couldn't be searched:\n" + ex.Message, "Add Projects");
                return;
            }
            if (paths.Count == 0) return;
            RememberProjectsFolder(Directory.Exists(dropped[0]) ? dropped[0] : Path.GetDirectoryName(dropped[0]));
            ReadProjects(paths);
        }

        void RememberProjectsFolder(string folder)
        {
            folders.ProjectsFolder = folder;
            folders.Save();
        }

        #endregion

        #region Links

        void KindTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == KindTabs) RefreshAll();
        }

        /// <summary>Applies one edit to many links, then updates the counts once.</summary>
        internal void EditRows(IEnumerable<LinkRow> rows, Action<LinkRow> edit)
        {
            bulkEdit = true;
            try
            {
                foreach (LinkRow row in rows) edit(row);
            }
            finally
            {
                bulkEdit = false;
            }
            RefreshCounts();
        }

        List<LinkRow> SelectedRowsOrWarn()
        {
            List<LinkRow> rows = SelectedRows();
            if (rows.Count == 0) Show(LinkConstants.MsgSelectRows, "Select Links");
            return rows;
        }

        void RepathSelected_Click(object sender, RoutedEventArgs e)
        {
            List<LinkRow> rows = SelectedRowsOrWarn();
            if (rows.Count == 0) return;
            string folder = PickRepathFolder("Select the folder to repath the selected links to", rows[0]);
            if (folder != null) EditRows(rows, r => r.RepathToFolder(folder));
        }

        void RepathAll_Click(object sender, RoutedEventArgs e)
        {
            List<LinkRow> rows = RowsOnTab();
            if (rows.Count == 0) return;
            string folder = PickRepathFolder("Select the folder to repath every link on this tab to", rows[0]);
            if (folder != null) EditRows(rows, r => r.RepathToFolder(folder));
        }

        void FindReplace_Click(object sender, RoutedEventArgs e)
        {
            LinksGrid.CommitEdit(DataGridEditingUnit.Row, true);
            List<LinkRow> every = ProcessedLinks().Where(r => r.IsEditable).ToList();
            if (every.Count == 0) return;

            var dialog = new FindReplaceWindow(SelectedRows(), RowsOnTab(), every, KindName(SelectedKind)) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            ReplaceInPaths(dialog.ChosenRows, dialog.FindText, dialog.ReplacementText, dialog.IsMatchCase);
        }

        /// <summary>Replaces text in the links' paths on next open, and returns how many paths changed.</summary>
        internal int ReplaceInPaths(IEnumerable<LinkRow> rows, string find, string replacement, bool matchCase)
        {
            int changed = 0;
            EditRows(rows, r => { if (r.ReplaceInPath(find, replacement, matchCase)) changed++; });
            return changed;
        }

        static string KindName(LinkKind kind) => kind == LinkKind.Cad ? "CAD" : kind == LinkKind.Dwf ? "DWF" : kind.ToString();

        void Reset_Click(object sender, RoutedEventArgs e)
        {
            List<LinkRow> rows = SelectedRows();
            if (rows.Count == 0) rows = RowsOnTab();
            EditRows(rows, r => { r.ResetPath(); r.ResetLoaded(); });
        }

        void LoadSelected_Click(object sender, RoutedEventArgs e) => EditRows(SelectedRowsOrWarn(), r => r.DesiredLoaded = true);
        void UnloadSelected_Click(object sender, RoutedEventArgs e) => EditRows(SelectedRowsOrWarn(), r => r.DesiredLoaded = false);
        void LoadAll_Click(object sender, RoutedEventArgs e) => EditRows(RowsOnTab(), r => r.DesiredLoaded = true);
        void UnloadAll_Click(object sender, RoutedEventArgs e) => EditRows(RowsOnTab(), r => r.DesiredLoaded = false);

        void BrowseLink_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.DataContext is LinkRow row) || !row.IsEditable) return;

            string filter = row.Kind == LinkKind.Revit ? LinkConstants.FilterRvt
                : row.Kind == LinkKind.Cad ? LinkConstants.FilterCad
                : LinkConstants.FilterDwf;
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter + "|All Files (*.*)|*.*", Title = "Repath " + row.DesiredName };

            // Start in the folder the link points at now, then where links were last repathed to
            string start = FolderMemory.FirstExisting(LinkFolder(row), folders.RepathFolder);
            if (start != null) dlg.InitialDirectory = start;

            if (dlg.ShowDialog(this) != true) return;
            RememberRepathFolder(Path.GetDirectoryName(dlg.FileName));
            row.Repath(dlg.FileName);
        }

        /// <summary>The folder a link points at now, or null if it can't be worked out.</summary>
        static string LinkFolder(LinkRow row)
        {
            try { return Path.GetDirectoryName(GetAbsolutePath.LinksAbsolutePath(row.DesiredPath, row.ProjectPath)); }
            catch (Exception) { return null; }
        }

        /// <summary>Picks a folder to repath to, starting where the first link points now, then where links were last repathed to.</summary>
        string PickRepathFolder(string title, LinkRow first)
        {
            string folder = FolderPicker.Pick(this, title, FolderMemory.FirstExisting(LinkFolder(first), folders.RepathFolder));
            if (folder != null) RememberRepathFolder(folder);
            return folder;
        }

        void RememberRepathFolder(string folder)
        {
            folders.RepathFolder = folder;
            folders.Save();
        }

        void PathType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Choosing a type converts the link's path. A file on another drive or share has no relative path, so it stays Absolute.
            if (!(sender is ComboBox box) || !(box.DataContext is LinkRow row) || e.AddedItems.Count == 0) return;
            if (e.AddedItems[0] as string != LinkConstants.PathTypeRelative || row.DesiredPathType == LinkConstants.PathTypeRelative) return;
            box.SelectedItem = row.DesiredPathType;
            Show($"{row.DesiredName} can't have a relative path: it's on a different drive or network share from the project.", "Path Type");
        }

        void LinksGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (!(e.Row.Item is LinkRow row) || !row.IsEditable) e.Cancel = true;
        }

        void PathEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox box)) return;
            pathEditor = box;
            box.Focus();
            box.SelectAll();
        }

        void LinksGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (pathEditor != null && e.EditAction == DataGridEditAction.Commit)
                pathEditor.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            pathEditor = null;
        }

        #endregion

        #region Reports

        string WriteReport()
        {
            List<LinkRow> processed = ProcessedLinks();
            bool manyProjects = processed.Select(l => l.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
            // Read-only links are listed as they'll be next time too, which is as they are now
            return LinkReport.WriteTempFile(processed, processed.Where(l => l.Kind != LinkKind.Other), includeProjectName: manyProjects);
        }

        void TextReport_Click(object sender, RoutedEventArgs e)
        {
            LinksGrid.CommitEdit(DataGridEditingUnit.Row, true);
            try
            {
                ShellOpen.Open(WriteReport());
            }
            catch (Exception ex)
            {
                Show("The text file could not be written:\n" + ex.Message, "Text Report");
            }
        }

        /// <summary>
        /// The Excel export's suggested name: the UK date and time, then the project's name, or "multiple projects",
        /// such as "23.10.2026 11-05_links_Riverside - Central".
        /// </summary>
        internal string DefaultWorkbookName()
        {
            List<string> exported = ProcessedLinks().Select(l => l.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            string projects = exported.Count == 1 ? Path.GetFileNameWithoutExtension(exported[0]) : "multiple projects";
            return DateTime.Now.ToString("dd.MM.yyyy HH-mm", CultureInfo.InvariantCulture) + "_links_" + projects;
        }

        void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            LinksGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = DefaultWorkbookName() + ".xlsx",
                OverwritePrompt = false,
                Title = "Export all paths to Excel"
            };
            string firstProject = ProcessedLinks().Select(l => l.ProjectPath).FirstOrDefault();
            string start = FolderMemory.FirstExisting(folders.ExportFolder, firstProject == null ? null : Path.GetDirectoryName(firstProject));
            if (start != null) dlg.InitialDirectory = start;
            if (dlg.ShowDialog(this) != true) return;
            folders.ExportFolder = Path.GetDirectoryName(dlg.FileName);
            folders.Save();
            if (File.Exists(dlg.FileName))
            {
                Show("Choose a new file name: an existing workbook isn't overwritten.", "Export to Excel");
                return;
            }

            try
            {
                LinkWorkbook.Save(dlg.FileName, ProcessedLinks());
            }
            catch (Exception ex)
            {
                Show("The workbook couldn't be saved:\n" + ex.Message, "Export to Excel");
                return;
            }
            ShellOpen.Open(dlg.FileName);
        }

        void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            LinksGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Import links from an exported workbook"
            };
            string firstProject = projects.Select(p => p.Path).FirstOrDefault();
            string start = FolderMemory.FirstExisting(folders.ExportFolder, firstProject == null ? null : Path.GetDirectoryName(firstProject));
            if (start != null) dlg.InitialDirectory = start;
            if (dlg.ShowDialog(this) != true) return;
            folders.ExportFolder = Path.GetDirectoryName(dlg.FileName);
            folders.Save();

            List<LinkWorkbook.WorkbookLink> rows;
            try
            {
                rows = LinkWorkbook.Read(dlg.FileName);
            }
            catch (Exception ex)
            {
                Show("The workbook couldn't be read:\n" + ex.Message, "Import from Excel");
                return;
            }
            Show(ApplyWorkbook(rows, dlg.FileName), "Import from Excel");
        }

        /// <summary>
        /// Sets links' paths, path types and load states as a workbook has them, as changes waiting for Make Changes, and
        /// returns a summary. Rows are matched to links by project and Link Id: the project is the one in the hidden Master
        /// File Path column, or failing that, the only project in the list with its Project Name.
        /// </summary>
        internal string ApplyWorkbook(List<LinkWorkbook.WorkbookLink> rows, string fileName)
        {
            var changed = new HashSet<LinkRow>();
            var notListed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            int paths = 0, types = 0, states = 0, unmatched = 0, readOnly = 0, noRelative = 0;

            bulkEdit = true;
            try
            {
                foreach (LinkWorkbook.WorkbookLink row in rows)
                {
                    ProjectRow project = WorkbookProject(row);
                    if (project == null)
                    {
                        string name = row.ProjectName.Length > 0 ? row.ProjectName : LinkRow.FileNameOf(row.ProjectPath);
                        if (name.Length > 0 && !projects.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))) notListed.Add(name);
                        else unmatched++;
                        continue;
                    }
                    LinkRow link = links.FirstOrDefault(l => string.Equals(l.ProjectPath, project.Path, StringComparison.OrdinalIgnoreCase)
                        && l.RefId == row.RefId
                        && (row.LinkType.Length == 0 || string.Equals(l.LinkTypeName, row.LinkType, StringComparison.OrdinalIgnoreCase)));
                    if (link == null)
                    {
                        unmatched++;
                        continue;
                    }

                    bool? loaded = LoadedFromState(row.State);
                    string type = string.Equals(row.PathType, LinkConstants.PathTypeRelative, StringComparison.OrdinalIgnoreCase) ? LinkConstants.PathTypeRelative
                        : string.Equals(row.PathType, LinkConstants.PathTypeAbsolute, StringComparison.OrdinalIgnoreCase) ? LinkConstants.PathTypeAbsolute
                        : null;
                    bool pathDiffers = row.DesiredPath.Length > 0 && row.DesiredPath != link.DesiredPath;
                    bool typeDiffers = type != null && type != link.DesiredPathType;
                    bool loadedDiffers = loaded.HasValue && loaded.Value != link.DesiredLoaded;
                    if (!pathDiffers && !typeDiffers && !loadedDiffers) continue;
                    if (!link.IsEditable)
                    {
                        readOnly++;
                        continue;
                    }

                    string typeBefore = link.DesiredPathType;
                    if (pathDiffers)
                    {
                        link.DesiredPath = row.DesiredPath;
                        paths++;
                        changed.Add(link);
                    }
                    // A type changed in the workbook is set after the path. One left as it was follows the path, as it does
                    // when a path is typed, so a full path typed into a Relative link's row makes it Absolute.
                    if (type != null && type != typeBefore && type != link.DesiredPathType && link.CanChangePathType)
                    {
                        link.DesiredPathType = type;
                        if (link.DesiredPathType == type)
                        {
                            types++;
                            changed.Add(link);
                        }
                        else
                        {
                            noRelative++;
                        }
                    }
                    if (loadedDiffers)
                    {
                        link.DesiredLoaded = loaded.Value;
                        states++;
                        changed.Add(link);
                    }
                }
            }
            finally
            {
                bulkEdit = false;
            }
            RefreshCounts();

            var lines = new List<string> { $"{Plural(rows.Count, "link")} read from {Path.GetFileName(fileName)}." };
            if (changed.Count == 0)
            {
                lines.Add("None of them differ from the list. If you've edited the workbook in Excel, save it there first.");
            }
            else
            {
                var what = new List<string>();
                if (paths > 0) what.Add(Plural(paths, "path"));
                if (types > 0) what.Add(Plural(types, "path type"));
                if (states > 0) what.Add(Plural(states, "load state"));
                lines.Add($"{Plural(changed.Count, "link")} changed: {JoinWithAnd(what)}. They're highlighted, ready for MAKE CHANGES, and RESET CHANGES undoes them.");
            }
            if (notListed.Count > 0)
                lines.Add($"Links of {Plural(notListed.Count, "project")} that aren't in the list were left out: "
                    + string.Join(", ", notListed.Take(5)) + (notListed.Count > 5 ? ", …" : "") + ". Add them, then import again.");
            if (unmatched > 0)
                lines.Add($"{Plural(unmatched, "row")} didn't match a link of the projects in the list.");
            if (readOnly > 0)
                lines.Add($"{Plural(readOnly, "change")} to links that can't be changed here (Other, Revit Server, cloud and IFC links) "
                    + (readOnly == 1 ? "was" : "were") + " left out.");
            if (noRelative > 0)
                lines.Add($"{Plural(noRelative, "link")} couldn't be made relative: " + (noRelative == 1 ? "it's" : "they're")
                    + " on a different drive or network share from the project.");
            return string.Join("\n\n", lines);
        }

        /// <summary>The project a workbook row belongs to: by its full path, or else by a name only one listed project has.</summary>
        ProjectRow WorkbookProject(LinkWorkbook.WorkbookLink row)
        {
            if (row.ProjectPath.Length > 0 && projectsByPath.TryGetValue(row.ProjectPath, out ProjectRow byPath)) return byPath;
            string name = row.ProjectName.Length > 0 ? row.ProjectName : LinkRow.FileNameOf(row.ProjectPath);
            List<ProjectRow> named = projects.Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
            return named.Count == 1 ? named[0] : null;
        }

        /// <summary>Loaded or NotFound (which Revit tries to load) as loaded, Unloaded as not, anything else as no change.</summary>
        static bool? LoadedFromState(string state)
        {
            if (string.Equals(state, LinkConstants.StateUnloaded, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(state, LinkConstants.StateLoaded, StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, LinkConstants.StateNotFound, StringComparison.OrdinalIgnoreCase)) return true;
            return null;
        }

        static string JoinWithAnd(List<string> items) =>
            items.Count <= 1 ? string.Join("", items) : string.Join(", ", items.Take(items.Count - 1)) + " and " + items[items.Count - 1];

        #endregion

        #region Make Changes

        // A long list of projects is cut short, so the question stays on screen
        const int PreviewProjects = 12;

        /// <summary>
        /// What Make Changes is about to write, for the user to confirm: how many links go to each project ticked to Process,
        /// which projects are left alone, and how many of the links point at files that weren't found.
        /// </summary>
        internal string WritePreview(bool writeAll, out bool anyMissing)
        {
            var lines = new List<string>();
            int total = 0;
            int written = 0;
            int untouched = 0;
            int missing = 0;
            int checking = 0;
            foreach (ProjectRow project in projects.Where(p => p.Process))
            {
                List<LinkRow> rows = links.Where(l => l.ShouldWrite(writeAll)
                    && string.Equals(l.ProjectPath, project.Path, StringComparison.OrdinalIgnoreCase)).ToList();
                if (rows.Count == 0)
                {
                    untouched++;
                    continue;
                }
                written++;
                total += rows.Count;
                missing += rows.Count(r => r.FileStatus == LinkFileStatus.Missing);
                checking += rows.Count(r => r.FileStatus == LinkFileStatus.Checking);
                if (lines.Count < PreviewProjects)
                    lines.Add(writeAll ? $"{project.Name}: {Plural(rows.Count, "link")}, {rows.Count(r => r.IsDirty)} changed"
                        : $"{project.Name}: {Plural(rows.Count, "changed link")}");
            }
            if (written > PreviewProjects) lines.Add($"…and {Plural(written - PreviewProjects, "more project")}");

            string text = $"Write {Plural(total, "link")} to {Plural(written, "project")}?\n\n" + string.Join("\n", lines);
            if (untouched > 0)
                text += $"\n\n{Plural(untouched, "project")} with " + (writeAll ? "no Revit, CAD or DWF links" : "no changes")
                    + (untouched == 1 ? " is" : " are") + " left as " + (untouched == 1 ? "it is." : "they are.");
            if (missing > 0)
                text += "\n\n" + (missing == 1 ? "1 of these links points at a file that wasn't found"
                    : $"{missing} of these links point at files that weren't found") + " from this computer (see the Found column).";
            if (checking > 0)
                text += "\n\n" + (checking == 1 ? "1 of these links is" : $"{checking} of these links are") + " still being looked for.";
            text += "\n\nRevit applies the links the next time each project is opened.";

            anyMissing = missing > 0;
            return text;
        }

        static string Plural(int count, string noun) => count == 1 ? "1 " + noun : $"{count} {noun}s";

        void MakeChanges_Click(object sender, RoutedEventArgs e)
        {
            LinksGrid.CommitEdit(DataGridEditingUnit.Row, true);
            bool writeAll = WriteAllLinks.IsChecked == true;

            List<ProjectRow> targets = projects.Where(p => p.Process).ToList();
            if (targets.Count == 0)
            {
                Show(LinkConstants.MsgNoProjects, "Make Changes");
                return;
            }
            // Only changed links are written, unless Write All Links is ticked, so a project with none isn't touched
            if (!ProcessedLinks().Any(r => r.ShouldWrite(writeAll)))
            {
                Show(writeAll ? LinkConstants.MsgNoLinksToWrite : LinkConstants.MsgNoChanges, "No Changes");
                return;
            }

            // Say what's about to be written first, warning of any links whose files weren't found
            string preview = WritePreview(writeAll, out bool anyMissing);
            if (MessageBox.Show(this, preview, "Make Changes", MessageBoxButton.YesNo,
                    anyMissing ? MessageBoxImage.Warning : MessageBoxImage.Question,
                    anyMissing ? MessageBoxResult.No : MessageBoxResult.Yes) != MessageBoxResult.Yes)
                return;

            int amendedCount = 0;
            int unchangedCount = 0;
            var failures = new List<string>();
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                foreach (ProjectRow project in targets)
                {
                    try
                    {
                        int written = LinkWriter.Write(project.Path, links, writeAll);
                        if (written > 0)
                        {
                            amendedCount++;
                            project.SetStatus($"{written} link(s) written at {DateTime.Now:HH:mm}");
                        }
                        else
                        {
                            unchangedCount++;
                            project.SetStatus(writeAll ? "No links to write" : "No changes to write");
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(project.Name + " - " + ex.Message);
                        project.SetStatus("Couldn't be changed: " + ex.Message, isError: true);
                    }
                }
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
            RefreshCounts();

            // One summary for every project, so a failed one can't hide behind a success message
            string summary = $"Changes have been applied to {amendedCount} project(s) and will take effect the next time each is opened";
            if (unchangedCount > 0)
                summary += writeAll
                    ? $"\n\n{unchangedCount} project(s) had no Revit, CAD or DWF links and were left as they were"
                    : $"\n\n{unchangedCount} project(s) had no changed links and were left as they were";

            if (failures.Count == 0)
            {
                Show(summary, "Links Amended");
            }
            else
            {
                summary += "\n\n" + LinkConstants.MsgBatchTransmissionFailed + ":\n" + string.Join("\n", failures);
                MessageBox.Show(this, summary, amendedCount == 0 ? "Error - No Links Amended" : "Error - Some Links Not Amended",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region Window

        void Close_Click(object sender, RoutedEventArgs e) => Close();

        void Window_Closing(object sender, CancelEventArgs e)
        {
            LinksGrid.CommitEdit(DataGridEditingUnit.Row, true);
            int changed = links.Count(l => l.IsDirty);
            if (changed == 0) return;
            string unwritten = changed == 1 ? "1 changed link hasn't" : $"{changed} changed links haven't";
            if (MessageBox.Show(this, $"{unwritten} been written. Close without writing them?", "Close",
                    MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                e.Cancel = true;
        }

        void Help_Click(object sender, RoutedEventArgs e) => OpenHelp();

        void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                OpenHelp();
                e.Handled = true;
            }
            else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control && LinkTools.IsEnabled)
            {
                FindReplace_Click(sender, e);
                e.Handled = true;
            }
        }

        void OpenHelp()
        {
            // The help page sits in the bundle's Contents folder, one up from this year's DLL
            string dllFolder = Path.GetDirectoryName(typeof(LinksManagerWindow).Assembly.Location);
            string helpPage = Path.Combine(Directory.GetParent(dllFolder)?.FullName ?? dllFolder, "External Access Links Manager.html");
            if (File.Exists(helpPage)) ShellOpen.Open(helpPage);
            else Show("The help page wasn't found:\n" + helpPage, "Help");
        }

        void Show(string text, string caption) => MessageBox.Show(this, text, caption);

        #endregion
    }
}
