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
            VersionText.Text = typeof(LinksManagerWindow).Assembly.GetName().Version.ToString(3);
            WriteAllLinks.ToolTip = LinkConstants.TipWriteAllLinks;

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

        /// <summary>Re-filters the links grid, then brings every count, header and button up to date.</summary>
        void RefreshAll()
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

            ProjectCount.Text = projects.Count == 0 ? "" : projects.Count == 1 ? "1 project" : $"{projects.Count} projects";
            ProjectsHint.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LinksSummary.Text = processed.Count == 0 ? "" : $"{processed.Count} link(s) in {projectsWithLinks} project(s)";

            int changed = processed.Count(l => l.IsDirty);
            ChangesBadgeText.Text = changed == 1 ? "1 LINK CHANGED" : $"{changed} LINKS CHANGED";
            ChangesBadge.Visibility = changed > 0 ? Visibility.Visible : Visibility.Collapsed;

            MakeChangesButton.IsEnabled = projects.Any(p => p.Process);
            TextReportButton.IsEnabled = ExcelButton.IsEnabled = processed.Count > 0;
            LinkTools.IsEnabled = SelectedKind != LinkKind.Other && linksView.Count > 0;
        }

        void Link_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!bulkEdit && e.PropertyName == nameof(LinkRow.IsDirty)) RefreshCounts();
        }

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
            return LinkReport.WriteTempFile(processed, processed.Where(l => l.IsEditable), includeProjectName: manyProjects);
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

        #endregion

        #region Make Changes

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
            if (!ProcessedLinks().Any(r => r.IsEditable && (writeAll || r.IsDirty)))
            {
                Show(writeAll ? LinkConstants.MsgNoLinksToWrite : LinkConstants.MsgNoChanges, "No Changes");
                return;
            }

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
            if (e.Key != Key.F1) return;
            OpenHelp();
            e.Handled = true;
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
