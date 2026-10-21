using System;
using System.Collections.Generic;
using System.IO;

namespace MRDTools
{
    public class GetRelativePath
    {
        /// <summary>
        /// Path to the link relative to the folder holding the host project, e.g. "Links\A.rvt" or "..\Other\A.rvt".
        /// When the two are on different drives or shares there is no relative path, so the absolute path is returned.
        /// </summary>
        /// <param name="linkFilePath">Absolute path of the linked file.</param>
        /// <param name="masterFilePath">Absolute path of the host project (.rvt).</param>
        public static string GetRelativePathUriA(string linkFilePath, string masterFilePath)
        {
            string linkPath = Path.GetFullPath(linkFilePath);
            string hostFolder = Path.GetDirectoryName(Path.GetFullPath(masterFilePath));

            if (!string.Equals(Path.GetPathRoot(linkPath), Path.GetPathRoot(hostFolder), StringComparison.OrdinalIgnoreCase))
                return linkPath;

            string[] fromParts = hostFolder.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
            string[] toParts = linkPath.Split(Path.DirectorySeparatorChar);

            // Count the leading folders both paths share (the link's file name is never one of them)
            int common = 0;
            while (common < fromParts.Length && common < toParts.Length - 1
                && string.Equals(fromParts[common], toParts[common], StringComparison.OrdinalIgnoreCase))
                common++;

            List<string> relative = new List<string>();
            for (int i = common; i < fromParts.Length; i++)
                relative.Add("..");
            for (int i = common; i < toParts.Length; i++)
                relative.Add(toParts[i]);

            return string.Join(Path.DirectorySeparatorChar.ToString(), relative);
        }
    }
}
