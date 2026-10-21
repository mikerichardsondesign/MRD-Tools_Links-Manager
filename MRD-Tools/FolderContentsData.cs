using System.ComponentModel;

namespace MRDTools
{
    /// <summary>
    /// Data source used to store folder contents
    /// </summary>

    public class FolderContentsData : INotifyPropertyChanged
    {
        #region Parameters
        /// <summary>
        /// Setup strings, ints etc.
        /// </summary>

        bool _fileToBeProcessed;
        string _fileToBeProcessedString;
        string _fileName;
        string _fileSavedTime;
        string _filePathName;

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        #region Class Object
        /// <summary>
        /// pass data to class object
        /// </summary>

        public FolderContentsData(
            bool fileToBeProcessed,
            string fileToBeProcessedString,
            string fileName,
            string fileSavedTime,
            string filePathName)
        {
            _fileToBeProcessed = fileToBeProcessed;
            _fileToBeProcessedString = fileToBeProcessedString;
            _fileName = fileName;
            _fileSavedTime = fileSavedTime;
            _filePathName = filePathName;
        }

        #endregion

        #region Get Set
        /// <summary>
        /// get and set parameters
        /// </summary>

        public bool FileToBeProcessed
        {
            get { return _fileToBeProcessed; }
            set
            {
                _fileToBeProcessed = value;
                this.NotifyPropertyChanged("FileToBeProcessed");
            }
        }

        public string FileToBeProcessedString
        {
            get { return _fileToBeProcessedString; }
            set
            {
                _fileToBeProcessedString = value;
                this.NotifyPropertyChanged("FileToBeProcessedString");
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

        public string FileSavedTime
        {
            get { return _fileSavedTime; }
            set
            {
                _fileSavedTime = value;
                this.NotifyPropertyChanged("FileSavedTime");
            }
        }

        public string FilePathName
        {
            get { return _filePathName; }
            set
            {
                _filePathName = value;
                this.NotifyPropertyChanged("FilePathName");
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
