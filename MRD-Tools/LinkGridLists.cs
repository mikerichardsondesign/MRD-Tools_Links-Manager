using System.Collections.Generic;
using System.Linq;

namespace MRDTools
{
    /// <summary>
    /// The binding lists behind the link manager forms' grids. Each Revit, CAD and DWF link is in two lists, one for
    /// the last saved grid and one for the desired grid. Both hold the same LinkRow, so an edit shows in both while
    /// each grid keeps its own sort order.
    /// </summary>
    internal class LinkGridLists
    {
        public SortableBindingList<LinkRow> RvtSaved { get; } = new SortableBindingList<LinkRow>();
        public SortableBindingList<LinkRow> RvtDesired { get; } = new SortableBindingList<LinkRow>();
        public SortableBindingList<LinkRow> CadSaved { get; } = new SortableBindingList<LinkRow>();
        public SortableBindingList<LinkRow> CadDesired { get; } = new SortableBindingList<LinkRow>();
        public SortableBindingList<LinkRow> DwfSaved { get; } = new SortableBindingList<LinkRow>();
        public SortableBindingList<LinkRow> DwfDesired { get; } = new SortableBindingList<LinkRow>();
        public SortableBindingList<LinkRow> Other { get; } = new SortableBindingList<LinkRow>();

        /// <summary>Every row, in the order they were read.</summary>
        public List<LinkRow> All { get; } = new List<LinkRow>();

        public bool HasChanges => All.Any(r => r.IsDirty);

        /// <summary>Every link as last saved, in the order the upper grids show them.</summary>
        public IEnumerable<LinkRow> SavedInGridOrder => RvtSaved.Concat(CadSaved).Concat(DwfSaved).Concat(Other);

        /// <summary>Revit, CAD and DWF links as desired, in the order the lower grids show them.</summary>
        public IEnumerable<LinkRow> DesiredInGridOrder => RvtDesired.Concat(CadDesired).Concat(DwfDesired);

        /// <summary>Adds a project's rows; null (a project with no links) adds nothing.</summary>
        public void Add(IEnumerable<LinkRow> rows)
        {
            if (rows == null) return;
            foreach (LinkRow row in rows)
            {
                All.Add(row);
                switch (row.Kind)
                {
                    case LinkKind.Revit: RvtSaved.Add(row); RvtDesired.Add(row); break;
                    case LinkKind.Cad: CadSaved.Add(row); CadDesired.Add(row); break;
                    case LinkKind.Dwf: DwfSaved.Add(row); DwfDesired.Add(row); break;
                    default: Other.Add(row); break;
                }
            }
        }

        public void Clear()
        {
            All.Clear();
            RvtSaved.Clear(); RvtDesired.Clear();
            CadSaved.Clear(); CadDesired.Clear();
            DwfSaved.Clear(); DwfDesired.Clear();
            Other.Clear();
        }
    }
}
