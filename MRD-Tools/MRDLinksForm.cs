using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;


namespace MRDTools
{
    /// <summary>
    /// Link manager main form — single project mode.
    /// </summary>
    public partial class MRDLinksForm : System.Windows.Forms.Form
    {
        #region Fields

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

        public MRDLinksForm(ExternalCommandData commandData)
        {
            InitializeComponent();
            Icon = FormIcon.Load();

            btnBrowseFolderRevitDGV.Click += new EventHandler(btnBrowseFolderRevitDGV_Click);
            btnBrowseFolderDwf.Click += new EventHandler(btnBrowseFolderDwf_Click);
            chkRevitFolder.CheckedChanged += new EventHandler(chkRevitFolder_CheckedChanged);
            chkCadFolder.CheckedChanged += new EventHandler(chkCadFolder_CheckedChanged);
            chkDwfFolder.CheckedChanged += new EventHandler(chkDwfFolder_CheckedChanged);

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

            DataGridViewCell cellGray = new DataGridViewTextBoxCell();
            cellGray.Style.BackColor = System.Drawing.Color.GhostWhite;

            // Read-only last-saved grids
            DataGridViewHelper.ConfigureReadOnlyGrid(RvtDGV, cellGray, includeSavedTime: true, dataSource: linkInfoListRvt);
            DataGridViewHelper.ConfigureReadOnlyGrid(CadDGV, cellGray, includeSavedTime: true, dataSource: linkInfoListCad);
            DataGridViewHelper.ConfigureReadOnlyGrid(DwfDGV, cellGray, includeSavedTime: true, dataSource: linkInfoListDwf);
            DataGridViewHelper.ConfigureReadOnlyGrid(OtherDGV, cellGray, includeSavedTime: false, dataSource: linkInfoListOther, linkPathWidth: 450);

            // Editable desired-state grids
            DataGridViewHelper.ConfigureDesiredGrid(RvtDesiredDGV, cellGray, linkInfoListRvtDesired,
                BtnFileBrowseRvtDesired_CellClick, cBoxLinkStateRvtDesired_CellValueChanged, cBoxLinkStateRvtDesired_CellMouseUp);
            DataGridViewHelper.ConfigureDesiredGrid(CadDesiredDGV, cellGray, linkInfoListCadDesired,
                BtnFileBrowseCadDesired_CellClick, cBoxLinkStateCadDesired_CellValueChanged, cBoxLinkStateCadDesired_CellMouseUp);
            DataGridViewHelper.ConfigureDesiredGrid(DwfDesiredDGV, cellGray, linkInfoListDwfDesired,
                BtnFileBrowseDwfDesired_CellClick, cBoxLinkStateDwfDesired_CellValueChanged, cBoxLinkStateDwfDesired_CellMouseUp);
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
                System.Windows.MessageBox.Show(LinkConstants.MsgSelectRows, "Error");
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

        private void ApplyLinkChanges(TransmissionData transData, ElementId refId, ExternalFileReference extRef,
            SortableBindingList<LinkData> desired, ExternalFileReferenceType type)
        {
            if (extRef.ExternalFileReferenceType != type) return;

            string dataKey = extRef.GetReferencingId().ToString();
            foreach (LinkData l in desired)
            {
                if (l.RefId == dataKey)
                {
                    bool loadState = l.RefLoadState == LinkConstants.StateLoaded || l.RefLoadState == LinkConstants.StateNotFound;
                    transData.SetDesiredReferenceData(refId, new FilePath(l.RefPath), extRef.PathType, loadState);
                }
            }
        }

        private bool GetIsLoaded(string loadState)
        {
            return loadState == LinkConstants.StateNotFound || loadState == LinkConstants.StateLoaded;
        }

        private void AppendLinkSection(StringBuilder sb, string header, SortableBindingList<LinkData> list, bool includeSavedTime, bool includeFileName = false)
        {
            sb.AppendLine(header);
            sb.Append(includeFileName ? "LINK ID\tLINK NAME\tLINK STATE\tLINK PATH" : "LINK ID\tLINK NAME\tLINK STATE\tLINK PATH");
            if (includeSavedTime) sb.Append("\tDATE");
            sb.AppendLine();

            foreach (LinkData l in list)
            {
                sb.Append($"{l.RefId}\t{l.RefName}\t{l.RefLoadState}\t{l.RefPath}");
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

        #endregion

        #region Browse Folders

        private void btnBrowseFolderRevitDGV_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderRevit);
        private void btnBrowseFolderCad_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderCad);
        private void btnBrowseFolderDwf_Click(object sender, EventArgs e) => HandleFolderBrowseClick(txtBrowseFolderDwf);

        #endregion

        #region Commit Changes

        private void btnMakeChanges_Click(object sender, EventArgs e)
        {
            bool generateTextFile = chkTextPrint.Checked;
            string outputfile = "";

            try
            {
                string filename = this.txtBrowseProject.Text;
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
                            ApplyLinkChanges(transData, refId, extRef, linkInfoListRvtDesired, ExternalFileReferenceType.RevitLink);
                            ApplyLinkChanges(transData, refId, extRef, linkInfoListCadDesired, ExternalFileReferenceType.CADLink);
                            ApplyLinkChanges(transData, refId, extRef, linkInfoListDwfDesired, ExternalFileReferenceType.DWFMarkup);
                        }

                        transData.IsTransmitted = true;
                        TransmissionData.WriteTransmissionData(mpSecond, transData);

                        if (generateTextFile)
                        {
                            outputfile = Path.Combine(Path.GetTempPath(), "CurrentLinks.txt");
                            using (StreamWriter sw = new StreamWriter(outputfile, false))
                                sw.Write(BuildLinkReport(prependNote: false));
                        }

                        System.Windows.MessageBox.Show("Changes have been applied and will take effect next time the project is opened", "Links Amended");
                        if (generateTextFile) ShellOpen.Open(outputfile);
                    }
                    catch (Exception)
                    {
                        System.Windows.MessageBox.Show(LinkConstants.MsgTransmissionFailed, "Error - Transmission Failed");
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show(LinkConstants.MsgNoLinks, "Error - Link Status");
                }
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show(LinkConstants.MsgOperationFailed, "Error - Operation Failed");
            }
        }

        #endregion

        #region Print to Text

        private void btnPrintTextOnly_Click(object sender, EventArgs e)
        {
            string outputfile = Path.Combine(Path.GetTempPath(), "CurrentLinks.txt");
            using (StreamWriter sw = new StreamWriter(outputfile, false))
                sw.Write(BuildLinkReport(prependNote: true));
            ShellOpen.Open(outputfile);
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
                    string projectFilePath = filename;
                    string projectFileName = Path.GetFileName(filename);
                    currentOpenDirectory = Path.GetDirectoryName(filename);
                    this.txtBrowseProject.Text = filename;

                    // Clear all lists
                    linkInfoListRvt.Clear(); linkInfoListRvtDesired.Clear();
                    linkInfoListCad.Clear(); linkInfoListCadDesired.Clear();
                    linkInfoListDwf.Clear(); linkInfoListDwfDesired.Clear();
                    linkInfoListOther.Clear();
                    perLinkInfoListRvtDesired.Clear(); perLinkInfoListCadDesired.Clear(); perLinkInfoListDwfDesired.Clear();
                    perLoadUnloadListRvtDesired.Clear(); perLoadUnloadListCadDesired.Clear(); perLoadUnloadListDwfDesired.Clear();

                    ModelPath mp = new FilePath(filename);
                    TransmissionData transData = TransmissionData.ReadTransmissionData(mp);

                    if (transData != null)
                    {
                        ICollection<ElementId> externalReferences = transData.GetAllExternalFileReferenceIds();

                        foreach (ElementId refId in externalReferences)
                        {
                            ExternalFileReference extRef = transData.GetLastSavedReferenceData(refId);
                            ExternalFileReference extRefDesired = transData.GetDesiredReferenceData(refId);

                            #region Sort Revit Data

                            if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.RevitLink)
                            {
                                string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                                string refLoadState = extRef.GetLinkedFileStatus().ToString();
                                string refName = Path.GetFileName(refPath);
                                bool checkIfcExt = GetExtension.IfcRvtExtension(refName);

                                if (!checkIfcExt)
                                {
                                    string dataKey = extRef.GetReferencingId().ToString();
                                    string refPathAbsolute = GetAbsolutePath.LinksAbsolutePath(refPath, filename);
                                    string refStableId = projectFileName + dataKey;
                                    bool refIsLoaded = GetIsLoaded(refLoadState);

                                    linkInfoListRvt.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                                        refLoadState, refIsLoaded, refPath,
                                        GetLastSavedDate.LinksLastSavedDate(refPathAbsolute),
                                        projectFilePath, projectFileName, refStableId, "Rvt"));
                                }
                            }

                            if (extRefDesired.ExternalFileReferenceType == ExternalFileReferenceType.RevitLink)
                            {
                                string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRefDesired.GetPath());
                                string refLoadState = extRefDesired.GetLinkedFileStatus().ToString();
                                string refName = Path.GetFileName(refPath);
                                bool checkIfcExt = GetExtension.IfcRvtExtension(refName);

                                if (!checkIfcExt)
                                {
                                    string dataKey = extRefDesired.GetReferencingId().ToString();
                                    string refStableId = projectFileName + dataKey;
                                    bool refIsLoaded = GetIsLoaded(refLoadState);

                                    linkInfoListRvtDesired.Add(new LinkData(dataKey, refName, extRefDesired.PathType.ToString(),
                                        refLoadState, refIsLoaded, refPath, "N/A",
                                        projectFilePath, projectFileName, refStableId, "Rvt"));
                                    perLinkInfoListRvtDesired.Add(new PersistantLinkData(dataKey, refName, refPath, refStableId));
                                    perLoadUnloadListRvtDesired.Add(new PersistantLoadUnloadData(dataKey, refLoadState, refIsLoaded, refStableId));
                                }
                            }

                            #endregion

                            #region Sort Cad Data

                            if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.CADLink)
                            {
                                string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                                string refLoadState = extRef.GetLinkedFileStatus().ToString();
                                string refName = Path.GetFileName(refPath);
                                string dataKey = extRef.GetReferencingId().ToString();
                                string refPathAbsolute = GetAbsolutePath.LinksAbsolutePath(refPath, filename);
                                string refStableId = projectFileName + dataKey;
                                bool refIsLoaded = GetIsLoaded(refLoadState);

                                linkInfoListCad.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                                    refLoadState, refIsLoaded, refPath,
                                    GetLastSavedDate.LinksLastSavedDate(refPathAbsolute),
                                    projectFilePath, projectFileName, refStableId, "Dwg"));
                            }

                            if (extRefDesired.ExternalFileReferenceType == ExternalFileReferenceType.CADLink)
                            {
                                string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRefDesired.GetPath());
                                string refLoadState = extRefDesired.GetLinkedFileStatus().ToString();
                                string refName = Path.GetFileName(refPath);
                                string dataKey = extRefDesired.GetReferencingId().ToString();
                                string refStableId = projectFileName + dataKey;
                                bool refIsLoaded = GetIsLoaded(refLoadState);

                                linkInfoListCadDesired.Add(new LinkData(dataKey, refName, extRefDesired.PathType.ToString(),
                                    refLoadState, refIsLoaded, refPath, "N/A",
                                    projectFilePath, projectFileName, refStableId, "Dwg"));
                                perLinkInfoListCadDesired.Add(new PersistantLinkData(dataKey, refName, refPath, refStableId));
                                perLoadUnloadListCadDesired.Add(new PersistantLoadUnloadData(dataKey, refLoadState, refIsLoaded, refStableId));
                            }

                            #endregion

                            #region Sort Dwf Data

                            if (extRef.ExternalFileReferenceType == ExternalFileReferenceType.DWFMarkup)
                            {
                                string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                                string refLoadState = extRef.GetLinkedFileStatus().ToString();
                                string refName = Path.GetFileName(refPath);
                                string dataKey = extRef.GetReferencingId().ToString();
                                string refPathAbsolute = GetAbsolutePath.LinksAbsolutePath(refPath, filename);
                                string refStableId = projectFileName + dataKey;
                                bool refIsLoaded = GetIsLoaded(refLoadState);

                                linkInfoListDwf.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                                    refLoadState, refIsLoaded, refPath,
                                    GetLastSavedDate.LinksLastSavedDate(refPathAbsolute),
                                    projectFilePath, projectFileName, refStableId, "Dwf"));
                            }

                            if (extRefDesired.ExternalFileReferenceType == ExternalFileReferenceType.DWFMarkup)
                            {
                                string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRefDesired.GetPath());
                                string refLoadState = extRefDesired.GetLinkedFileStatus().ToString();
                                string refName = Path.GetFileName(refPath);
                                string dataKey = extRefDesired.GetReferencingId().ToString();
                                string refStableId = projectFileName + dataKey;
                                bool refIsLoaded = GetIsLoaded(refLoadState);

                                linkInfoListDwfDesired.Add(new LinkData(dataKey, refName, extRefDesired.PathType.ToString(),
                                    refLoadState, refIsLoaded, refPath, "N/A",
                                    projectFilePath, projectFileName, refStableId, "Dwf"));
                                perLinkInfoListDwfDesired.Add(new PersistantLinkData(dataKey, refName, refPath, refStableId));
                                perLoadUnloadListDwfDesired.Add(new PersistantLoadUnloadData(dataKey, refLoadState, refIsLoaded, refStableId));
                            }

                            #endregion

                            #region Sort Other Data

                            if (extRef.ExternalFileReferenceType != ExternalFileReferenceType.DWFMarkup &
                                extRef.ExternalFileReferenceType != ExternalFileReferenceType.CADLink &
                                extRef.ExternalFileReferenceType != ExternalFileReferenceType.RevitLink)
                            {
                                string refPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(extRef.GetPath());
                                string refLoadState = extRef.GetLinkedFileStatus().ToString();
                                string refName = Path.GetFileName(refPath);
                                string dataKey = extRef.GetReferencingId().ToString();
                                string refStableId = projectFileName + dataKey;
                                bool refIsLoaded = GetIsLoaded(refLoadState);

                                linkInfoListOther.Add(new LinkData(dataKey, refName, extRef.PathType.ToString(),
                                    refLoadState, refIsLoaded, refPath, "N/A",
                                    projectFilePath, projectFileName, refStableId, "Other"));
                            }

                            #endregion
                        }

                        transData.IsTransmitted = false;
                    }

                    // Enable controls after successful load
                    btnExportExcel.Enabled = true;
                    btnRepathRowsRevit.Enabled = btnRepathRowsCad.Enabled = btnRepathRowsDwf.Enabled = true;
                    btnUnloadSelectedRowsRevit.Enabled = btnUnloadSelectedRowsCad.Enabled = btnUnloadSelectedRowsDwf.Enabled = true;
                    btnLoadSelectedRowsRevit.Enabled = btnLoadSelectedRowsCad.Enabled = btnLoadSelectedRowsDwf.Enabled = true;
                    btnResetPathsRevit.Enabled = btnResetPathsCad.Enabled = btnResetPathsDwf.Enabled = true;
                    btnBrowseFolderRevitDGV.Enabled = chkRevitFolder.Enabled = txtBrowseFolderRevit.Enabled = true;
                    btnBrowseFolderCad.Enabled = chkCadFolder.Enabled = txtBrowseFolderCad.Enabled = true;
                    btnBrowseFolderDwf.Enabled = chkDwfFolder.Enabled = txtBrowseFolderDwf.Enabled = true;
                    cBoxAbsolutePaths.Enabled = btnMakeChanges.Enabled = chkTextPrint.Enabled = btnPrintTextOnly.Enabled = true;
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
                    System.Windows.MessageBox.Show("You must create a new excel file. Overwriting is not permitted", "Error");
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

        private void btnResetPathsRevit_Click(object sender, EventArgs e) => ResetPaths(linkInfoListRvtDesired, perLinkInfoListRvtDesired);
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

        #region Repath Option Checkbox

        private void cBoxAbsolutePaths_CheckedChanged(object sender, EventArgs e)
        {
            // Referenced from elsewhere; intentionally empty.
        }

        #endregion

    }
}
