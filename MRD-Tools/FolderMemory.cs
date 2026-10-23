using System;
using System.Collections.Generic;
using System.IO;

namespace MRDTools
{
    /// <summary>
    /// The folders the links manager's dialogs last used, kept between sessions in
    /// %LOCALAPPDATA%\Mike Richardson Design\External Access Links Manager\Settings.txt.
    /// Local AppData always belongs to the signed-in user, so it can be written there. If it can't be read or written
    /// anyway, nothing is remembered and Windows picks where each dialog starts.
    /// </summary>
    internal class FolderMemory
    {
        const string ProjectsKey = "ProjectsFolder";
        const string RepathKey = "RepathFolder";
        const string ExportKey = "ExportFolder";

        readonly string file;

        /// <summary>Where Add Files and Add Folder last added projects from.</summary>
        public string ProjectsFolder { get; set; }

        /// <summary>The folder links were last repathed to.</summary>
        public string RepathFolder { get; set; }

        /// <summary>Where the Excel export was last saved.</summary>
        public string ExportFolder { get; set; }

        public static string DefaultFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Mike Richardson Design", "External Access Links Manager", "Settings.txt");

        FolderMemory(string file)
        {
            this.file = file;
        }

        /// <summary>Reads the remembered folders; a missing or unreadable file remembers nothing.</summary>
        public static FolderMemory Load(string file = null)
        {
            var memory = new FolderMemory(file ?? DefaultFile);
            try
            {
                if (!File.Exists(memory.file)) return memory;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(memory.file))
                {
                    int equals = line.IndexOf('=');
                    if (equals > 0) values[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
                memory.ProjectsFolder = values.TryGetValue(ProjectsKey, out string projects) ? projects : null;
                memory.RepathFolder = values.TryGetValue(RepathKey, out string repath) ? repath : null;
                memory.ExportFolder = values.TryGetValue(ExportKey, out string export) ? export : null;
            }
            catch (Exception)
            {
                // Nothing remembered
            }
            return memory;
        }

        /// <summary>Writes the folders back. Returns false if it couldn't, which only means they won't be remembered.</summary>
        public bool Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllLines(file, new[]
                {
                    "# Folders the External Access Links Manager's dialogs last used",
                    ProjectsKey + "=" + ProjectsFolder,
                    RepathKey + "=" + RepathFolder,
                    ExportKey + "=" + ExportFolder,
                });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The first of the folders given that's set and still exists, or null to let Windows choose.</summary>
        public static string FirstExisting(params string[] folders)
        {
            foreach (string folder in folders)
            {
                try
                {
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) return folder;
                }
                catch (Exception)
                {
                    // Not a usable folder
                }
            }
            return null;
        }
    }
}
