using System;
using System.ComponentModel;

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

            DesiredPathType = desiredPathType;
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

        public string DesiredPathType { get; }
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
                NotifyPropertyChanged(nameof(IsDirty));
            }
        }

        public string DesiredName => _desiredName;

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
        public bool IsDirty =>
            _desiredLoaded != OriginalLoaded || !string.Equals(_desiredPath, OriginalPath, StringComparison.OrdinalIgnoreCase);

        #endregion

        #region Editing

        /// <summary>
        /// Points the link at another file. A Relative link gets a path relative to its project, unless
        /// forceAbsolute is set or the file is on another drive or share, where no relative path exists.
        /// </summary>
        public void Repath(string targetPath, bool forceAbsolute)
        {
            string path = targetPath;
            if (!forceAbsolute && DesiredPathType == LinkConstants.PathTypeRelative)
            {
                try { path = GetRelativePath.GetRelativePathUriA(targetPath, ProjectPath); }
                catch (Exception) { path = targetPath; }
            }
            DesiredPath = path;
        }

        /// <summary>Points the link at the file of the same name in another folder.</summary>
        public void RepathToFolder(string folder, bool forceAbsolute) =>
            Repath(System.IO.Path.Combine(folder, DesiredName), forceAbsolute);

        public void ResetPath() => DesiredPath = OriginalPath;

        public void ResetLoaded() => DesiredLoaded = OriginalLoaded;

        /// <summary>After the desired state has been written, it becomes what the project holds.</summary>
        internal void AcceptChanges()
        {
            OriginalPath = _desiredPath;
            OriginalName = _desiredName;
            OriginalLoaded = _desiredLoaded;
            OriginalState = _desiredState;
            NotifyPropertyChanged(nameof(IsDirty));
        }

        #endregion

        #region Helpers

        /// <summary>Revit loads a link that is Loaded, and tries again on opening one it couldn't find.</summary>
        internal static bool IsLoadedState(string state) =>
            state == LinkConstants.StateLoaded || state == LinkConstants.StateNotFound;

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
