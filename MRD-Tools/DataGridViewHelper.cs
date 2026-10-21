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
        /// Configures a read-only display grid for showing last-saved link data.
        /// </summary>
        /// <param name="dgv">Target DataGridView.</param>
        /// <param name="cellGray">Cell template for the Id column.</param>
        /// <param name="includeSavedTime">When true, adds the Last Saved column.</param>
        /// <param name="dataSource">Binding list to attach.</param>
        /// <param name="includeProjectName">When true, adds a Project Name column first (batch form).</param>
        /// <param name="linkPathWidth">Width for the Link Path column (defaults to 325; use 450 for Other).</param>
        public static void ConfigureReadOnlyGrid(DataGridView dgv, DataGridViewCell cellGray,
            bool includeSavedTime, object dataSource,
            bool includeProjectName = false, int linkPathWidth = 325)
        {
            ApplyStandardProperties(dgv);

            if (includeProjectName)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Project Name", DataPropertyName = "FileName", HeaderText = "Project Name", Width = 210, ReadOnly = true });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { CellTemplate = cellGray, DataPropertyName = "RefId", HeaderText = "Link Id", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Name", DataPropertyName = "RefName", HeaderText = "Link Name", Width = 210, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RefPathType", HeaderText = "Path Type", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link State", DataPropertyName = "RefLoadState", HeaderText = "Link State", Width = 210, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Path", DataPropertyName = "RefPath", HeaderText = "Link Path", Width = linkPathWidth, ReadOnly = true });

            if (includeSavedTime)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Last Saved", DataPropertyName = "RefSavedTime", HeaderText = "Last Saved", Width = 125, ReadOnly = true });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Empty", HeaderText = "", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });

            dgv.DataSource = dataSource;
        }

        /// <summary>
        /// Configures an editable "desired state" grid with checkbox and browse button columns.
        /// </summary>
        /// <param name="dgv">Target DataGridView.</param>
        /// <param name="cellGray">Cell template for the Id column.</param>
        /// <param name="dataSource">Binding list to attach.</param>
        /// <param name="cellClickHandler">Handler for the browse button cell click.</param>
        /// <param name="cellValueChangedHandler">Handler for checkbox value changes.</param>
        /// <param name="cellMouseUpHandler">Handler for checkbox mouse up (commit edit).</param>
        /// <param name="includeProjectName">When true, adds a Project Name column first (batch form).</param>
        public static void ConfigureDesiredGrid(DataGridView dgv, DataGridViewCell cellGray,
            object dataSource,
            DataGridViewCellEventHandler cellClickHandler,
            DataGridViewCellEventHandler cellValueChangedHandler,
            DataGridViewCellMouseEventHandler cellMouseUpHandler,
            bool includeProjectName = false)
        {
            ApplyStandardProperties(dgv);

            if (includeProjectName)
                dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Project Name", DataPropertyName = "FileName", HeaderText = "Project Name", Width = 210, ReadOnly = true });

            dgv.Columns.Add(new DataGridViewTextBoxColumn { CellTemplate = cellGray, DataPropertyName = "RefId", HeaderText = "Link Id", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Name", DataPropertyName = "RefName", HeaderText = "Link Name", Width = 210, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RefPathType", HeaderText = "Path Type", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link State", DataPropertyName = "RefLoadState", HeaderText = "Link State", Width = 110, ReadOnly = true });
            dgv.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Link State CBox", DataPropertyName = "RefIsLoaded", HeaderText = "Is Loaded", Width = 100, TrueValue = true, FalseValue = false });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Link Path", DataPropertyName = "RefPath", HeaderText = "Link Path", Width = 325 });
            dgv.Columns.Add(new DataGridViewButtonColumn { Name = "Browse Button", HeaderText = "Repath", Width = 125, Text = "Browse", UseColumnTextForButtonValue = true });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Empty", HeaderText = "", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });

            dgv.CellClick += cellClickHandler;
            dgv.CellValueChanged += cellValueChangedHandler;
            dgv.CellMouseUp += cellMouseUpHandler;

            dgv.DataSource = dataSource;

            foreach (DataGridViewRow row in dgv.Rows)
                row.Height = 20;
        }
    }
}
