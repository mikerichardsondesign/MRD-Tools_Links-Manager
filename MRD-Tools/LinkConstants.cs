namespace MRDTools
{
    internal static class LinkConstants
    {
        public const string StateLoaded = "Loaded";
        public const string StateUnloaded = "Unloaded";
        public const string StateNotFound = "NotFound";
        public const string PathTypeRelative = "Relative";
        public const string PathTypeAbsolute = "Absolute";

        public const string FilterRvt = "Rvt Files (*.rvt)|*.rvt";
        public const string FilterCad = "Dwg Files (*.dwg)|*.dwg";
        public const string FilterDwf = "Dwf Files (*.dwf)|*.dwf|Dwfx Files (*.dwfx)|*.dwfx";

        public const string MsgTransmissionFailed = "The project could not be changed. Make sure it is not open";
        public const string MsgBatchTransmissionFailed = "These projects could not be changed. Make sure they are not open";
        public const string MsgNoLinks = "The Document does not have Any Links";
        public const string MsgOperationFailed = "Operation could not be completed. Make sure you have selected a project using the 'Browse Project' button";
        public const string MsgSelectRows = "Please select rows from the 'Current Status' table and try again";
        public const string MsgNoChanges = "No links have been changed, so nothing has been written. Change a link's path or load state first, or tick 'Write All Links, Not Just Changed Ones' to write every link as it is";
        public const string MsgNoLinksToWrite = "There are no Revit, CAD or DWF links to write";

        public const string TipWriteAllLinks = "Writes every Revit, CAD and DWF link back to the project, not just the ones you've changed.\n\n"
            + "Leave this ticked if you'll move the project and its linked files to another folder afterwards.\n"
            + "Make Changes sets the project to transmitted, and when it next opens, relative links\n"
            + "that weren't written may not be found from the new folder.\n\n"
            + "Clear it to write only the links you've changed, and leave a project with none untouched.";
        public const string MsgLinksNotFound = "None of the changed links were found in the project, so nothing has been written. It may have changed since it was read: browse to it again and retry";
    }
}
