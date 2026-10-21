using System.ComponentModel;

namespace MRDTools
{
    public class PersistantLoadUnloadData : INotifyPropertyChanged
    {
        #region Parameters
        /// <summary>
        /// Setup strings, ints etc.
        /// </summary>
        
        string _refId;
        string _refLoadState;
        bool _refIsLoaded;
        string _refStableId;

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        #region Class Object
        /// <summary>
        /// pass data to class object
        /// </summary>

        public PersistantLoadUnloadData(
            string refId,
            string refLoadState,
            bool refIsLoaded,
            string refStableId)
        {
            _refId = refId;
            _refLoadState = refLoadState;
            _refIsLoaded = refIsLoaded;
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
