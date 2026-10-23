namespace MRDTools
{
    /// <summary>
    /// What a linked Revit file's header says about it, read without opening it (see RevitFileInfo): the Revit version it
    /// was saved in, and whether it's a central model or someone's local copy of one.
    /// </summary>
    public sealed class LinkFileInfo
    {
        public LinkFileInfo(string format, bool isWorkshared, bool isCentral, bool isLocal, bool isCurrentVersion, bool isLaterVersion)
        {
            Format = format;
            IsWorkshared = isWorkshared;
            IsCentral = isCentral;
            IsLocal = isLocal;
            IsCurrentVersion = isCurrentVersion;
            IsLaterVersion = isLaterVersion;
        }

        LinkFileInfo(string error)
        {
            Error = error;
        }

        /// <summary>For a file whose header couldn't be read, such as one that isn't a Revit file.</summary>
        public static LinkFileInfo Unreadable(string reason) => new LinkFileInfo(string.IsNullOrWhiteSpace(reason) ? "unknown reason" : reason.Trim());

        /// <summary>The Revit version the file was saved in, such as 2025.</summary>
        public string Format { get; }
        public bool IsWorkshared { get; }
        public bool IsCentral { get; }
        public bool IsLocal { get; }
        public bool IsCurrentVersion { get; }

        /// <summary>Saved in a later Revit than this one, so the link won't load here.</summary>
        public bool IsLaterVersion { get; }

        /// <summary>Why the header couldn't be read, or null when it was.</summary>
        public string Error { get; }

        public bool IsOlderVersion => Error == null && !IsCurrentVersion && !IsLaterVersion;

        /// <summary>The Saved In column: the version, then Central or Local for a workshared model.</summary>
        public string Text => Error != null ? "Unreadable"
            : Format + (IsLocal ? " · Local" : IsCentral ? " · Central" : IsWorkshared ? " · Workshared" : "");

        public string ToolTip
        {
            get
            {
                if (Error != null) return "Revit couldn't read this file's details: " + Error;

                string tip = $"Saved in Revit {Format}" + (IsCentral ? ", a central model."
                    : IsLocal ? ", a local copy of a central model." : IsWorkshared ? ", workshared." : ".");
                if (IsLaterVersion) tip += "\nThat's later than this Revit, so the link won't load here.";
                else if (IsOlderVersion) tip += "\nThat's earlier than this Revit, so Revit upgrades it each time the project opens, which takes longer.";
                if (IsLocal) tip += "\nLink the central model instead, so the link shows everyone's saved work.";
                return tip;
            }
        }
    }
}
