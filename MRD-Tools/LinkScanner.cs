using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace MRDTools
{
    /// <summary>
    /// Reads the links of a closed project from its transmission data, without opening it in Revit.
    /// </summary>
    internal static class LinkScanner
    {
        /// <summary>
        /// One row per external reference, or null when the project has no transmission data (no links).
        /// Revit links made from IFC files (name.ifc.rvt) are left out, because Revit manages those through the IFC link.
        /// </summary>
        public static List<LinkRow> Scan(string projectPath)
        {
            TransmissionData transData = TransmissionData.ReadTransmissionData(new FilePath(projectPath));
            if (transData == null) return null;

            List<LinkRow> rows = new List<LinkRow>();
            foreach (ElementId refId in transData.GetAllExternalFileReferenceIds())
            {
                ExternalFileReference saved = transData.GetLastSavedReferenceData(refId);
                LinkKind kind = KindOf(saved.ExternalFileReferenceType);

                // Other references can't be edited, so they're shown as last saved
                ExternalFileReference desired = kind == LinkKind.Other ? saved : transData.GetDesiredReferenceData(refId) ?? saved;

                string savedPath = UserVisiblePath(saved);
                string desiredPath = UserVisiblePath(desired);
                if (kind == LinkKind.Revit && (GetExtension.IfcRvtExtension(LinkRow.FileNameOf(savedPath))
                    || GetExtension.IfcRvtExtension(LinkRow.FileNameOf(desiredPath))))
                    continue;

                string savedTime = kind == LinkKind.Other ? "N/A"
                    : GetLastSavedDate.LinksLastSavedDate(GetAbsolutePath.LinksAbsolutePath(savedPath, projectPath));

                rows.Add(new LinkRow(projectPath, saved.GetReferencingId().ToString(), kind,
                    savedPath, saved.PathType.ToString(), saved.GetLinkedFileStatus().ToString(), savedTime,
                    desiredPath, desired.PathType.ToString(), desired.GetLinkedFileStatus().ToString()));
            }
            return rows;
        }

        internal static LinkKind KindOf(ExternalFileReferenceType type)
        {
            if (type == ExternalFileReferenceType.RevitLink) return LinkKind.Revit;
            if (type == ExternalFileReferenceType.CADLink) return LinkKind.Cad;
            if (type == ExternalFileReferenceType.DWFMarkup) return LinkKind.Dwf;
            return LinkKind.Other;
        }

        private static string UserVisiblePath(ExternalFileReference reference) =>
            ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetPath());
    }
}
