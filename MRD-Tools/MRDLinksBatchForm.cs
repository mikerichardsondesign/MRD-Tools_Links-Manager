using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
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

        SortableBindingList<LinkData> linkInfoListRvt = new SortableBindingList<LinkData>();
        SortableBindingList<LinkData> linkInfoListRvtDesired = new SortableBindingList<LinkData>();
        SortableBindingList<LinkData> linkInfoListCad = new SortableBindingList<LinkData>();
        SortableBindingList<LinkData> linkInfoListCadDesired = new SortableBindingList<LinkData>();
        SortableBindingList<LinkData> linkInfoListDwf = new SortableBindingList<LinkData>();
        SortableBindingList<LinkData> linkInfoListDwfDesired = new SortableBindingList<LinkData>();
        SortableBindingList<LinkData> linkInfoListOther = new SortableBindingList<LinkData>();

        List<PersistantLinkData> perLinkInfoListRvtDesired = new List<PersistantLinkData>();
        List<PersistantLinkData> perLinkInfoListCadDesired = new List<PersistantLinkData>();
        List<PersistantLinkData> perLinkInfoListDwfDesired = new List<PersistantLinkData>();

        List<PersistantLoadUnloadData> perLoadUnloadListRvtDesired = new List<PersistantLoadUnloadData>();
        List<PersistantLoadUnloadData> perLoadUnloadListCadDesired = new List<PersistantLoadUnloadData>();
        List<PersistantLoadUnloadData> perLoadUnloadListDwfDesired = new List<PersistantLoadUnloadData>();

        string currentOpenDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        #endregion

        #region Constructor

        public MRDLinksBatchForm(ExternalCommandData commandData)
        {
            InitializeComponent();

            chkFilesAllNone.CheckedChanged += new EventHandler(chkFilesAllNone_CheckedChanged);
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
            filesToProcessDGV.CellMouseUp += new DataGridViewCellMouseEventHandler(cBoxFileToBeProcessed_CellMouseUp);
            filesToProcessDGV.DataSource = fileInfoList;

            DataGridViewCell cellGray = new DataGridViewTextBoxCell();
            cellGray.Style.BackColor = System.Drawing.Color.GhostWhite;

            // Read-only last-saved grids (with Project Name column)
            DataGridViewHelper.ConfigureReadOnlyGrid(RvtDGV, cellGray, includeSavedTime: true, dataSource: linkInfoListRvt, includeProjectName: true);
            DataGridViewHelper.ConfigureReadOnlyGrid(CadDGV, cellGray, includeSavedTime: true, dataSource: linkInfoListCad, includeProjectName: true);
            DataGridViewHelper.ConfigureReadOnlyGrid(DwfDGV, cellGray, includeSavedTime: true, dataSource: linkInfoListDwf, includeProjectName: true);
            DataGridViewHelper.ConfigureReadOnlyGrid(OtherDGV, cellGray, includeSavedTime: false, dataSource: linkInfoListOther, includeProjectName: true, linkPathWidth: 450);

            // Editable desired-state grids (with Project Name column)
            DataGridViewHelper.ConfigureDesiredGrid(RvtDesiredDGV, cellGray, linkInfoListRvtDesired,
                BtnFileBrowseRvtDesired_CellClick, cBoxLinkStateRvtDesired_CellValueChanged, cBoxLinkStateRvtDesired_CellMouseUp, includeProjectName: true);
            DataGridViewHelper.ConfigureDesiredGrid(CadDesiredDGV, cellGray, linkInfoListCadDesired,
                BtnFileBrowseCadDesired_CellClick, cBoxLinkStateCadDesired_CellValueChanged, cBoxLinkStateCadDesired_CellMouseUp, includeProjectName: true);
            DataGridViewHelper.ConfigureDesiredGrid(DwfDesiredDGV, cellGray, linkInfoListDwfDesired,
                BtnFileBrowseDwfDesired_CellClick, cBoxLinkStateDwfDesired_CellValueChanged, cBoxLinkStateDwfDesired_CellMouseUp, includeProjectName: true);
        }

        #endregion

        #region Private Helpers

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

        private void ApplyPathToLink(LinkData srd, string targetPath)
        {
            srd.RefName = Path.GetFileName(targetPath);
            if (srd.RefPathType == LinkConstants.PathTypeRelative && cBoxAbsolutePaths.CheckState == CheckState.Unchecked)
            {
                try { srd.RefPath = GetRelativePath.GetRelativePathUriA(targetPath, srd.FilePath); }
                catch (Exception) { srd.RefPath = targetPath; }
            }
            else
            {
                srd.RefPath = targetPath;
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
                    ApplyPathToLink(dgv.Rows[e.RowIndex].DataBoundItem as LinkData, dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file. Original error:" + ex.Message);
                }
            }
        }

        private void ApplyFolderToList(SortableBindingList<LinkData> list, List<PersistantLinkData> persistent,
            string folder, bool isChecked)
        {
            if (isChecked)
            {
                foreach (LinkData ld in list)
                {
                    string targetPath = Path.Combine(folder, Path.GetFileName(ld.RefName));
                    if (ld.RefPathType == LinkConstants.PathTypeRelative && cBoxAbsolutePaths.CheckState == CheckState.Unchecked)
                    {
                        try { ld.RefPath = GetRelativePath.GetRelativePathUriA(targetPath, ld.FilePath); }
                        catch (Exception) { ld.RefPath = targetPath; }
                    }
                    else
                    {
                        ld.RefPath = targetPath;
                    }
                }
            }
            else
            {
                ResetPaths(list, persistent);
            }
        }

        private void ResetPaths(SortableBindingList<LinkData> list, List<PersistantLinkData> persistent)
        {
            foreach (LinkData ld in list)
            {
                foreach (PersistantLinkData pld in persistent)
                {
                    if (pld.RefStableId == ld.RefStableId)
                    {
                        ld.RefPath = pld.RefPath;
                        ld.RefName = pld.RefName;
                    }
                }
            }
        }

        private void RepathSelectedRows(DataGridView dgv)
        {
            if (dgv.Rows.GetRowCount(DataGridViewElementStates.Selected) == 0)
            {
                System.Windows.MessageBox.Show("Error", LinkConstants.MsgSelectRows);
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
                {
                    LinkData srd = row.DataBoundItem as LinkData;
                    string targetPath = Path.Combine(dlg.SelectedPath, Path.GetFileName(srd.RefName));
                    if (srd.RefPathType == LinkConstants.PathTypeRelative && cBoxAbsolutePaths.CheckState == CheckState.Unchecked)
                    {
                        try { srd.RefPath = GetRelativePath.GetRelativePathUriA(targetPath, srd.FilePath); }
                        catch (Exception) { srd.RefPath = targetPath; }
                    }
                    else
                    {
                        srd.RefPath = targetPath;
                    }
                }
            }
        }

        private void SetSelectedRowsLoadState(DataGridView dgv, bool loaded)
        {
            if (dgv.Rows.GetRowCount(DataGridViewElementStates.Selected) == 0)
            {
                System.Windows.MessageBox.Show("Error", LinkConstants.MsgSelectRows);
                return;
            }
            foreach (DataGridViewRow row in dgv.SelectedRows)
            {
                LinkData srd = row.DataBoundItem as LinkData;
                srd.RefIsLoaded = loaded;
                srd.RefLoadState = loaded ? LinkConstants.StateLoaded : LinkConstants.StateUnloaded;
            }
        }

        private void HandleLoadUnloadCheckbox(bool setLoaded, SortableBindingList<LinkData> list,
            List<PersistantLoadUnloadData> persistent, CheckBox oppositeBox, bool isChecked)
        {
            if (isChecked)
            {
                oppositeBox.Checked = false;
                oppositeBox.Enabled = false;
                foreach (LinkData ld in list)
                {
                    ld.RefIsLoaded = setLoaded;
                    ld.RefLoadState = setLoaded ? LinkConstants.StateLoaded : LinkConstants.StateUnloaded;
                }
            }
            else
            {
                oppositeBox.Enabled = true;
                foreach (LinkData ld in list)
                {
                    foreach (PersistantLoadUnloadData pld in persistent)
                    {
                        if (pld.RefStableId == ld.RefStableId)
                        {
                            ld.RefIsLoaded = pld.RefIsLoaded;
                            ld.RefLoadState = pld.RefLoadState;
                        }
                    }
                }
            }
        }

        private void HandleCellValueChanged(DataGridView dgv, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dgv.Columns["Link State CBox"].Index && e.RowIndex != -1)
            {
                string current = dgv.Rows[e.RowIndex].Cells["Link State"].Value.ToString();
                dgv.Rows[e.RowIndex].Cells["Link State"].Value =
                    current == LinkConstants.StateLoaded ? LinkConstants.StateUnloaded : LinkConstants.StateLoaded;
            }
        }

        private void HandleCellMouseUp(DataGridView dgv, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex == dgv.Columns["Link State"].Index && e.RowIndex != -1)
                dgv.EndEdit();
        }

        private bool GetIsLoaded(string loadState)
        {
            return loadState == LinkConstants.StateNotFound || loadState == LinkConstants.StateLoaded;
        }

        private void AppendLinkSection(StringBuilder sb, string header, SortableBindingList<LinkData> list, bool includeSavedTime)
        {
            sb.AppendLine(header);
            sb.Append("PROJECT NAME\tLINK ID\tLINK NAME\tLINK STATE\tLINK PATH");
            if (includeSavedTime) sb.Append("\tDATE");
            sb.AppendLine();

            foreach (LinkData l in list)
            {
                sb.Append($"{l.FileName}\t{l.RefId}\t{l.RefName}\t{l.RefLoadState}\t{l.RefPath}");
                if (includeSavedTime) sb.Append($"\t{l.RefSavedTime}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }

        private string BuildLinkReport(bool prependNote)
        {
            var sb = new StringBuilder();
            if (prependNote)
            {
                sb.AppendLine("PLEASE NOTE THAT THIS OUTPUT IS WHAT WILL BE TRUE IF CHANGES ARE COMMITED BY PRESSING THE 'MAKE CHANGES' BUTTON");
                sb.AppendLine("IF CHANGES ARE NOT COMMITTED THEN THE STATUS OF LINKS WILL STAY AS THEY WERE");
                sb.AppendLine();
            }

            AppendLinkSection(sb, "REVIT - LAST SAVED LINK STATUS", linkInfoListRvt, includeSavedTime: true);
            AppendLinkSection(sb, "REVIT - CURRENT LINK STATUS WHICH WILL BE APPLIED UPON NEXT OPEN OF PROJECT", linkInfoListRvtDesired, includeSavedTime: false);
            AppendLinkSection(sb, "CAD - LAST SAVED LINK STATUS", linkInfoListCad, includeSavedTime: true);
            AppendLinkSection(sb, "CAD - CURRENT LINK STATUS WHICH WILL BE APPLIED UPON NEXT OPEN OF PROJECT", linkInfoListCadDesired, includeSavedTime: false);
            AppendLinkSection(sb, "DWF - LAST SAVED LINK STATUS", linkInfoListDwf, includeSavedTime: true);
            AppendLinkSection(sb, "DWF - CURRENT LINK STATUS WHICH WILL BE APPLIED UPON NEXT OPEN OF PROJECT", linkInfoListDwfDesired, includeSavedTime: false);
            AppendLinkSection(sb, "OTHER LINKS - LAST SAVED LINK STATUS", linkInfoListOther, includeSavedTime: false);
            return sb.ToString();
        }

        private int WriteLinksToSheet(ExcelWorksheet sheet, int row, params SortableBindingList<LinkData>[] lists)
        {
            foreach (var list in lists)
            {
                foreach (LinkData l in list)
                {
                    sheet.Cells[row, 1].Value = l.FileName;
                    sheet.Cells[row, 2].Value = l.RefId;
                    sheet.Cells[row, 3].Value = l.RefName;
                    sheet.Cells[row, 4].Value = l.RefLoadState;
                    sheet.Cells[row, 5].Value = l.RefPath;
                    sheet.Cells[row, 6].Value = l.RefSavedTime;
                    sheet.Cells[row, 7].Value = l.RefLinkType;
                    sheet.Cells[row, 8].Value = l.RefStableId;
                    sheet.Cells[row, 9].Value = l.FilePath;
                    row++;
                }
            }
            return row;
        }

        private void SetupExcelSheetHeaders(ExcelWorksheet sheet, string title)
        {
            sheet.Cells.Style.Font.Size = 10;
            sheet.Cells.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            sheet.Cells.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            sheet.Column(1).Width = 37;
            sheet.Column(2).Width = 15;
            sheet.Column(3).Width = 37;
            sheet.Column(4).Width = 15;
            sheet.Column(5).Width = 50;
            sheet.Column(6).Width = 20;
            sheet.Column(7).Width = 20;
            sheet.Column(8).Width = 37;
            sheet.Column(9).Width = 50;

            sheet.Cells["A1"].Style.Font.Bold = true;
            sheet.Cells["A1"].Value = title;

            string[] headers = { "Project Name", "Link Id", "Link Name", "Link State", "Link Path", "Date", "Link Type", "Link Stable Id", "Master File Path" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[2, i + 1].Style.Font.Bold = true;
                sheet.Cells[2, i + 1].Value = headers[i];
            }

            sheet.Cells["A1:I1"].Merge = true;
            sheet.Cells["A1:I1"].Style.Border.Top.Style = ExcelBorderStyle.Thick;
            sheet.Cells["A1:I1"].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
            sheet.Cells["A1:I1"].Style.Border.Left.Style = ExcelBorderStyle.Thick;
            sheet.Cells["A1:I1"].Style.Border.Right.Style = ExcelBorderStyle.Thick;

            sheet.Cells["A2:I2"].AutoFilter = true;
            sheet.Column(8).Hidden = true;
            sheet.Column(9).Hidden = true;
            sheet.HeaderFooter.OddFooter.RightAlignedText = "Page &P of &N";
        }

        private void ClearAllLinkLists()
        {
            linkInfoListRvt.Clear(); linkInfoListRvtDesired.Clear();
            linkInfoListCad.Clear(); linkInfoListCadDesired.Clear();
            linkInfoListDwf.Clear(); linkInfoListDwfDesired.Clear();
            linkInfoListOther.Clear();
            perLinkInfoListRvtDesired.Clear(); perLinkInfoListCadDesired.Clear(); perLinkInfoListDwfDesired.Clear();
            perLoadUnloadListRvtDesired.Clear(); perLoadUnloadListCadDesired.Clear(); perLoadUnloadListDwfDesired.Clear();
        }

        private void LoadProjectData(string filename)
        {
            string projectFilePath = filename;
            string projectFileName = Path.GetFileName(filename);

            ModelPath mp = new FilePath(filename);
            TransmissionData transData = TransmissionData.ReadTransmissionData(mp);
            if (transData == null) return;

            ICollection<ElementId> externalReferences = transData.GetAllExternalFileReferenceIds();

            foreach (ElementId refId in externalReferences)
            {
                ExternalFileReference extRef = transData.GetLastSavedReferenceData(refId);
                ExternalFileReference extRefDesired = transData.GetDesiredReferenceData(refId);

                if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.RevitLink)
                {
                    string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                    string refName = Path.GetFileName(refPath);
                    if (GetExtension.IfcRvtExtension(refName)) continue;
                    string dataKey = extRef.GetReferencingId().ToString();
                    string refStableId = projectFilePath + dataKey;
                    string refLoadState = extRef.GetLinkedFileStatus().ToString();
                    bool refIsLoaded = GetIsLoaded(refLoadState);
                    string refPathAbsolute = GetAbsolutePath.LinksAbsolutePath(refPath, filename);
                    linkInfoListRvt.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                        refLoadState, refIsLoaded, refPath,
                        GetLastSavedDate.LinksLastSavedDate(refPathAbsolute),
                        projectFilePath, projectFileName, refStableId, "Rvt"));
                }

                if (extRefDesired.ExternalFileReferenceType == ExternalFileReferenceType.RevitLink)
                {
                    string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRefDesired.GetPath());
                    string refName = Path.GetFileName(refPath);
                    if (GetExtension.IfcRvtExtension(refName)) continue;
                    string dataKey = extRefDesired.GetReferencingId().ToString();
                    string refStableId = projectFilePath + dataKey;
                    string refLoadState = extRefDesired.GetLinkedFileStatus().ToString();
                    bool refIsLoaded = GetIsLoaded(refLoadState);
                    linkInfoListRvtDesired.Add(new LinkData(dataKey, refName, extRefDesired.PathType.ToString(),
                        refLoadState, refIsLoaded, refPath, "N/A",
                        projectFilePath, projectFileName, refStableId, "Rvt"));
                    perLinkInfoListRvtDesired.Add(new PersistantLinkData(dataKey, refName, refPath, refStableId));
                    perLoadUnloadListRvtDesired.Add(new PersistantLoadUnloadData(dataKey, refLoadState, refIsLoaded, refStableId));
                }

                if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.CADLink)
                {
                    string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                    string refName = Path.GetFileName(refPath);
                    string dataKey = extRef.GetReferencingId().ToString();
                    string refStableId = projectFilePath + dataKey;
                    string refLoadState = extRef.GetLinkedFileStatus().ToString();
                    bool refIsLoaded = GetIsLoaded(refLoadState);
                    string refPathAbsolute = GetAbsolutePath.LinksAbsolutePath(refPath, filename);
                    linkInfoListCad.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                        refLoadState, refIsLoaded, refPath,
                        GetLastSavedDate.LinksLastSavedDate(refPathAbsolute),
                        projectFilePath, projectFileName, refStableId, "Dwg"));
                }

                if (extRefDesired.ExternalFileReferenceType == ExternalFileReferenceType.CADLink)
                {
                    string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRefDesired.GetPath());
                    string refName = Path.GetFileName(refPath);
                    string dataKey = extRefDesired.GetReferencingId().ToString();
                    string refStableId = projectFilePath + dataKey;
                    string refLoadState = extRefDesired.GetLinkedFileStatus().ToString();
                    bool refIsLoaded = GetIsLoaded(refLoadState);
                    linkInfoListCadDesired.Add(new LinkData(dataKey, refName, extRefDesired.PathType.ToString(),
                        refLoadState, refIsLoaded, refPath, "N/A",
                        projectFilePath, projectFileName, refStableId, "Dwg"));
                    perLinkInfoListCadDesired.Add(new PersistantLinkData(dataKey, refName, refPath, refStableId));
                    perLoadUnloadListCadDesired.Add(new PersistantLoadUnloadData(dataKey, refLoadState, refIsLoaded, refStableId));
                }

                if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.DWFMarkup)
                {
                    string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                    string refName = Path.GetFileName(refPath);
                    string dataKey = extRef.GetReferencingId().ToString();
                    string refStableId = projectFilePath + dataKey;
                    string refLoadState = extRef.GetLinkedFileStatus().ToString();
                    bool refIsLoaded = GetIsLoaded(refLoadState);
                    string refPathAbsolute = GetAbsolutePath.LinksAbsolutePath(refPath, filename);
                    linkInfoListDwf.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                        refLoadState, refIsLoaded, refPath,
                        GetLastSavedDate.LinksLastSavedDate(refPathAbsolute),
                        projectFilePath, projectFileName, refStableId, "Dwf"));
                }

                if (extRefDesired.ExternalFileReferenceType == ExternalFileReferenceType.DWFMarkup)
                {
                    string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRefDesired.GetPath());
                    string refName = Path.GetFileName(refPath);
                    string dataKey = extRefDesired.GetReferencingId().ToString();
                    string refStableId = projectFilePath + dataKey;
                    string refLoadState = extRefDesired.GetLinkedFileStatus().ToString();
                    bool refIsLoaded = GetIsLoaded(refLoadState);
                    linkInfoListDwfDesired.Add(new LinkData(dataKey, refName, extRefDesired.PathType.ToString(),
                        refLoadState, refIsLoaded, refPath, "N/A",
                        projectFilePath, projectFileName, refStableId, "Dwf"));
                    perLinkInfoListDwfDesired.Add(new PersistantLinkData(dataKey, refName, refPath, refStableId));
                    perLoadUnloadListDwfDesired.Add(new PersistantLoadUnloadData(dataKey, refLoadState, refIsLoaded, refStableId));
                }

                if (extRef.ExternalFileReferenceType != ExternalFileReferenceType.DWFMarkup &
                    extRef.ExternalFileReferenceType != ExternalFileReferenceType.CADLink &
                    extRef.ExternalFileReferenceType != ExternalFileReferenceType.RevitLink)
                {
                    string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                    string refName = Path.GetFileName(refPath);
                    string dataKey = extRef.GetReferencingId().ToString();
                    string refStableId = projectFilePath + dataKey;
                    string refLoadState = extRef.GetLinkedFileStatus().ToString();
                    bool refIsLoaded = GetIsLoaded(refLoadState);
                    linkInfoListOther.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                        refLoadState, refIsLoaded, refPath, "N/A",
                        projectFilePath, projectFileName, refStableId, "Other"));
                }
            }

            transData.IsTransmitted = false;
        }

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
                string current = filesToProcessDGV.Rows[e.RowIndex].Cells["Process Project"].Value.ToString();
                filesToProcessDGV.Rows[e.RowIndex].Cells["Process Project"].Value =
                    current == "Project will be Processed" ? "Project will be Ignored" : "Project will be Processed";
            }
        }

        private void cBoxFileToBeProcessed_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex == filesToProcessDGV.Columns["Process Project"].Index && e.RowIndex != -1)
                filesToProcessDGV.EndEdit();
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

            ClearAllLinkLists();

            foreach (FolderContentsData f in fileInfoList)
            {
                if (!f.FileToBeProcessed) continue;
                try
                {
                    LoadProjectData(f.FilePathName);
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

            ClearAllLinkLists();

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

        private void btnPrintTextOnly_Click(object sender, EventArgs e)
        {
            string outputfile = Path.Combine(Path.GetTempPath(), "CurrentLinks.txt");
            using (StreamWriter sw = new StreamWriter(outputfile, false))
                sw.Write(BuildLinkReport(prependNote: true));
            Process.Start(outputfile);
        }

        #endregion

        #region Commit Changes

        private void btnMakeChanges_Click(object sender, EventArgs e)
        {
            bool generateTextFile = chkTextPrint.Checked;
            string outputfile = "";

            foreach (FolderContentsData f in fileInfoList)
            {
                if (!f.FileToBeProcessed) continue;

                string filename = f.FilePathName;
                string projectFilePath = filename;

                try
                {
                    ModelPath mpSecond = new FilePath(filename);
                    TransmissionData transData = TransmissionData.ReadTransmissionData(mpSecond);

                    if (transData != null)
                    {
                        ICollection<ElementId> externalReferences = transData.GetAllExternalFileReferenceIds();

                        try
                        {
                            foreach (ElementId refId in externalReferences)
                            {
                                ExternalFileReference extRef = transData.GetLastSavedReferenceData(refId);
                                string dataKey = extRef.GetReferencingId().ToString();
                                string refStableId = projectFilePath + dataKey;

                                SortableBindingList<LinkData> desired = null;
                                if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.RevitLink)
                                    desired = linkInfoListRvtDesired;
                                else if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.CADLink)
                                    desired = linkInfoListCadDesired;
                                else if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.DWFMarkup)
                                    desired = linkInfoListDwfDesired;

                                if (desired == null) continue;

                                foreach (LinkData l in desired)
                                {
                                    if (l.RefStableId == refStableId)
                                    {
                                        bool loadState = l.RefLoadState == LinkConstants.StateLoaded || l.RefLoadState == LinkConstants.StateNotFound;
                                        transData.SetDesiredReferenceData(refId, new FilePath(l.RefPath), extRef.PathType, loadState);
                                    }
                                }
                            }

                            transData.IsTransmitted = true;
                            TransmissionData.WriteTransmissionData(mpSecond, transData);
                        }
                        catch (Exception)
                        {
                            System.Windows.MessageBox.Show("Error - Transmission Failed",
                                LinkConstants.MsgTransmissionFailed + "\n" + filename);
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Error - Link Status", LinkConstants.MsgNoLinks);
                    }
                }
                catch (Exception)
                {
                    System.Windows.MessageBox.Show("Error - Operation Failed", LinkConstants.MsgOperationFailed);
                }
            }

            if (generateTextFile)
            {
                outputfile = Path.Combine(Path.GetTempPath(), "CurrentLinks.txt");
                using (StreamWriter sw = new StreamWriter(outputfile, false))
                    sw.Write(BuildLinkReport(prependNote: false));
            }

            System.Windows.MessageBox.Show("Links Amended", "Changes have been applied and will take effect next time the project is opened");
            if (generateTextFile) Process.Start(outputfile);
        }

        #endregion

        #region Browse Folders

        private void btnBrowseFolderRevitDGV_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderRevit);
        private void btnBrowseFolderCad_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderCad);
        private void btnBrowseFolderDwf_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderDwf);

        #endregion

        #region Checkbox Cell Events

        private void cBoxLinkStateRvtDesired_CellValueChanged(object sender, DataGridViewCellEventArgs e) => HandleCellValueChanged(RvtDesiredDGV, e);
        private void cBoxLinkStateRvtDesired_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e) => HandleCellMouseUp(RvtDesiredDGV, e);

        private void cBoxLinkStateCadDesired_CellValueChanged(object sender, DataGridViewCellEventArgs e) => HandleCellValueChanged(CadDesiredDGV, e);
        private void cBoxLinkStateCadDesired_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e) => HandleCellMouseUp(CadDesiredDGV, e);

        private void cBoxLinkStateDwfDesired_CellValueChanged(object sender, DataGridViewCellEventArgs e) => HandleCellValueChanged(DwfDesiredDGV, e);
        private void cBoxLinkStateDwfDesired_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e) => HandleCellMouseUp(DwfDesiredDGV, e);

        #endregion

        #region Folder Apply Checkboxes

        private void chkRevitFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(linkInfoListRvtDesired, perLinkInfoListRvtDesired, txtBrowseFolderRevit.Text, chkRevitFolder.Checked);

        private void chkCadFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(linkInfoListCadDesired, perLinkInfoListCadDesired, txtBrowseFolderCad.Text, chkCadFolder.Checked);

        private void chkDwfFolder_CheckedChanged(object sender, EventArgs e) =>
            ApplyFolderToList(linkInfoListDwfDesired, perLinkInfoListDwfDesired, txtBrowseFolderDwf.Text, chkDwfFolder.Checked);

        #endregion

        #region Load / Unload All Checkboxes

        private void cBoxBasicChangesRevitUnload_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: false, linkInfoListRvtDesired, perLoadUnloadListRvtDesired, cBoxBasicChangesRevitLoad, cBoxBasicChangesRevitUnload.Checked);

        private void cBoxBasicChangesRevitLoad_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: true, linkInfoListRvtDesired, perLoadUnloadListRvtDesired, cBoxBasicChangesRevitUnload, cBoxBasicChangesRevitLoad.Checked);

        private void cBoxBasicChangesCADUnload_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: false, linkInfoListCadDesired, perLoadUnloadListCadDesired, cBoxBasicChangesCADLoad, cBoxBasicChangesCADUnload.Checked);

        private void cBoxBasicChangesCADLoad_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: true, linkInfoListCadDesired, perLoadUnloadListCadDesired, cBoxBasicChangesCADUnload, cBoxBasicChangesCADLoad.Checked);

        private void cBoxBasicChangesDWFUnload_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: false, linkInfoListDwfDesired, perLoadUnloadListDwfDesired, cBoxBasicChangesDWFLoad, cBoxBasicChangesDWFUnload.Checked);

        private void cBoxBasicChangesDWFLoad_CheckedChanged(object sender, EventArgs e) =>
            HandleLoadUnloadCheckbox(setLoaded: true, linkInfoListDwfDesired, perLoadUnloadListDwfDesired, cBoxBasicChangesDWFUnload, cBoxBasicChangesDWFLoad.Checked);

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
                    System.Windows.MessageBox.Show("Error", "You must create a new excel file. Overwriting is not permitted");
                    return;
                }

                using (ExcelPackage package = new ExcelPackage(new FileInfo(filename)))
                {
                    ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Proposed Link Status");
                    SetupExcelSheetHeaders(sheet, "Proposed Link Data Properties");
                    WriteLinksToSheet(sheet, 3, linkInfoListRvtDesired, linkInfoListCadDesired, linkInfoListDwfDesired, linkInfoListOther);

                    ExcelWorksheet sheetA = package.Workbook.Worksheets.Add("Current Link Status");
                    SetupExcelSheetHeaders(sheetA, "Current Link Data Properties");
                    WriteLinksToSheet(sheetA, 3, linkInfoListRvt, linkInfoListCad, linkInfoListDwf, linkInfoListOther);

                    package.Save();
                }
                Process.Start(filename);
            }
        }

        #endregion

        #region Repath Selected Rows

        private void btnRepathRows_Click(object sender, EventArgs e) => RepathSelectedRows(RvtDesiredDGV);
        private void btnRepathRowsCad_Click(object sender, EventArgs e) => RepathSelectedRows(CadDesiredDGV);
        private void btnRepathRowsDwf_Click(object sender, EventArgs e) => RepathSelectedRows(DwfDesiredDGV);

        #endregion

        #region Reset Paths

        private void btnResetPaths_Click(object sender, EventArgs e) => ResetPaths(linkInfoListRvtDesired, perLinkInfoListRvtDesired);
        private void btnResetPathsCad_Click(object sender, EventArgs e) => ResetPaths(linkInfoListCadDesired, perLinkInfoListCadDesired);
        private void btnResetPathsDwf_Click(object sender, EventArgs e) => ResetPaths(linkInfoListDwfDesired, perLinkInfoListDwfDesired);

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
                System.Windows.MessageBox.Show("Error", "Please select rows from the 'FOLDER CONTENTS' table and try again");
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
                System.Windows.MessageBox.Show("Error", "Please select rows from the 'FOLDER CONTENTS' table and try again");
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
