using System.ComponentModel;

namespace MRDTools
{
    /// <summary>
    /// Data source used to store a Link Data
    /// </summary>

    public class LinkData : INotifyPropertyChanged
    {
        #region Parameters
        /// <summary>
        /// Setup strings, ints etc.
        /// </summary>
        
        string _refId;
        string _refName;
        string _refPathType;
        string _refLoadState;
        bool _refIsLoaded;
        string _refPath;
        string _refSavedTime;
        string _filePath;
        string _fileName;
        string _refStableId;
        string _refLinkType;

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        #region Class Object
        /// <summary>
        /// pass data to class object
        /// </summary>

        public LinkData(
            string refId,
            string refName,
            string refPathType,
            string refLoadState,
            bool refIsLoaded,
            string refPath,
            string refSavedTime,
            string filePath,
            string fileName,
            string refStableId,
            string refLinkType)
        {
            _refId = refId;
            _refName = refName;
            _refPathType = refPathType;
            _refLoadState = refLoadState;
            _refIsLoaded = refIsLoaded;
            _refPath = refPath;
            _refSavedTime = refSavedTime;
            _filePath = filePath;
            _fileName = fileName;
            _refStableId = refStableId;
            _refLinkType = refLinkType;
        }

        #endregion

        #region Get Set
        /// <summary>
        /// get and set parameters
        /// </summary>

        public string RefId
        {
            get { return _refId; }
            set
            {
                _refId = value;
                this.NotifyPropertyChanged("RefId");
            }
        }

        public string RefName
        {
            get { return _refName; }
            set
            {
                _refName = value;
                this.NotifyPropertyChanged("RefName");
            }
        }

        public string RefPathType
        {
            get { return _refPathType; }
            set
            {
                _refPathType = value;
                this.NotifyPropertyChanged("RefPathType");
            }
        }

        public string RefLoadState
        {
            get { return _refLoadState; }
            set
            {
                _refLoadState = value;
                this.NotifyPropertyChanged("RefLoadState");
            }
        }

        public bool RefIsLoaded
        {
            get { return _refIsLoaded; }
            set
            {
                _refIsLoaded = value;
                this.NotifyPropertyChanged("RefIsLoaded");
            }
        }

        public string RefPath
        {
            get { return _refPath; }
            set
            {
                _refPath = value;
                this.NotifyPropertyChanged("RefPath");
            }
        }

        public string RefSavedTime
        {
            get { return _refSavedTime; }
            set
            {
                _refSavedTime = value;
                this.NotifyPropertyChanged("RefSavedTime");
            }
        }

        public string FilePath
        {
            get { return _filePath; }
            set
            {
                _filePath = value;
                this.NotifyPropertyChanged("FilePath");
            }
        }

        public string FileName
        {
            get { return _fileName; }
            set
            {
                _fileName = value;
                this.NotifyPropertyChanged("FileName");
            }
        }

        public string RefStableId
        {
            get { return _refStableId; }
            set
            {
                _refStableId = value;
                this.NotifyPropertyChanged("RefStableId");
            }
        }

        public string RefLinkType
        {
            get { return _refLinkType; }
            set
            {
                _refLinkType = value;
                this.NotifyPropertyChanged("RefLinkType");
            }
        }

        #endregion

        private void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }       
}
