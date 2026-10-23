namespace MRDTools
{
    internal static class LinkConstants
    {
        public const string StateLoaded = "Loaded";
        public const string StateUnloaded = "Unloaded";
        public const string StateNotFound = "NotFound";
        public const string PathTypeRelative = "Relative";
        public const string PathTypeAbsolute = "Absolute";
        public const string PathTypeServer = "Server";
        public const string PathTypeCloud = "Cloud";

        public const string FilterRvt = "Rvt Files (*.rvt)|*.rvt";
        public const string FilterCad = "Dwg Files (*.dwg)|*.dwg";
        public const string FilterDwf = "Dwf Files (*.dwf, *.dwfx)|*.dwf;*.dwfx";

        public const string MsgBatchTransmissionFailed = "These projects could not be changed. Make sure they are not open";
        public const string MsgNoLinks = "The project has no links";
        public const string MsgSelectRows = "Select one or more links in the list first";
        public const string MsgSelectProjects = "Select the projects to remove in the Projects list first";
        public const string MsgNoProjects = "Tick Process for the projects you want to change first";
        public const string MsgNoChanges = "No links have been changed, so nothing has been written. Change a link's path or load state first, or tick 'Write all links, not just changed ones' to write every link as it is";
        public const string MsgNoLinksToWrite = "There are no Revit, CAD or DWF links to write";

        public const string TipWriteAllLinks = "Writes every Revit, CAD and DWF link back to the project, not just the ones you've changed.\n"
            + "Locked links (Revit Server, cloud and IFC) are left as they are.\n\n"
            + "Tick this if you'll move the project and its linked files to another folder afterwards.\n"
            + "Make Changes sets the project to transmitted, and when it next opens, relative links\n"
            + "that weren't written may not be found from the new folder.\n\n"
            + "Left clear, only the links you've changed are written, and a project with none is left untouched.";
    }
}
