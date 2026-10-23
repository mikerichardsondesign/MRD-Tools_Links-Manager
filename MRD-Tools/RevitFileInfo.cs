using Autodesk.Revit.DB;
using System;

namespace MRDTools
{
    /// <summary>
    /// Reads a Revit file's header with Revit's BasicFileInfo, without opening the file. It's Revit's API, so it only works
    /// on Revit's own thread.
    /// </summary>
    internal static class RevitFileInfo
    {
        public static LinkFileInfo Read(string path)
        {
            try
            {
                using (BasicFileInfo info = BasicFileInfo.Extract(path))
                {
                    string format = string.IsNullOrEmpty(info.Format) ? "older" : info.Format;
                    return new LinkFileInfo(format, info.IsWorkshared, info.IsCentral, info.IsLocal,
                        info.IsSavedInCurrentVersion, info.IsSavedInLaterVersion);
                }
            }
            catch (Exception ex)
            {
                return LinkFileInfo.Unreadable(ex.Message);
            }
        }
    }
}
