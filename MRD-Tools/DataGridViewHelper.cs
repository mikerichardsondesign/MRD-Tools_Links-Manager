using System.Windows.Forms;

namespace MRDTools
{
    /// <summary>
    /// Shared DataGridView configuration methods used by both link manager forms.
    /// </summary>
    internal static class DataGridViewHelper
    {
        private static void ApplyStandardProperties(DataGridView dgv)
        {
            dgv.Controls.Clear();
            dgv.Columns.Clear();
            dgv.Dock = DockStyle.Fill;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv.AllowUserToResizeRows = false;
            dgv.AutoGenerateColumns = false;
        }

        /// <summary>
        /// Configures a read-only display grid for showing last-saved link data (LinkRow.LastSaved*).
        /// </summary>
        /// <param name="dgv">Target DataGridView.</param>
        /// <param name="cellGray">Cell template for the Id column.</param>
        /// <param name="includeSavedTime">When true, adds the Last Saved column.</param>
        /// <param name="dataSource">Binding list of LinkRow to attach.</param>
        /// <param name="includeProjectName">When true, adds a Project Name column first (batch form).</param>
        /// <param name="linkPathWidth">Width for the Link Path column (defaults to 325; use 450 for Other).</param>
        public static void ConfigureReadOnlyGrid(DataGridView dgv, DataGridViewCell cellGray,
            bool includeSavedTime, object dataSource,
            bool includeProjectName = false, int linkPathWidth = 325)
        {
            ApplyStandardProperties(dgv);

            if (includeProjectName)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Project Name", DataPropertyName = "ProjectName", HeaderText = "Project Name", Width = 210, ReadOnly = true });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { CellTemplate = cellGray, DataPropertyName = "RefId", HeaderText = "Link Id", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Name", DataPropertyName = "LastSavedName", HeaderText = "Link Name", Width = 210, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LastSavedPathType", HeaderText = "Path Type", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link State", DataPropertyName = "LastSavedState", HeaderText = "Link State", Width = 210, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Path", DataPropertyName = "LastSavedPath", HeaderText = "Link Path", Width = linkPathWidth, ReadOnly = true });

            if (includeSavedTime)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Last Saved", DataPropertyName = "LastSavedTime", HeaderText = "Last Saved", Width = 125, ReadOnly = true });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Empty", HeaderText = "", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });

            dgv.DataSource = dataSource;
        }

        /// <summary>
        /// Configures an editable "desired state" grid (LinkRow.Desired*) with checkbox and browse button columns.
        /// </summary>
        /// <param name="dgv">Target DataGridView.</param>
        /// <param name="cellGray">Cell template for the Id column.</param>
        /// <param name="dataSource">Binding list of LinkRow to attach.</param>
        /// <param name="cellClickHandler">Handler for the browse button cell click.</param>
        /// <param name="includeProjectName">When true, adds a Project Name column first (batch form).</param>
        public static void ConfigureDesiredGrid(DataGridView dgv, DataGridViewCell cellGray,
            object dataSource,
            DataGridViewCellEventHandler cellClickHandler,
            bool includeProjectName = false)
        {
            ApplyStandardProperties(dgv);

            if (includeProjectName)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Project Name", DataPropertyName = "ProjectName", HeaderText = "Project Name", Width = 210, ReadOnly = true });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { CellTemplate = cellGray, DataPropertyName = "RefId", HeaderText = "Link Id", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Name", DataPropertyName = "DesiredName", HeaderText = "Link Name", Width = 210, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DesiredPathType", HeaderText = "Path Type", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link State", DataPropertyName = "DesiredState", HeaderText = "Link State", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Link State CBox", DataPropertyName = "DesiredLoaded", HeaderText = "Is Loaded", Width = 100, TrueValue = true, FalseValue = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Path", DataPropertyName = "DesiredPath", HeaderText = "Link Path", Width = 325 });
            dgv.Columns.Add(new DataGridViewButtonColumn { Name = "Browse Button", HeaderText = "Repath", Width = 125, Text = "Browse", UseColumnTextForButtonValue = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Empty", HeaderText = "", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });

            dgv.CellClick += cellClickHandler;
            CommitCheckBoxEditsImmediately(dgv);

            dgv.DataSource = dataSource;

            foreach (DataGridViewRow row in dgv.Rows)
                row.Height = 20;
        }

        /// <summary>
        /// Commits a checkbox click straight away, rather than when the user leaves the cell, so the row
        /// (and any text that follows the checkbox) updates as soon as it's clicked.
        /// </summary>
        public static void CommitCheckBoxEditsImmediately(DataGridView dgv)
        {
            dgv.CurrentCellDirtyStateChanged += (sender, e) =>
            {
                if (dgv.IsCurrentCellDirty && dgv.CurrentCell is DataGridViewCheckBoxCell)
                    dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }
    }
}
