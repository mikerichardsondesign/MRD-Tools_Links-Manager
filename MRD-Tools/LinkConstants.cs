namespace MRDTools
{
    internal static class LinkConstants
    {
        public const string StateLoaded = "Loaded";
        public const string StateUnloaded = "Unloaded";
        public const string StateNotFound = "NotFound";
        public const string PathTypeRelative = "Relative";

        public const string FilterRvt = "Rvt Files (*.rvt)|*.rvt";
        public const string FilterCad = "Dwg Files (*.dwg)|*.dwg";
        public const string FilterDwf = "Dwf Files (*.dwf)|*.dwf|Dwfx Files (*.dwfx)|*.dwfx";

        public const string MsgTransmissionFailed = "The project could not be changed. Make sure it is not open";
        public const string MsgBatchTransmissionFailed = "These projects could not be changed. Make sure they are not open";
        public const string MsgNoLinks = "The Document does not have Any Links";
        public const string MsgOperationFailed = "Operation could not be completed. Make sure you have selected a project using the 'Browse Project' button";
        public const string MsgSelectRows = "Please select rows from the 'Current Status' table and try again";
    }
}
