using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace MRDTools
{
    /// <summary>
    /// Link manager batch form — multiple projects from a folder.
    /// </summary>
    public partial class MRDLinksBatchForm : System.Windows.Forms.Form
    {
        #region Fields

        SortableBindingList<FolderContentsData> fileInfoList = new SortableBindingList<FolderContentsData>();

        readonly LinkGridLists links = new LinkGridLists();

        string currentOpenDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        #endregion

        #region Constructor

        public MRDLinksBatchForm(ExternalCommandData commandData)
        {
            InitializeComponent();
            Icon = FormIcon.Load();

            chkRevitFolder.CheckedChanged += new EventHandler(chkRevitFolder_CheckedChanged);
            chkCadFolder.CheckedChanged += new EventHandler(chkCadFolder_CheckedChanged);
            chkDwfFolder.CheckedChanged += new EventHandler(chkDwfFolder_CheckedChanged);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            // File selection grid (unique to batch form — set up manually)
            filesToProcessDGV.Controls.Clear();
            filesToProcessDGV.Columns.Clear();
            filesToProcessDGV.Dock = DockStyle.Fill;
            filesToProcessDGV.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            filesToProcessDGV.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            filesToProcessDGV.AllowUserToResizeRows = false;
            filesToProcessDGV.AutoGenerateColumns = false;
            filesToProcessDGV.Columns.Add(new DataGridViewTextBoxColumn { Name = "Project Name", DataPropertyName = "FileName", HeaderText = "Project Name", Width = 750, ReadOnly = true });
            filesToProcessDGV.Columns.Add(new DataGridViewCheckBoxColumn { Name = "To Process", DataPropertyName = "FileToBeProcessed", HeaderText = "To Process", Width = 100, TrueValue = true, FalseValue = false });
            filesToProcessDGV.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process Project", DataPropertyName = "FileToBeProcessedString", HeaderText = "Process Project", Width = 325, ReadOnly = true });
            filesToProcessDGV.Columns.Add(new DataGridViewTextBoxColumn { Name = "Last Saved", DataPropertyName = "FileSavedTime", HeaderText = "Last Saved", Width = 125, ReadOnly = true });
            filesToProcessDGV.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Empty", HeaderText = "", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
            filesToProcessDGV.CellValueChanged += new DataGridViewCellEventHandler(cBoxFileToBeProcessed_CellValueChanged);
            DataGridViewHelper.CommitCheckBoxEditsImmediately(filesToProcessDGV);
            filesToProcessDGV.DataSource = fileInfoList;

            DataGridViewCell cellGray = new DataGridViewTextBoxCell();
            cellGray.Style.BackColor = System.Drawing.Color.GhostWhite;

            // Read-only last-saved grids (with Project Name column)
            DataGridViewHelper.ConfigureReadOnlyGrid(RvtDGV, cellGray, includeSavedTime: true, dataSource: links.RvtSaved, includeProjectName: true);
            DataGridViewHelper.ConfigureReadOnlyGrid(CadDGV, cellGray, includeSavedTime: true, dataSource: links.CadSaved, includeProjectName: true);
            DataGridViewHelper.ConfigureReadOnlyGrid(DwfDGV, cellGray, includeSavedTime: true, dataSource: links.DwfSaved, includeProjectName: true);
            DataGridViewHelper.ConfigureReadOnlyGrid(OtherDGV, cellGray, includeSavedTime: false, dataSource: links.Other, includeProjectName: true, linkPathWidth: 450);

            // Editable desired-state grids (with Project Name column)
            DataGridViewHelper.ConfigureDesiredGrid(RvtDesiredDGV, cellGray, links.RvtDesired, BtnFileBrowseRvtDesired_CellClick, includeProjectName: true);
            DataGridViewHelper.ConfigureDesiredGrid(CadDesiredDGV, cellGray, links.CadDesired, BtnFileBrowseCadDesired_CellClick, includeProjectName: true);
            DataGridViewHelper.ConfigureDesiredGrid(DwfDesiredDGV, cellGray, links.DwfDesired, BtnFileBrowseDwfDesired_CellClick, includeProjectName: true);
        }

        #endregion

        #region Private Helpers

        /// <summary>Repath with absolute paths, even for links whose path type is Relative.</summary>
        private bool ForceAbsolute => cBoxAbsolutePaths.CheckState != CheckState.Unchecked;

        private void HandleFolderBrowseClick(System.Windows.Forms.TextBox targetTextBox)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog { Description = "Select folder to repath links to" };
            if (Directory.Exists(currentOpenDirectory))
                dlg.SelectedPath = currentOpenDirectory;
            else
                dlg.RootFolder = Environment.SpecialFolder.MyDocuments;

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                currentOpenDirectory = dlg.SelectedPath;
                targetTextBox.Text = dlg.SelectedPath;
            }
        }

        private void HandleFileBrowseCellClick(DataGridView dgv, DataGridViewCellEventArgs e, string filter)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != dgv.Columns["Browse Button"].Index) return;

            OpenFileDialog dlg = new OpenFileDialog { Filter = filter, FilterIndex = 2 };
            if (Directory.Exists(currentOpenDirectory))
                dlg.InitialDirectory = currentOpenDirectory;
            else
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using (dlg.OpenFile()) { }
                    currentOpenDirectory = Path.GetDirectoryName(dlg.FileName);
                    (dgv.Rows[e.RowIndex].DataBoundItem as LinkRow).Repath(dlg.FileName, ForceAbsolute);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file. Original error:" + ex.Message);
                }
            }
        }

        private void ApplyFolderToList(SortableBindingList<LinkRow> list, string folder, bool isChecked)
        {
            foreach (LinkRow row in list)
            {
                if (isChecked) row.RepathToFolder(folder, ForceAbsolute);
                else row.ResetPath();
            }
        }

        private void ResetPaths(SortableBindingList<LinkRow> list)
        {
            foreach (LinkRow row in list)
                row.ResetPath();
        }

        private void RepathSelectedRows(DataGridView dgv)
        {
            if (dgv.Rows.GetRowCount(DataGridViewElementStates.Selected) == 0)
            {
                System.Windows.MessageBox.Show(LinkConstants.MsgSelectRows, "Error");
                return;
            }

            FolderBrowserDialog dlg = new FolderBrowserDialog { Description = "Select folder to repath links to" };
            if (Directory.Exists(currentOpenDirectory))
                dlg.SelectedPath = currentOpenDirectory;
            else
                dlg.RootFolder = Environment.SpecialFolder.MyDocuments;

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                currentOpenDirectory = dlg.SelectedPath;
                foreach (DataGridViewRow row in dgv.SelectedRows)
                    (row.DataBoundItem as LinkRow).RepathToFolder(dlg.SelectedPath, ForceAbsolute);
            }
        }

        private void SetSelectedRowsLoadState(DataGridView dgv, bool loaded)
        {
            if (dgv.Rows.GetRowCount(DataGridViewElementStates.Selected) == 0)
            {
                System.Windows.MessageBox.Show(LinkConstants.MsgSelectRows, "Error");
                return;
            }
            foreach (DataGridViewRow row in dgv.SelectedRows)
                (row.DataBoundItem as LinkRow).DesiredLoaded = loaded;
        }

        private void HandleLoadUnloadCheckbox(bool setLoaded, SortableBindingList<LinkRow> list, CheckBox oppositeBox, bool isChecked)
        {
            if (isChecked)
            {
                oppositeBox.Checked = false;
                oppositeBox.Enabled = false;
                foreach (LinkRow row in list)
                    row.DesiredLoaded = setLoaded;
            }
            else
            {
                oppositeBox.Enabled = true;
                foreach (LinkRow row in list)
                    row.ResetLoaded();
            }
        }

        private string WriteReport(bool prependNote) =>
            LinkReport.WriteTempFile(links.SavedInGridOrder, links.DesiredInGridOrder, prependNote, includeProjectName: true);

        #endregion

        #region Exit

        private void btnClose_Click(object sender, EventArgs e) => this.Close();

        #endregion

        #region Browse Folder (file list)

        private void btnBrowseFolder_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog { Description = "Select folder to get Revit projects" };
            if (Directory.Exists(currentOpenDirectory))
                dlg.SelectedPath = currentOpenDirectory;
            else
                dlg.RootFolder = Environment.SpecialFolder.MyDocuments;

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                currentOpenDirectory = dlg.SelectedPath;
                fileInfoList.Clear();
                this.txtBrowseFolder.Text = currentOpenDirectory;

                SearchOption searchOption = cBoxSearchSubdirectories.CheckState == CheckState.Checked
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                foreach (string path in Directory.GetFiles(currentOpenDirectory, "*", searchOption))
                {
                    if (File.Exists(path) && GetExtension.RvtExtension(path))
                    {
                        string sFileName = Path.GetFileName(path);
                        fileInfoList.Add(new FolderContentsData(true, "Project will be Processed", sFileName,
                            GetLastSavedDate.LinksLastSavedDate(path), path));
                    }
                }

                chkFilesAllNone.Enabled = true;
                btnFixFileSelectionChoice.Enabled = true;
            }
        }

        #endregion

        #region Browse Individually

        private void BtnFileBrowseRvtDesired_CellClick(object sender, DataGridViewCellEventArgs e) =>
            HandleFileBrowseCellClick(RvtDesiredDGV, e, LinkConstants.FilterRvt);

        private void BtnFileBrowseCadDesired_CellClick(object sender, DataGridViewCellEventArgs e) =>
            HandleFileBrowseCellClick(CadDesiredDGV, e, LinkConstants.FilterCad);

        private void BtnFileBrowseDwfDesired_CellClick(object sender, DataGridViewCellEventArgs e) =>
            HandleFileBrowseCellClick(DwfDesiredDGV, e, LinkConstants.FilterDwf);

        #endregion

        #region File Selection Grid Events

        private void chkFilesAllNone_CheckedChanged(object sender, EventArgs e)
        {
            string stateText = chkFilesAllNone.Checked ? "Project will be Processed" : "Project will be Ignored";
            foreach (FolderContentsData fcd in fileInfoList)
            {
                fcd.FileToBeProcessed = chkFilesAllNone.Checked;
                fcd.FileToBeProcessedString = stateText;
            }
        }

        private void cBoxFileToBeProcessed_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == filesToProcessDGV.Columns["To Process"].Index && e.RowIndex != -1)
            {
                // Follow the checkbox, rather than toggling, so the text can't drift out of step with it
                bool toProcess = filesToProcessDGV.Rows[e.RowIndex].Cells["To Process"].Value as bool? == true;
                (filesToProcessDGV.Rows[e.RowIndex].DataBoundItem as FolderContentsData).FileToBeProcessedString =
                    toProcess ? "Project will be Processed" : "Project will be Ignored";
            }
        }

        #endregion

        #region Fix and Clear File Selection

        private void btnFixFileSelectionChoice_Click(object sender, EventArgs e)
        {
            txtBrowseFolder.Enabled = false;
            btnBrowseFolder.Enabled = false;
            chkFilesAllNone.Enabled = false;

            foreach (FolderContentsData fcd in fileInfoList)
            {
                if (fcd.FileToBeProcessedString == "Project will be Processed")
                    fcd.FileToBeProcessedString = "Project has been Processed";
                else if (fcd.FileToBeProcessedString == "Project will be Ignored")
                    fcd.FileToBeProcessedString = "Project has been Ignored";
            }

            links.Clear();

            foreach (FolderContentsData f in fileInfoList)
            {
                if (!f.FileToBeProcessed) continue;
                try
                {
                    links.Add(LinkScanner.Scan(f.FilePathName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file. Original error:" + ex.Message);
                }
            }

            btnClearFileSelectionChoice.Enabled = true;
            btnFixFileSelectionChoice.Enabled = false;
            btnExportExcel.Enabled = true;
            btnRepathRowsRevit.Enabled = btnRepathRowsCad.Enabled = btnRepathRowsDwf.Enabled = true;
            btnUnloadSelectedRowsRevit.Enabled = btnUnloadSelectedRowsCad.Enabled = btnUnloadSelectedRowsDwf.Enabled = true;
            btnLoadSelectedRowsRevit.Enabled = btnLoadSelectedRowsCad.Enabled = btnLoadSelectedRowsDwf.Enabled = true;
            btnResetPathsRevit.Enabled = btnResetPathsCad.Enabled = btnResetPathsDwf.Enabled = true;
            cBoxAbsolutePaths.Enabled = true;
            btnUncheckRows.Enabled = false;
            btnCheckRows.Enabled = false;
            cBoxSearchSubdirectories.Enabled = false;
            btnPrintTextOnly.Enabled = chkTextPrint.Enabled = btnMakeChanges.Enabled = true;
            cBoxBasicChangesRevitLoad.Enabled = cBoxBasicChangesRevitUnload.Enabled = true;
            cBoxBasicChangesCADLoad.Enabled = cBoxBasicChangesCADUnload.Enabled = true;
            cBoxBasicChangesDWFLoad.Enabled = cBoxBasicChangesDWFUnload.Enabled = true;
            btnBrowseFolderRevitDGV.Enabled = chkRevitFolder.Enabled = txtBrowseFolderRevit.Enabled = true;
            btnBrowseFolderCad.Enabled = chkCadFolder.Enabled = txtBrowseFolderCad.Enabled = true;
            btnBrowseFolderDwf.Enabled = chkDwfFolder.Enabled = txtBrowseFolderDwf.Enabled = true;
            filesToProcessDGV.Enabled = false;
        }

        private void btnClearFileSelectionChoice_Click(object sender, EventArgs e)
        {
            txtBrowseFolder.Enabled = true;
            btnBrowseFolder.Enabled = true;
            chkFilesAllNone.Enabled = true;

            foreach (FolderContentsData fcd in fileInfoList)
            {
                if (fcd.FileToBeProcessedString == "Project has been Processed")
                    fcd.FileToBeProcessedString = "Project will be Processed";
                else if (fcd.FileToBeProcessedString == "Project has been Ignored")
                    fcd.FileToBeProcessedString = "Project will be Ignored";
            }

            links.Clear();

            btnFixFileSelectionChoice.Enabled = true;
            btnClearFileSelectionChoice.Enabled = false;
            btnPrintTextOnly.Enabled = chkTextPrint.Enabled = btnMakeChanges.Enabled = false;

            cBoxBasicChangesDWFLoad.Checked = cBoxBasicChangesDWFUnload.Checked = false;
            cBoxBasicChangesCADLoad.Checked = cBoxBasicChangesCADUnload.Checked = false;
            cBoxBasicChangesRevitLoad.Checked = cBoxBasicChangesRevitUnload.Checked = false;

            txtBrowseFolderRevit.Text = txtBrowseFolderCad.Text = txtBrowseFolderDwf.Text = "";
            chkRevitFolder.Checked = chkCadFolder.Checked = chkDwfFolder.Checked = false;

            cBoxBasicChangesDWFLoad.Enabled = cBoxBasicChangesDWFUnload.Enabled = false;
            cBoxBasicChangesCADLoad.Enabled = cBoxBasicChangesCADUnload.Enabled = false;
            cBoxBasicChangesRevitLoad.Enabled = cBoxBasicChangesRevitUnload.Enabled = false;

            btnBrowseFolderRevitDGV.Enabled = chkRevitFolder.Enabled = txtBrowseFolderRevit.Enabled = false;
            btnBrowseFolderCad.Enabled = chkCadFolder.Enabled = txtBrowseFolderCad.Enabled = false;
            btnBrowseFolderDwf.Enabled = chkDwfFolder.Enabled = txtBrowseFolderDwf.Enabled = false;

            btnExportExcel.Enabled = false;
            btnRepathRowsRevit.Enabled = btnRepathRowsCad.Enabled = btnRepathRowsDwf.Enabled = false;
            btnUnloadSelectedRowsRevit.Enabled = btnUnloadSelectedRowsCad.Enabled = btnUnloadSelectedRowsDwf.Enabled = false;
            btnLoadSelectedRowsRevit.Enabled = btnLoadSelectedRowsCad.Enabled = btnLoadSelectedRowsDwf.Enabled = false;
            btnResetPathsRevit.Enabled = btnResetPathsCad.Enabled = btnResetPathsDwf.Enabled = false;

            cBoxAbsolutePaths.Enabled = false;
            btnUncheckRows.Enabled = btnCheckRows.Enabled = true;
            cBoxSearchSubdirectories.Enabled = true;
            filesToProcessDGV.Enabled = true;
        }

        #endregion

        #region Print to Text

        private void btnPrintTextOnly_Click(object sender, EventArgs e) => ShellOpen.Open(WriteReport(prependNote: true));

        #endregion

        #region Commit Changes

        private void btnMakeChanges_Click(object sender, EventArgs e)
        {
            // Only changed links are written, so a project with none isn't touched
            if (!links.HasChanges)
            {
                System.Windows.MessageBox.Show(LinkConstants.MsgNoChanges, "No Changes");
                return;
            }

            int amendedCount = 0;
            int unchangedCount = 0;
            List<string> failures = new List<string>();

            foreach (FolderContentsData f in fileInfoList)
            {
                if (!f.FileToBeProcessed) continue;

                try
                {
                    if (LinkWriter.Write(f.FilePathName, links.All) > 0)
                        amendedCount++;
                    else
                        unchangedCount++;
                }
                catch (Exception ex)
                {
                    failures.Add(f.FileName + " - " + ex.Message);
                }
            }

            // One summary for the whole batch, so a failed project can't hide behind a success message
            string summary = $"Changes have been applied to {amendedCount} project(s) and will take effect next time each project is opened";
            if (unchangedCount > 0)
                summary += $"\n\n{unchangedCount} project(s) had no changed links and were left as they were";

            string outputfile = null;
            if (chkTextPrint.Checked)
            {
                try { outputfile = WriteReport(prependNote: false); }
                catch (Exception ex) { summary += "\n\nThe text file could not be written: " + ex.Message; }
            }

            if (failures.Count == 0)
            {
                System.Windows.MessageBox.Show(summary, "Links Amended");
            }
            else
            {
                summary += "\n\n" + LinkConstants.MsgBatchTransmissionFailed + ":\n" + string.Join("\n", failures);
                System.Windows.MessageBox.Show(summary, amendedCount == 0 ? "Error - No Links Amended" : "Error - Some Links Not Amended",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }

            if (outputfile != null) ShellOpen.Open(outputfile);
        }

        #endregion

        #region Browse Folders

        private void btnBrowseFolderRevitDGV_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderRevit);
        private void btnBrowseFolderCad_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderCad);
        private void btnBrowseFolderDwf_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderDwf);

        #endregion

        #region Folder Apply Checkboxes

        private void chkRevitFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(links.RvtDesired, txtBrowseFolderRevit.Text, chkRevitFolder.Checked);

        private void chkCadFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(links.CadDesired, txtBrowseFolderCad.Text, chkCadFolder.Checked);

        private void chkDwfFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(links.DwfDesired, txtBrowseFolderDwf.Text, chkDwfFolder.Checked);

        #endregion

        #region Load / Unload All Checkboxes

        private void cBoxBasicChangesRevitUnload_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: false, links.RvtDesired, cBoxBasicChangesRevitLoad, cBoxBasicChangesRevitUnload.Checked);

        private void cBoxBasicChangesRevitLoad_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: true, links.RvtDesired, cBoxBasicChangesRevitUnload, cBoxBasicChangesRevitLoad.Checked);

        private void cBoxBasicChangesCADUnload_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: false, links.CadDesired, cBoxBasicChangesCADLoad, cBoxBasicChangesCADUnload.Checked);

        private void cBoxBasicChangesCADLoad_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: true, links.CadDesired, cBoxBasicChangesCADUnload, cBoxBasicChangesCADLoad.Checked);

        private void cBoxBasicChangesDWFUnload_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: false, links.DwfDesired, cBoxBasicChangesDWFLoad, cBoxBasicChangesDWFUnload.Checked);

        private void cBoxBasicChangesDWFLoad_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: true, links.DwfDesired, cBoxBasicChangesDWFUnload, cBoxBasicChangesDWFLoad.Checked);

        #endregion

        #region Excel Export

        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog { Filter = "Excel Files (*.xlsx)|*.xlsx" };
            if (Directory.Exists(currentOpenDirectory))
                saveDialog.InitialDirectory = currentOpenDirectory;
            else
                saveDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (saveDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = saveDialog.FileName;
                if (File.Exists(filename))
                {
                    System.Windows.MessageBox.Show("You must create a new excel file. Overwriting is not permitted", "Error");
                    return;
                }

                LinkWorkbook.Save(filename, links.SavedInGridOrder, links.DesiredInGridOrder);
                ShellOpen.Open(filename);
            }
        }

        #endregion

        #region Repath Selected Rows

        private void btnRepathRows_Click(object sender, EventArgs e) => RepathSelectedRows(RvtDesiredDGV);
        private void btnRepathRowsCad_Click(object sender, EventArgs e) => RepathSelectedRows(CadDesiredDGV);
        private void btnRepathRowsDwf_Click(object sender, EventArgs e) => RepathSelectedRows(DwfDesiredDGV);

        #endregion

        #region Reset Paths

        private void btnResetPaths_Click(object sender, EventArgs e) => ResetPaths(links.RvtDesired);
        private void btnResetPathsCad_Click(object sender, EventArgs e) => ResetPaths(links.CadDesired);
        private void btnResetPathsDwf_Click(object sender, EventArgs e) => ResetPaths(links.DwfDesired);

        #endregion

        #region Load / Unload Selected Rows

        private void btnUnloadSelectedRowsRevit_Click(object sender, EventArgs e) => SetSelectedRowsLoadState(RvtDesiredDGV, loaded: false);
        private void btnLoadSelectedRowsRevit_Click(object sender, EventArgs e) => SetSelectedRowsLoadState(RvtDesiredDGV, loaded: true);
        private void btnUnloadSelectedRowsCad_Click(object sender, EventArgs e) => SetSelectedRowsLoadState(CadDesiredDGV, loaded: false);
        private void btnLoadSelectedRowsCad_Click(object sender, EventArgs e) => SetSelectedRowsLoadState(CadDesiredDGV, loaded: true);
        private void btnUnloadSelectedRowsDwf_Click(object sender, EventArgs e) => SetSelectedRowsLoadState(DwfDesiredDGV, loaded: false);
        private void btnLoadSelectedRowsDwf_Click(object sender, EventArgs e) => SetSelectedRowsLoadState(DwfDesiredDGV, loaded: true);

        #endregion

        #region Check / Uncheck Selected File Rows

        private void btnUncheckRows_Click(object sender, EventArgs e)
        {
            if (filesToProcessDGV.Rows.GetRowCount(DataGridViewElementStates.Selected) == 0)
            {
                System.Windows.MessageBox.Show("Please select rows from the 'FOLDER CONTENTS' table and try again", "Error");
                return;
            }
            foreach (DataGridViewRow row in filesToProcessDGV.SelectedRows)
            {
                FolderContentsData srd = row.DataBoundItem as FolderContentsData;
                srd.FileToBeProcessed = false;
                srd.FileToBeProcessedString = "Project will be Ignored";
            }
        }

        private void btnCheckRows_Click(object sender, EventArgs e)
        {
            if (filesToProcessDGV.Rows.GetRowCount(DataGridViewElementStates.Selected) == 0)
            {
                System.Windows.MessageBox.Show("Please select rows from the 'FOLDER CONTENTS' table and try again", "Error");
                return;
            }
            foreach (DataGridViewRow row in filesToProcessDGV.SelectedRows)
            {
                FolderContentsData srd = row.DataBoundItem as FolderContentsData;
                srd.FileToBeProcessed = true;
                srd.FileToBeProcessedString = "Project will be Processed";
            }
        }

        #endregion
    }
}
