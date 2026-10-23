using System;
using System.ComponentModel;
using System.Text;

namespace MRDTools
{
    /// <summary>
    /// The kinds of external reference the link manager sorts links into.
    /// </summary>
    public enum LinkKind
    {
        Revit,
        Cad,
        Dwf,
        Other
    }

    /// <summary>
    /// Whether the file a link's path on next open points at was found from this computer, in the order the Found column
    /// sorts them: missing files first.
    /// </summary>
    public enum LinkFileStatus
    {
        Missing,
        Found,
        Checking,
        NotChecked
    }

    /// <summary>
    /// One external reference of one project: how it was last saved, the desired state read from the project
    /// (what Revit will apply the next time it opens), and that desired state as edited here.
    /// Revit, CAD and DWF links can be edited; Other links are only shown as last saved.
    /// </summary>
    public class LinkRow : INotifyPropertyChanged
    {
        #region Fields

        string _desiredPath;
        string _desiredName;
        bool _desiredLoaded;
        string _desiredState;
        LinkFileStatus _fileStatus = LinkFileStatus.NotChecked;
        string _checkedFile;

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        #region Constructor

        public LinkRow(string projectPath, string refId, LinkKind kind,
            string lastSavedPath, string lastSavedPathType, string lastSavedState, string lastSavedTime,
            string desiredPath, string desiredPathType, string desiredState)
        {
            ProjectPath = projectPath;
            ProjectName = FileNameOf(projectPath);
            RefId = refId;
            Kind = kind;

            LastSavedPath = lastSavedPath;
            LastSavedName = FileNameOf(lastSavedPath);
            LastSavedPathType = lastSavedPathType;
            LastSavedState = lastSavedState;
            LastSavedTime = lastSavedTime;

            OriginalPathType = desiredPathType;
            OriginalPath = _desiredPath = desiredPath;
            OriginalName = _desiredName = FileNameOf(desiredPath);
            OriginalState = _desiredState = desiredState;
            OriginalLoaded = _desiredLoaded = IsLoadedState(desiredState);
        }

        #endregion

        #region Which link

        /// <summary>Full path of the project (.rvt) the link belongs to.</summary>
        public string ProjectPath { get; }
        public string ProjectName { get; }

        /// <summary>Id of the element that references the linked file, shown as the Link Id.</summary>
        public string RefId { get; }
        public LinkKind Kind { get; }
        public bool IsEditable => Kind != LinkKind.Other;

        /// <summary>The short type name the Excel export uses: Rvt, Dwg, Dwf or Other.</summary>
        public string LinkTypeName
        {
            get
            {
                switch (Kind)
                {
                    case LinkKind.Revit: return "Rvt";
                    case LinkKind.Cad: return "Dwg";
                    case LinkKind.Dwf: return "Dwf";
                    default: return "Other";
                }
            }
        }

        /// <summary>Identifies the link across every project in a batch.</summary>
        public string StableId => ProjectPath + RefId;

        #endregion

        #region As last saved

        public string LastSavedPath { get; }
        public string LastSavedName { get; }
        public string LastSavedPathType { get; }
        public string LastSavedState { get; }
        public string LastSavedTime { get; }

        #endregion

        #region Desired, as read from the project

        public string OriginalPathType { get; private set; }
        public string OriginalPath { get; private set; }
        public string OriginalName { get; private set; }
        public string OriginalState { get; private set; }
        public bool OriginalLoaded { get; private set; }

        #endregion

        #region Desired, as edited

        /// <summary>The path Revit will use. Can be typed into the grid; the link name follows it.</summary>
        public string DesiredPath
        {
            get { return _desiredPath; }
            set
            {
                string path = value ?? "";
                if (path == _desiredPath) return;
                _desiredPath = path;
                _desiredName = FileNameOf(path);
                NotifyPropertyChanged(nameof(DesiredPath));
                NotifyPropertyChanged(nameof(DesiredName));
                NotifyPropertyChanged(nameof(DesiredPathType));
                NotifyPropertyChanged(nameof(IsPathChanged));
                NotifyPropertyChanged(nameof(IsDirty));
            }
        }

        public string DesiredName => _desiredName;

        /// <summary>
        /// The path type to write with DesiredPath. Revit takes the path type as describing the path it's given, so once the
        /// path has changed, a full path makes the link Absolute and a relative path makes it Relative. An unchanged path keeps
        /// the type it was read with, as do Server links.
        /// Setting it, as the Path Type list in the grid does, converts the path to match. A file on another drive or share
        /// has no relative path, so asking for Relative then leaves the link Absolute.
        /// </summary>
        public string DesiredPathType
        {
            get
            {
                if (!IsPathChanged || !CanChangePathType) return OriginalPathType;
                return IsFullPath(_desiredPath) ? LinkConstants.PathTypeAbsolute : LinkConstants.PathTypeRelative;
            }
            set
            {
                if (CanChangePathType && value != DesiredPathType)
                {
                    if (value == LinkConstants.PathTypeRelative) DesiredPath = RelativeToProject(_desiredPath, ProjectPath);
                    else if (value == LinkConstants.PathTypeAbsolute) DesiredPath = AbsoluteFromProject(_desiredPath, ProjectPath);
                }
                // Also when nothing changed, so a list bound to it shows the type the link really has
                NotifyPropertyChanged(nameof(DesiredPathType));
            }
        }

        /// <summary>Revit, CAD and DWF links can be Relative or Absolute; Server links, and Other references, stay as they are.</summary>
        public bool CanChangePathType => IsEditable &&
            (OriginalPathType == LinkConstants.PathTypeRelative || OriginalPathType == LinkConstants.PathTypeAbsolute);

        public bool IsPathChanged => !string.Equals(_desiredPath, OriginalPath, StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether Revit will load the link. The Link State text follows it.</summary>
        public bool DesiredLoaded
        {
            get { return _desiredLoaded; }
            set
            {
                if (value == _desiredLoaded) return;
                _desiredLoaded = value;
                // Back at the value read from the project, show the state Revit reported, such as NotFound
                _desiredState = value == OriginalLoaded ? OriginalState
                    : value ? LinkConstants.StateLoaded : LinkConstants.StateUnloaded;
                NotifyPropertyChanged(nameof(DesiredLoaded));
                NotifyPropertyChanged(nameof(DesiredState));
                NotifyPropertyChanged(nameof(IsDirty));
            }
        }

        public string DesiredState => _desiredState;

        /// <summary>True when the path or load state differs from what the project holds, so it needs writing.</summary>
        public bool IsDirty => _desiredLoaded != OriginalLoaded || IsPathChanged;

        /// <summary>Whether Make Changes writes the link: a changed Revit, CAD or DWF link, or every one of them when writing all.</summary>
        public bool ShouldWrite(bool writeAll) => IsEditable && (writeAll || IsDirty);

        #endregion

        #region Whether the file is there

        /// <summary>Whether the file at DesiredPath was found when it was last looked for (see FileChecker).</summary>
        public LinkFileStatus FileStatus => _fileStatus;

        /// <summary>The Found column's tooltip: the full path looked for, and whether it was there.</summary>
        public string FileStatusText
        {
            get
            {
                switch (_fileStatus)
                {
                    case LinkFileStatus.Found: return "Found: " + _checkedFile;
                    case LinkFileStatus.Missing: return "Not found from this computer: " + _checkedFile;
                    case LinkFileStatus.Checking: return "Looking for " + _checkedFile;
                    default: return IsEditable && !string.IsNullOrWhiteSpace(_desiredPath) ? "Revit Server and cloud models aren't looked for" : null;
                }
            }
        }

        /// <summary>
        /// The full path of the file DesiredPath points at, with a relative path worked out from the project's folder, or null
        /// when there's no file to look for: an Other reference, or a Revit Server or cloud model.
        /// </summary>
        internal string FileToCheck()
        {
            if (!IsEditable || string.IsNullOrWhiteSpace(_desiredPath) || _desiredPath.Contains("://")
                || OriginalPathType == LinkConstants.PathTypeServer)
                return null;
            string full = AbsoluteFromProject(_desiredPath, ProjectPath);
            return IsFullPath(full) ? full : null;
        }

        /// <summary>Marks the link as being looked for, or as not looked for when it has no file, and returns the file to look for.</summary>
        internal string BeginFileCheck()
        {
            string file = FileToCheck();
            SetFileStatus(file == null ? LinkFileStatus.NotChecked : LinkFileStatus.Checking, file);
            return file;
        }

        /// <summary>Records whether the file was found, unless the link has been pointed at another file since it was looked for.</summary>
        internal void EndFileCheck(string file, bool found)
        {
            if (_fileStatus != LinkFileStatus.Checking || !string.Equals(file, _checkedFile, StringComparison.OrdinalIgnoreCase)) return;
            SetFileStatus(found ? LinkFileStatus.Found : LinkFileStatus.Missing, file);
        }

        void SetFileStatus(LinkFileStatus status, string file)
        {
            if (status == _fileStatus && file == _checkedFile) return;
            _fileStatus = status;
            _checkedFile = file;
            NotifyPropertyChanged(nameof(FileStatus));
            NotifyPropertyChanged(nameof(FileStatusText));
        }

        #endregion

        #region Editing

        /// <summary>
        /// Points the link at another file, keeping its path type: a Relative link gets a path relative to its project,
        /// unless the file is on another drive or share, where no relative path exists and the link becomes Absolute.
        /// </summary>
        public void Repath(string targetPath)
        {
            DesiredPath = DesiredPathType == LinkConstants.PathTypeRelative ? RelativeToProject(targetPath, ProjectPath) : targetPath;
        }

        /// <summary>Points the link at the file of the same name in another folder.</summary>
        public void RepathToFolder(string folder) => Repath(System.IO.Path.Combine(folder, DesiredName));

        /// <summary>Whether DesiredPath contains the text, ignoring case unless matchCase is set.</summary>
        public bool PathContains(string find, bool matchCase) =>
            !string.IsNullOrEmpty(find) && (_desiredPath ?? "").IndexOf(find, Comparison(matchCase)) >= 0;

        /// <summary>
        /// Replaces every occurrence of find in DesiredPath, and returns whether the path changed. The path type follows the
        /// new path, as it does when a path is typed. A replacement that would leave no path is ignored.
        /// </summary>
        public bool ReplaceInPath(string find, string replacement, bool matchCase)
        {
            if (!PathContains(find, matchCase)) return false;
            string path = ReplaceText(_desiredPath, find, replacement ?? "", matchCase);
            if (string.IsNullOrWhiteSpace(path) || path == _desiredPath) return false;
            DesiredPath = path;
            return true;
        }

        public void ResetPath() => DesiredPath = OriginalPath;

        public void ResetLoaded() => DesiredLoaded = OriginalLoaded;

        /// <summary>After the desired state has been written, it becomes what the project holds.</summary>
        internal void AcceptChanges()
        {
            // The type first: it's worked out from how the path differs from the original
            OriginalPathType = DesiredPathType;
            OriginalPath = _desiredPath;
            OriginalName = _desiredName;
            OriginalLoaded = _desiredLoaded;
            OriginalState = _desiredState;
            NotifyPropertyChanged(nameof(IsPathChanged));
            NotifyPropertyChanged(nameof(IsDirty));
        }

        #endregion

        #region Helpers

        /// <summary>Revit loads a link that is Loaded, and tries again on opening one it couldn't find.</summary>
        internal static bool IsLoadedState(string state) =>
            state == LinkConstants.StateLoaded || state == LinkConstants.StateNotFound;

        /// <summary>A path with a drive (C:\...) or a share (\\server\...), as opposed to one relative to the project.</summary>
        internal static bool IsFullPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.StartsWith(@"\\") || path.StartsWith("//")) return true;
            return path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
        }

        /// <summary>
        /// A Relative link's full path made relative to the project's folder. Revit hands back CAD links made in its UI as
        /// Relative with their full path, although Manage Links shows them relative. Anything else, including a file on
        /// another drive or share where no relative path exists, comes back as it was.
        /// </summary>
        internal static string RelativeToProject(string path, string projectPath)
        {
            if (!IsFullPath(path)) return path;
            try
            {
                string relative = GetRelativePath.GetRelativePathUriA(path, projectPath);
                return IsFullPath(relative) ? path : relative;
            }
            catch (Exception)
            {
                return path;
            }
        }

        /// <summary>A relative path made full from the project's folder; a full path, or one that can't be resolved, comes back as it was.</summary>
        internal static string AbsoluteFromProject(string path, string projectPath)
        {
            if (string.IsNullOrEmpty(path) || IsFullPath(path)) return path;
            try
            {
                return System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(projectPath), path));
            }
            catch (Exception)
            {
                return path;
            }
        }

        /// <summary>Replaces every occurrence of find, matching case or not: string.Replace can't ignore case on .NET Framework (Revit 2024).</summary>
        internal static string ReplaceText(string text, string find, string replacement, bool matchCase)
        {
            var sb = new StringBuilder();
            int start = 0;
            int at;
            while ((at = text.IndexOf(find, start, Comparison(matchCase))) >= 0)
            {
                sb.Append(text, start, at - start).Append(replacement);
                start = at + find.Length;
            }
            return sb.Append(text, start, text.Length - start).ToString();
        }

        static StringComparison Comparison(bool matchCase) => matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        /// <summary>The file name at the end of a path, without the exceptions Path.GetFileName can throw.</summary>
        internal static string FileNameOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int slash = path.LastIndexOfAny(new[] { '\\', '/' });
            return slash < 0 ? path : path.Substring(slash + 1);
        }

        private void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        #endregion
    }
}
