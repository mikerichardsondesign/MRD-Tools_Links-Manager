using System;
using System.IO;
using System.Linq;
using System.Windows;
using static MRDTools.Checks.Checker;

namespace MRDTools.Checks
{
    /// <summary>
    /// Runs every check, prints any that fail and a count, and returns 1 if any did. Its test files and the window
    /// screenshots go in %TEMP%\MRD-Tools.Checks, or the folder after --out. With --update-help, the screenshots the help
    /// page uses are copied into bundle\Contents\Resources too.
    /// </summary>
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            int outIndex = Array.IndexOf(args, "--out");
            string outDir = outIndex >= 0 && outIndex + 1 < args.Length ? args[outIndex + 1] : Path.Combine(Path.GetTempPath(), "MRD-Tools.Checks");
            bool updateHelp = args.Contains("--update-help");
            Directory.CreateDirectory(outDir);

            // The windows need a WPF application, but the theme is merged into each window, not into this
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            LinkRowChecks.Run();
            ReportChecks.Run(outDir);
            WindowChecks.Run(outDir);
            var files = new TestFiles(outDir);
            FileCheckerChecks.Run(files);
            new LinkToolsChecks(files, outDir).Run();
            SettingsChecks.Run(outDir);
            Screenshots.Render(outDir);

            if (updateHelp) UpdateHelpImages(outDir);

            // Returning ends the process without closing the windows, some of which would ask about their unwritten changes
            Console.WriteLine($"{Passes} passed, {Failures} failed");
            Console.WriteLine($"Test files and screenshots: {outDir}");
            return Failures == 0 ? 0 : 1;
        }

        /// <summary>Copies the help page's screenshots into the repo's bundle\Contents\Resources.</summary>
        static void UpdateHelpImages(string outDir)
        {
            // Up from the build output to the repo root, which has the bundle folder
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "bundle", "Contents", "Resources"))) dir = dir.Parent;
            if (dir == null)
            {
                Check(false, "the repo's bundle\\Contents\\Resources folder is found, for --update-help");
                return;
            }

            string resources = Path.Combine(dir.FullName, "bundle", "Contents", "Resources");
            foreach (string image in Screenshots.HelpImages)
                File.Copy(Path.Combine(outDir, image), Path.Combine(resources, image), overwrite: true);
            Console.WriteLine($"Help page screenshots copied to {resources}");
        }
    }
}
