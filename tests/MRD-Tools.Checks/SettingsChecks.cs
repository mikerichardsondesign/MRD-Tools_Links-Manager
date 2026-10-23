using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using static MRDTools.Checks.Checker;

namespace MRDTools.Checks
{
    /// <summary>
    /// The folders the dialogs remember between sessions, and the real Windows folder picker, with its buttons pressed
    /// from another thread. The picker shows for a moment, so this needs a desktop.
    /// </summary>
    static class SettingsChecks
    {
        public static void Run(string outDir)
        {
            string settingsDir = FolderMemoryChecks(outDir);
            FolderPickerChecks(settingsDir);
        }

        /// <summary>Kept between sessions, and never in the way. Returns the folder it used.</summary>
        static string FolderMemoryChecks(string outDir)
        {
            string settingsDir = Path.Combine(outDir, "settings-test");
            if (Directory.Exists(settingsDir)) Directory.Delete(settingsDir, true);
            string settingsFile = Path.Combine(settingsDir, "Sub", "Settings.txt");

            FolderMemory fresh = FolderMemory.Load(settingsFile);
            Check(fresh.ProjectsFolder == null && fresh.RepathFolder == null && fresh.ExportFolder == null, "nothing remembered the first time");
            fresh.ProjectsFolder = @"C:\Projects\Riverside";
            fresh.RepathFolder = @"\\server\share\Links = Rev C";
            fresh.ExportFolder = @"C:\Reports";
            Check(fresh.Save(), "the folders are saved, creating the settings folder");
            FolderMemory again = FolderMemory.Load(settingsFile);
            Eq(@"C:\Projects\Riverside", again.ProjectsFolder, "projects folder remembered");
            Eq(@"\\server\share\Links = Rev C", again.RepathFolder, "repath folder remembered, = and all");
            Eq(@"C:\Reports", again.ExportFolder, "export folder remembered");

            File.WriteAllText(Path.Combine(settingsDir, "Blocker"), "a file where the settings folder would go");
            Check(!FolderMemory.Load(Path.Combine(settingsDir, "Blocker", "Settings.txt")).Save(), "a settings file that can't be written is shrugged off");
            Check(FolderMemory.DefaultFile.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                && FolderMemory.DefaultFile.EndsWith(@"Mike Richardson Design\External Access Links Manager\Settings.txt"), "kept in Local AppData");
            Eq(settingsDir, FolderMemory.FirstExisting(null, "", @"C:\No\Such\Folder", settingsDir, outDir), "the first folder that still exists is used");
            Eq(null, FolderMemory.FirstExisting(null, @"C:\No\Such\Folder"), "or none, so Windows chooses");
            return settingsDir;
        }

        static void FolderPickerChecks(string startFolder)
        {
            var owner = new Window { Width = 200, Height = 100 };
            ShowOffscreen(owner);

            PressWhenShown("Checks folder picker OK", IdOk);
            string picked = FolderPicker.Pick(owner, "Checks folder picker OK", startFolder);
            // Windows hands back the long form of the path (no ~1 short names), so compare by what's in the folder
            Check(picked != null && File.Exists(Path.Combine(picked, "Blocker")), $"the folder picker opens in the start folder and returns it ({picked})");
            PressWhenShown("Checks folder picker Cancel", IdCancel);
            Eq(null, FolderPicker.Pick(owner, "Checks folder picker Cancel", startFolder), "cancelling the folder picker returns nothing");
            owner.Close();
        }

        const int IdOk = 1;
        const int IdCancel = 2;
        const uint WmCommand = 0x0111;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>Waits on another thread for a dialog with this title, then presses one of its buttons.</summary>
        static void PressWhenShown(string title, int button)
        {
            var thread = new Thread(() =>
            {
                for (int i = 0; i < 150; i++)
                {
                    IntPtr dialog = FindWindow("#32770", title);
                    if (dialog != IntPtr.Zero)
                    {
                        Thread.Sleep(300);
                        PostMessage(dialog, WmCommand, (IntPtr)button, IntPtr.Zero);
                        return;
                    }
                    Thread.Sleep(100);
                }
                Console.WriteLine("FAIL: the folder picker never appeared");
                Environment.Exit(3);
            }) { IsBackground = true };
            thread.Start();
        }
    }
}
