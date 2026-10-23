using System.ComponentModel;

namespace MRDTools
{
    /// <summary>
    /// A project in the links manager's project list: whether to process it, how many of its links there are and how
    /// many have been changed, and what happened when it was read or written.
    /// </summary>
    public class ProjectRow : INotifyPropertyChanged
    {
        #region Fields

        bool _process = true;
        int _linkCount;
        int _changeCount;
        string _status = "";
        bool _hasError;

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        public ProjectRow(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Folder = System.IO.Path.GetDirectoryName(path);
            LastSaved = GetLastSavedDate.LinksLastSavedDate(path);
        }

        public string Path { get; }
        public string Name { get; }
        public string Folder { get; }
        public string LastSaved { get; }

        /// <summary>Whether the project's links are shown, and written by Make Changes.</summary>
        public bool Process
        {
            get { return _process; }
            set
            {
                if (value == _process) return;
                _process = value;
                NotifyPropertyChanged(nameof(Process));
                NotifyPropertyChanged(nameof(Status));
            }
        }

        public int LinkCount
        {
            get { return _linkCount; }
            internal set
            {
                if (value == _linkCount) return;
                _linkCount = value;
                NotifyPropertyChanged(nameof(LinkCount));
            }
        }

        public int ChangeCount
        {
            get { return _changeCount; }
            internal set
            {
                if (value == _changeCount) return;
                _changeCount = value;
                NotifyPropertyChanged(nameof(ChangeCount));
                NotifyPropertyChanged(nameof(HasChanges));
                NotifyPropertyChanged(nameof(ChangesText));
            }
        }

        public bool HasChanges => _changeCount > 0;
        public string ChangesText => _changeCount == 1 ? "1 CHANGE" : $"{_changeCount} CHANGES";

        /// <summary>What happened when the project was read or written; "Not processed" while it's set aside, unless it couldn't be read.</summary>
        public string Status => !_process && !_hasError ? "Not processed" : _status;
        public bool HasError => _hasError;

        internal void SetStatus(string status, bool isError = false)
        {
            _status = status;
            _hasError = isError;
            NotifyPropertyChanged(nameof(Status));
            NotifyPropertyChanged(nameof(HasError));
        }

        private void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
