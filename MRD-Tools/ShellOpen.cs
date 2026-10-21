using System;
using System.Diagnostics;

namespace MRDTools
{
    /// <summary>
    /// Opens a saved report or spreadsheet in its default application.
    /// </summary>
    internal static class ShellOpen
    {
        /// <remarks>
        /// Process.Start(path) does this on .NET Framework, but on .NET 8+ (Revit 2025 onwards) UseShellExecute
        /// defaults to false and it throws instead.
        /// </remarks>
        public static void Open(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("The file was saved but could not be opened:\n" + path + "\n\n" + ex.Message, "Open File");
            }
        }
    }
}
