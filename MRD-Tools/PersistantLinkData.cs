using System.ComponentModel;

namespace MRDTools
{
    public class PersistantLinkData : INotifyPropertyChanged
    {
        #region Parameters
        /// <summary>
        /// Setup strings, ints etc.
        /// </summary>
        
        string _refId;
        string _refName;
        string _refPath;
        string _refStableId;

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        #region Class Object
        /// <summary>
        /// pass data to class object
        /// </summary>

        public PersistantLinkData(
            string refId,
            string refName,
            string refPath,
            string refStableId)
        {
            _refId = refId;
            _refName = refName;
            _refPath = refPath;
            _refStableId = refStableId;
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

        public string RefPath
        {
            get { return _refPath; }
            set
            {
                _refPath = value;
                this.NotifyPropertyChanged("RefPath");
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

        #endregion

        private void NotifyPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
