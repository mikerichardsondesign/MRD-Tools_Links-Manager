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
        /// Revit links made from IFC files (name.ifc.rvt), and Revit Server and cloud models, are listed but can't be changed
        /// (see LinkRow.ReadOnlyReason).
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

                // A Relative link can come back with its full path (CAD links do): show it, and write it, relative to the project
                if (saved.PathType == PathType.Relative) savedPath = LinkRow.RelativeToProject(savedPath, projectPath);
                if (desired.PathType == PathType.Relative) desiredPath = LinkRow.RelativeToProject(desiredPath, projectPath);

                string savedPathType = PathTypeOf(saved);
                string savedTime = kind == LinkKind.Other || LinkRow.IsRemote(savedPath, savedPathType) ? "N/A"
                    : GetLastSavedDate.LinksLastSavedDate(GetAbsolutePath.LinksAbsolutePath(savedPath, projectPath));

                rows.Add(new LinkRow(projectPath, saved.GetReferencingId().ToString(), kind,
                    savedPath, savedPathType, saved.GetLinkedFileStatus().ToString(), savedTime,
                    desiredPath, PathTypeOf(desired), desired.GetLinkedFileStatus().ToString()));
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

        /// <summary>Relative, Absolute or Server, as Revit has it, or Cloud for a model on BIM 360 or Autodesk Docs.</summary>
        private static string PathTypeOf(ExternalFileReference reference) =>
            reference.GetPath().CloudPath ? LinkConstants.PathTypeCloud : reference.PathType.ToString();
    }
}
