using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace MRDTools
{
    /// <summary>
    /// Writes edited links back into a closed project's transmission data. Revit applies them the next time the project opens.
    /// </summary>
    internal static class LinkWriter
    {
        /// <summary>
        /// Writes the Revit, CAD and DWF links among the rows that belong to projectPath, and returns how many it wrote.
        /// Only changed links are written, unless writeAll is set. With nothing to write the project isn't touched
        /// (0 is returned), so it isn't set to transmitted for nothing.
        /// writeAll is for a project that will be moved along with its linked files: the links that aren't written keep
        /// their last saved data, and relative links among them may not be found from the new folder.
        /// Throws when the project can't be read or written, for example because it's open.
        /// </summary>
        public static int Write(string projectPath, IEnumerable<LinkRow> rows, bool writeAll)
        {
            Dictionary<string, LinkRow> changed = new Dictionary<string, LinkRow>();
            foreach (LinkRow row in rows)
            {
                if (row.ShouldWrite(writeAll) && string.Equals(row.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
                    changed[Key(row.Kind, row.RefId)] = row;
            }
            if (changed.Count == 0) return 0;

            ModelPath modelPath = new FilePath(projectPath);
            TransmissionData transData = TransmissionData.ReadTransmissionData(modelPath);
            if (transData == null) throw new InvalidOperationException(LinkConstants.MsgNoLinks);

            List<LinkRow> written = new List<LinkRow>();
            foreach (ElementId refId in transData.GetAllExternalFileReferenceIds())
            {
                ExternalFileReference saved = transData.GetLastSavedReferenceData(refId);
                if (!changed.TryGetValue(Key(LinkScanner.KindOf(saved.ExternalFileReferenceType), saved.GetReferencingId().ToString()), out LinkRow row))
                    continue;

                if (string.IsNullOrWhiteSpace(row.DesiredPath))
                    throw new InvalidOperationException($"Link {row.RefId} has no path");

                // Revit takes the path type as describing the path it's given, so it comes from the row rather than from the
                // last save: a Relative link given a full path is written as Absolute (see LinkRow.DesiredPathType)
                PathType pathType = Enum.TryParse(row.DesiredPathType, out PathType rowPathType) ? rowPathType : saved.PathType;
                transData.SetDesiredReferenceData(refId, new FilePath(row.DesiredPath), pathType, row.DesiredLoaded);
                written.Add(row);
            }
            if (written.Count == 0) return 0;

            transData.IsTransmitted = true;
            TransmissionData.WriteTransmissionData(modelPath, transData);

            foreach (LinkRow row in written)
                row.AcceptChanges();
            return written.Count;
        }

        private static string Key(LinkKind kind, string refId) => kind + "|" + refId;
    }
}
