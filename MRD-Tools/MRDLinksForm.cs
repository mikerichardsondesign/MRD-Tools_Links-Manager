using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Windows.Forms;


namespace MRDTools
{
    /// <summary>
    /// Link manager main form — single project mode.
    /// </summary>
    public partial class MRDLinksForm : System.Windows.Forms.Form
    {
        #region Fields

        readonly LinkGridLists links = new LinkGridLists();

        string currentOpenDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        #endregion

        #region Constructor

        public MRDLinksForm(ExternalCommandData commandData)
        {
            InitializeComponent();
            Icon = FormIcon.Load();
            toolTip.SetToolTip(chkWriteAllLinks, LinkConstants.TipWriteAllLinks);

            btnBrowseFolderRevitDGV.Click += new EventHandler(btnBrowseFolderRevitDGV_Click);
            btnBrowseFolderDwf.Click += new EventHandler(btnBrowseFolderDwf_Click);
            chkRevitFolder.CheckedChanged += new EventHandler(chkRevitFolder_CheckedChanged);
            chkCadFolder.CheckedChanged += new EventHandler(chkCadFolder_CheckedChanged);
            chkDwfFolder.CheckedChanged += new EventHandler(chkDwfFolder_CheckedChanged);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            DataGridViewCell cellGray = new DataGridViewTextBoxCell();
            cellGray.Style.BackColor = System.Drawing.Color.GhostWhite;

            // Read-only last-saved grids
            DataGridViewHelper.ConfigureReadOnlyGrid(RvtDGV, cellGray, includeSavedTime: true, dataSource: links.RvtSaved);
            DataGridViewHelper.ConfigureReadOnlyGrid(CadDGV, cellGray, includeSavedTime: true, dataSource: links.CadSaved);
            DataGridViewHelper.ConfigureReadOnlyGrid(DwfDGV, cellGray, includeSavedTime: true, dataSource: links.DwfSaved);
            DataGridViewHelper.ConfigureReadOnlyGrid(OtherDGV, cellGray, includeSavedTime: false, dataSource: links.Other, linkPathWidth: 450);

            // Editable desired-state grids
            DataGridViewHelper.ConfigureDesiredGrid(RvtDesiredDGV, cellGray, links.RvtDesired, BtnFileBrowseRvtDesired_CellClick);
            DataGridViewHelper.ConfigureDesiredGrid(CadDesiredDGV, cellGray, links.CadDesired, BtnFileBrowseCadDesired_CellClick);
            DataGridViewHelper.ConfigureDesiredGrid(DwfDesiredDGV, cellGray, links.DwfDesired, BtnFileBrowseDwfDesired_CellClick);
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
            LinkReport.WriteTempFile(links.SavedInGridOrder, links.DesiredInGridOrder, prependNote, includeProjectName: false);

        #endregion

        #region Browse Folders

        private void btnBrowseFolderRevitDGV_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderRevit);
        private void btnBrowseFolderCad_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderCad);
        private void btnBrowseFolderDwf_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderDwf);

        #endregion

        #region Commit Changes

        private void btnMakeChanges_Click(object sender, EventArgs e)
        {
            string projectPath = this.txtBrowseProject.Text;
            if (string.IsNullOrEmpty(projectPath))
            {
                System.Windows.MessageBox.Show(LinkConstants.MsgOperationFailed, "Error - Operation Failed");
                return;
            }

            // Only changed links are written, unless Write All Links is ticked, so a project with none isn't touched
            bool writeAll = chkWriteAllLinks.Checked;
            if (!links.HasLinksToWrite(writeAll))
            {
                System.Windows.MessageBox.Show(writeAll ? LinkConstants.MsgNoLinksToWrite : LinkConstants.MsgNoChanges, "No Changes");
                return;
            }

            int changed;
            try
            {
                changed = LinkWriter.Write(projectPath, links.All, writeAll);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(LinkConstants.MsgTransmissionFailed + "\n\n" + ex.Message, "Error - Transmission Failed");
                return;
            }

            if (changed == 0)
            {
                System.Windows.MessageBox.Show(LinkConstants.MsgLinksNotFound, "No Changes");
                return;
            }

            string summary = writeAll
                ? $"All {changed} link(s) have been written and will take effect next time the project is opened"
                : $"Changes to {changed} link(s) have been applied and will take effect next time the project is opened";
            string outputfile = null;
            if (chkTextPrint.Checked)
            {
                try { outputfile = WriteReport(prependNote: false); }
                catch (Exception ex) { summary += "\n\nThe text file could not be written: " + ex.Message; }
            }

            System.Windows.MessageBox.Show(summary, "Links Amended");
            if (outputfile != null) ShellOpen.Open(outputfile);
        }

        #endregion

        #region Print to Text

        private void btnPrintTextOnly_Click(object sender, EventArgs e) => ShellOpen.Open(WriteReport(prependNote: true));

        #endregion

        #region Browse Individually

        private void BtnFileBrowseRvtDesired_CellClick(object sender, DataGridViewCellEventArgs e) =>
            HandleFileBrowseCellClick(RvtDesiredDGV, e, LinkConstants.FilterRvt);

        private void BtnFileBrowseCadDesired_CellClick(object sender, DataGridViewCellEventArgs e) =>
            HandleFileBrowseCellClick(CadDesiredDGV, e, LinkConstants.FilterCad);

        private void BtnFileBrowseDwfDesired_CellClick(object sender, DataGridViewCellEventArgs e) =>
            HandleFileBrowseCellClick(DwfDesiredDGV, e, LinkConstants.FilterDwf);

        #endregion

        #region Folder Apply Checkboxes

        private void chkRevitFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(links.RvtDesired, txtBrowseFolderRevit.Text, chkRevitFolder.Checked);

        private void chkCadFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(links.CadDesired, txtBrowseFolderCad.Text, chkCadFolder.Checked);

        private void chkDwfFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(links.DwfDesired, txtBrowseFolderDwf.Text, chkDwfFolder.Checked);

        #endregion

        #region Exit

        private void btnClose_Click(object sender, EventArgs e) => this.Close();

        #endregion

        #region Browse Project

        private void btnBrowseProject_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = LinkConstants.FilterRvt, FilterIndex = 2 };
            if (Directory.Exists(currentOpenDirectory))
                dlg.InitialDirectory = currentOpenDirectory;
            else
                dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using (dlg.OpenFile()) { }

                    string filename = dlg.FileName;
                    currentOpenDirectory = Path.GetDirectoryName(filename);
                    this.txtBrowseProject.Text = filename;

                    links.Clear();
                    links.Add(LinkScanner.Scan(filename));

                    // Enable controls after successful load
                    btnExportExcel.Enabled = true;
                    btnRepathRowsRevit.Enabled = btnRepathRowsCad.Enabled = btnRepathRowsDwf.Enabled = true;
                    btnUnloadSelectedRowsRevit.Enabled = btnUnloadSelectedRowsCad.Enabled = btnUnloadSelectedRowsDwf.Enabled = true;
                    btnLoadSelectedRowsRevit.Enabled = btnLoadSelectedRowsCad.Enabled = btnLoadSelectedRowsDwf.Enabled = true;
                    btnResetPathsRevit.Enabled = btnResetPathsCad.Enabled = btnResetPathsDwf.Enabled = true;
                    btnBrowseFolderRevitDGV.Enabled = chkRevitFolder.Enabled = txtBrowseFolderRevit.Enabled = true;
                    btnBrowseFolderCad.Enabled = chkCadFolder.Enabled = txtBrowseFolderCad.Enabled = true;
                    btnBrowseFolderDwf.Enabled = chkDwfFolder.Enabled = txtBrowseFolderDwf.Enabled = true;
                    cBoxAbsolutePaths.Enabled = btnMakeChanges.Enabled = chkTextPrint.Enabled = chkWriteAllLinks.Enabled = btnPrintTextOnly.Enabled = true;
                    cBoxBasicChangesDWFLoad.Enabled = cBoxBasicChangesDWFUnload.Enabled = true;
                    cBoxBasicChangesCADLoad.Enabled = cBoxBasicChangesCADUnload.Enabled = true;
                    cBoxBasicChangesRevitLoad.Enabled = cBoxBasicChangesRevitUnload.Enabled = true;

                    cBoxBasicChangesDWFLoad.Checked = cBoxBasicChangesDWFUnload.Checked = false;
                    cBoxBasicChangesCADLoad.Checked = cBoxBasicChangesCADUnload.Checked = false;
                    cBoxBasicChangesRevitLoad.Checked = cBoxBasicChangesRevitUnload.Checked = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file. Original error:" + ex.Message);
                }
            }
        }

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

        private void btnRepathRowsRevit_Click(object sender, EventArgs e) => RepathSelectedRows(RvtDesiredDGV);
        private void btnRepathRowsCad_Click(object sender, EventArgs e) => RepathSelectedRows(CadDesiredDGV);
        private void btnRepathRowsDwf_Click(object sender, EventArgs e) => RepathSelectedRows(DwfDesiredDGV);

        #endregion

        #region Reset Paths

        private void btnResetPathsRevit_Click(object sender, EventArgs e) => ResetPaths(links.RvtDesired);
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

        #region Repath Option Checkbox

        private void cBoxAbsolutePaths_CheckedChanged(object sender, EventArgs e)
        {
            // Referenced from elsewhere; intentionally empty.
        }

        #endregion

    }
}
