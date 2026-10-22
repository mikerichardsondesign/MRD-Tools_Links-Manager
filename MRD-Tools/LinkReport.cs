using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MRDTools
{
    /// <summary>
    /// The plain-text link report: each kind of link as last saved, then as it will be the next time the project opens.
    /// </summary>
    internal static class LinkReport
    {
        /// <summary>Writes the report to CurrentLinks.txt in the temp folder and returns its path.</summary>
        /// <param name="saved">Every link as last saved, in the order to list them.</param>
        /// <param name="desired">Revit, CAD and DWF links as desired, in the order to list them.</param>
        /// <param name="prependNote">Starts with a note that nothing has been written yet.</param>
        /// <param name="includeProjectName">Adds a Project Name column (batch mode).</param>
        public static string WriteTempFile(IEnumerable<LinkRow> saved, IEnumerable<LinkRow> desired,
            bool prependNote, bool includeProjectName)
        {
            string outputFile = Path.Combine(Path.GetTempPath(), "CurrentLinks.txt");
            File.WriteAllText(outputFile, Build(saved, desired, prependNote, includeProjectName));
            return outputFile;
        }

        public static string Build(IEnumerable<LinkRow> saved, IEnumerable<LinkRow> desired,
            bool prependNote, bool includeProjectName)
        {
            var sb = new StringBuilder();
            if (prependNote)
            {
                sb.AppendLine("PLEASE NOTE THAT THIS OUTPUT IS WHAT WILL BE TRUE IF CHANGES ARE COMMITED BY PRESSING THE 'MAKE CHANGES' BUTTON");
                sb.AppendLine("IF CHANGES ARE NOT COMMITTED THEN THE STATUS OF LINKS WILL STAY AS THEY WERE");
                sb.AppendLine();
            }

            const string nextOpen = "CURRENT LINK STATUS WHICH WILL BE APPLIED UPON NEXT OPEN OF PROJECT";
            AppendSection(sb, "REVIT - LAST SAVED LINK STATUS", saved, LinkKind.Revit, proposed: false, includeSavedTime: true, includeProjectName);
            AppendSection(sb, "REVIT - " + nextOpen, desired, LinkKind.Revit, proposed: true, includeSavedTime: false, includeProjectName);
            AppendSection(sb, "CAD - LAST SAVED LINK STATUS", saved, LinkKind.Cad, proposed: false, includeSavedTime: true, includeProjectName);
            AppendSection(sb, "CAD - " + nextOpen, desired, LinkKind.Cad, proposed: true, includeSavedTime: false, includeProjectName);
            AppendSection(sb, "DWF - LAST SAVED LINK STATUS", saved, LinkKind.Dwf, proposed: false, includeSavedTime: true, includeProjectName);
            AppendSection(sb, "DWF - " + nextOpen, desired, LinkKind.Dwf, proposed: true, includeSavedTime: false, includeProjectName);
            AppendSection(sb, "OTHER LINKS - LAST SAVED LINK STATUS", saved, LinkKind.Other, proposed: false, includeSavedTime: false, includeProjectName);
            return sb.ToString();
        }

        private static void AppendSection(StringBuilder sb, string header, IEnumerable<LinkRow> rows, LinkKind kind,
            bool proposed, bool includeSavedTime, bool includeProjectName)
        {
            sb.AppendLine(header);
            if (includeProjectName) sb.Append("PROJECT NAME\t");
            sb.Append("LINK ID\tLINK NAME\tLINK STATE\tLINK PATH");
            if (includeSavedTime) sb.Append("\tDATE");
            sb.AppendLine();

            foreach (LinkRow l in rows.Where(r => r.Kind == kind))
            {
                if (includeProjectName) sb.Append($"{l.ProjectName}\t");
                sb.Append(proposed
                    ? $"{l.RefId}\t{l.DesiredName}\t{l.DesiredState}\t{l.DesiredPath}"
                    : $"{l.RefId}\t{l.LastSavedName}\t{l.LastSavedState}\t{l.LastSavedPath}");
                if (includeSavedTime) sb.Append($"\t{l.LastSavedTime}");
                sb.AppendLine();
            }
            sb.AppendLine();
        }
    }
}
