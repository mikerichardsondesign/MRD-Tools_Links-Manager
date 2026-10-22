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
        /// Writes the changed Revit, CAD and DWF links among the rows that belong to projectPath, and returns how many it wrote.
        /// A project with no changed links isn't touched (0 is returned), so it isn't set to transmitted for nothing.
        /// Throws when the project can't be read or written, for example because it's open.
        /// </summary>
        public static int Write(string projectPath, IEnumerable<LinkRow> rows)
        {
            Dictionary<string, LinkRow> changed = new Dictionary<string, LinkRow>();
            foreach (LinkRow row in rows)
            {
                if (row.IsEditable && row.IsDirty && string.Equals(row.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
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

                transData.SetDesiredReferenceData(refId, new FilePath(row.DesiredPath), saved.PathType, row.DesiredLoaded);
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
