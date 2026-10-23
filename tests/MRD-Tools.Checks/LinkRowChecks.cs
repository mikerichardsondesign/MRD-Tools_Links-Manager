using System.Collections.Generic;
using static MRDTools.Checks.Checker;
using static MRDTools.Checks.Samples;

namespace MRDTools.Checks
{
    /// <summary>
    /// The link model on its own: load states, repathing, path types, find and replace, what gets written, read-only links,
    /// and what a linked Revit file's header says.
    /// </summary>
    static class LinkRowChecks
    {
        public static void Run()
        {
            Basics();
            LoadState();
            Repathing();
            PathTypes();
            RelativeOnRead();
            AfterWriting();
            FindAndReplace();
            WhatGetsWritten();
            ReadOnlyLinks();
            FileInfo();
        }

        static void Basics()
        {
            var a = Row("101", LinkKind.Revit, @"Links\A.rvt", "Relative", "Loaded");
            Eq("A.rvt", a.DesiredName, "name from path");
            Eq("Host.rvt", a.ProjectName, "project name");
            Eq("Rvt", a.LinkTypeName, "type name");
            Eq(Host + "101", a.StableId, "stable id");
            Check(!a.IsDirty, "a fresh row isn't dirty");
            Check(a.DesiredLoaded, "Loaded is loaded");

            var other = new LinkRow(Host, "200", LinkKind.Other, @"C:\Keynotes\K.txt", "Absolute", "Loaded", "N/A", @"C:\Keynotes\K.txt", "Absolute", "Loaded");
            Check(!other.IsEditable, "Other isn't editable");
            Eq("Other", other.LinkTypeName, "Other type name");
        }

        static void LoadState()
        {
            // Load state follows the checkbox
            var a = Row("101", LinkKind.Revit, @"Links\A.rvt", "Relative", "Loaded");
            var changes = new List<string>();
            a.PropertyChanged += (s, e) => changes.Add(e.PropertyName);
            a.DesiredLoaded = false;
            Eq("Unloaded", a.DesiredState, "untick shows Unloaded");
            Check(a.IsDirty, "untick makes it dirty");
            Check(changes.Contains("DesiredLoaded") && changes.Contains("DesiredState") && changes.Contains("IsDirty"), "untick notifies loaded, state and dirty");
            a.DesiredLoaded = true;
            Eq("Loaded", a.DesiredState, "retick shows Loaded");
            Check(!a.IsDirty, "back to the original isn't dirty");

            // The NotFound case the old toggle got wrong
            var nf = Row("102", LinkKind.Revit, @"C:\Gone\B.rvt", "Absolute", "NotFound");
            Check(nf.DesiredLoaded, "NotFound counts as loaded");
            nf.DesiredLoaded = false;
            Eq("Unloaded", nf.DesiredState, "untick NotFound shows Unloaded (the old toggle showed Loaded)");
            Check(nf.IsDirty && !nf.DesiredLoaded, "untick NotFound will unload it");
            nf.DesiredLoaded = true;
            Eq("NotFound", nf.DesiredState, "retick NotFound shows NotFound again");
            Check(!nf.IsDirty, "retick NotFound isn't dirty");

            var lu = Row("103", LinkKind.Revit, @"C:\X\C.rvt", "Absolute", "LocallyUnloaded");
            Check(!lu.DesiredLoaded, "LocallyUnloaded isn't loaded");
            lu.DesiredLoaded = true;
            Eq("Loaded", lu.DesiredState, "tick LocallyUnloaded shows Loaded");
            lu.ResetLoaded();
            Eq("LocallyUnloaded", lu.DesiredState, "ResetLoaded restores the state Revit reported");
            Check(!lu.IsDirty, "ResetLoaded isn't dirty");
        }

        static void Repathing()
        {
            var a = Row("101", LinkKind.Revit, @"Links\A.rvt", "Relative", "Loaded");
            a.Repath(@"C:\Proj\Links\New\B.rvt");
            Eq(@"Links\New\B.rvt", a.DesiredPath, "Relative link gets a relative path");
            Eq("B.rvt", a.DesiredName, "name follows the path");
            Check(a.IsDirty, "repath is dirty");
            a.DesiredPathType = "Absolute";
            Eq(@"C:\Proj\Links\New\B.rvt", a.DesiredPath, "choosing Absolute converts the path");
            a.DesiredPathType = "Relative";
            Eq(@"Links\New\B.rvt", a.DesiredPath, "choosing Relative converts it back");
            a.Repath(@"D:\Other\B.rvt");
            Eq(@"D:\Other\B.rvt", a.DesiredPath, "another drive stays absolute");
            a.DesiredPathType = "Relative";
            Eq(@"D:\Other\B.rvt", a.DesiredPath, "and can't be made relative");
            Eq("Absolute", a.DesiredPathType, "so it stays Absolute");
            a.Repath(@"\\server\share\B.rvt");
            Eq(@"\\server\share\B.rvt", a.DesiredPath, "a share stays absolute");
            a.ResetPath();
            a.Repath(@"C:\Elsewhere\B.rvt");
            Eq(@"..\Elsewhere\B.rvt", a.DesiredPath, "sibling folder is relative");
            a.ResetPath();
            Eq(@"Links\A.rvt", a.DesiredPath, "ResetPath restores the path");
            Eq("A.rvt", a.DesiredName, "ResetPath restores the name");
            Check(!a.IsDirty, "ResetPath isn't dirty");

            var nf = Row("102", LinkKind.Revit, @"C:\Gone\B.rvt", "Absolute", "NotFound");
            nf.Repath(@"C:\Proj\Links\B.rvt");
            Eq(@"C:\Proj\Links\B.rvt", nf.DesiredPath, "Absolute link stays absolute");
            nf.RepathToFolder(@"E:\Moved");
            Eq(@"E:\Moved\B.rvt", nf.DesiredPath, "RepathToFolder keeps the file name");

            nf.ResetPath();
            nf.DesiredPath = @"c:\gone\b.rvt";
            Check(!nf.IsDirty, "a case-only change isn't dirty");
            nf.DesiredPath = null;
            Eq("", nf.DesiredPath, "a cleared path is empty, not null");
            Check(nf.IsDirty, "a cleared path is dirty");
        }

        static void PathTypes()
        {
            // The path type follows the path once it changes
            var rel = Row("110", LinkKind.Revit, @"Links\R.rvt", "Relative", "Loaded");
            var typeChanges = new List<string>();
            rel.PropertyChanged += (s, e) => typeChanges.Add(e.PropertyName);
            Eq("Relative", rel.DesiredPathType, "unchanged Relative link stays Relative");
            rel.Repath(@"C:\Proj\Links\Other\R.rvt");
            Eq(@"Links\Other\R.rvt", rel.DesiredPath, "Relative repath on the same drive");
            Eq("Relative", rel.DesiredPathType, "a relative path keeps it Relative");
            rel.DesiredPathType = "Absolute";
            Eq(@"C:\Proj\Links\Other\R.rvt", rel.DesiredPath, "choosing Absolute gives the full path");
            Eq("Absolute", rel.DesiredPathType, "and the Absolute type");
            Check(typeChanges.Contains("DesiredPathType"), "the path type change is notified");
            rel.Repath(@"D:\Other\R.rvt");
            Eq("Absolute", rel.DesiredPathType, "another drive makes it Absolute");
            rel.Repath(@"\\server\share\R.rvt");
            Eq("Absolute", rel.DesiredPathType, "a share makes it Absolute");
            rel.Repath(@"C:\Elsewhere\R.rvt");
            Eq(@"C:\Elsewhere\R.rvt", rel.DesiredPath, "repathing keeps the link's current type, Absolute");
            rel.DesiredPathType = "Relative";
            Eq(@"..\Elsewhere\R.rvt", rel.DesiredPath, "choosing Relative works it out from the project's folder");
            Eq("Relative", rel.DesiredPathType, "back to a relative path, back to Relative");
            rel.DesiredPath = @"C:\Typed\R.rvt";
            Eq("Absolute", rel.DesiredPathType, "a typed full path makes it Absolute");
            rel.ResetPath();
            Eq("Relative", rel.DesiredPathType, "ResetPath restores the path type");

            var abs = Row("111", LinkKind.Revit, @"C:\Abs\S.rvt", "Absolute", "Loaded");
            abs.DesiredPath = @"..\Abs\S.rvt";
            Eq("Relative", abs.DesiredPathType, "a typed relative path makes an Absolute link Relative");
            abs.ResetPath();
            abs.Repath(@"C:\Proj\Abs\S.rvt");
            Eq(@"C:\Proj\Abs\S.rvt", abs.DesiredPath, "an Absolute link isn't made relative by Repath");
            Eq("Absolute", abs.DesiredPathType, "and stays Absolute");
            abs.DesiredPath = @"c:\abs\s.rvt";
            Eq("Absolute", abs.DesiredPathType, "a case-only change keeps the type it was read with");

            // Revit sometimes reports a CAD link as Relative with a full path: left as it is unless the path changes
            var cadQuirk = Row("310", LinkKind.Cad, @"C:\Cad\Q.dwg", "Relative", "Loaded");
            cadQuirk.DesiredLoaded = false;
            Check(cadQuirk.IsDirty && !cadQuirk.IsPathChanged, "a load change alone doesn't change the path");
            Eq("Relative", cadQuirk.DesiredPathType, "so the type is written back as read");
            cadQuirk.Repath(@"C:\Proj\Cad\Q.dwg");
            Eq(@"Cad\Q.dwg", cadQuirk.DesiredPath, "repathing it gives a true relative path");
            Eq("Relative", cadQuirk.DesiredPathType, "with the Relative type to match");

            var server = Row("120", LinkKind.Revit, @"RSN://srv/Proj/S.rvt", "Server", "Loaded");
            server.DesiredPath = @"RSN://srv/Proj/T.rvt";
            Eq("Server", server.DesiredPathType, "a Server link keeps its type");
            Check(!server.CanChangePathType, "a Server link's type can't be chosen");
            server.DesiredPathType = "Absolute";
            Eq("Server", server.DesiredPathType, "and setting it does nothing");

            // Choosing a path type converts the path, from the project's folder
            var conv = Row("140", LinkKind.Cad, @"..\Shared\G.dwg", "Relative", "Loaded");
            Check(conv.CanChangePathType, "a CAD link's type can be chosen");
            var convChanges = new List<string>();
            conv.PropertyChanged += (s, e) => convChanges.Add(e.PropertyName);
            conv.DesiredPathType = "Absolute";
            Eq(@"C:\Shared\G.dwg", conv.DesiredPath, "Absolute resolves .. from the project's folder");
            Check(conv.IsDirty && conv.IsPathChanged, "and is a change");
            Check(convChanges.Contains("IsPathChanged"), "the bold path follows (IsPathChanged is notified)");
            conv.DesiredPathType = "Relative";
            Eq(@"..\Shared\G.dwg", conv.DesiredPath, "Relative gives the original path back");
            Check(!conv.IsDirty, "so it's no longer a change");
            var crossDrive = Row("141", LinkKind.Revit, @"D:\Elsewhere\X.rvt", "Absolute", "Loaded");
            var crossChanges = new List<string>();
            crossDrive.PropertyChanged += (s, e) => crossChanges.Add(e.PropertyName);
            crossDrive.DesiredPathType = "Relative";
            Eq("Absolute", crossDrive.DesiredPathType, "a file on another drive can't be Relative");
            Eq(@"D:\Elsewhere\X.rvt", crossDrive.DesiredPath, "and its path is untouched");
            Check(!crossDrive.IsDirty, "so nothing changes");
            Check(crossChanges.Contains("DesiredPathType"), "but the type is notified, so a list shows Absolute again");

            var forced = Row("130", LinkKind.Revit, @"Links\F.rvt", "Relative", "Loaded");
            forced.DesiredPathType = "Absolute";
            forced.Repath(@"C:\Proj\Links\F2.rvt");
            forced.AcceptChanges();
            Eq("Absolute", forced.OriginalPathType, "after writing, the link is Absolute");
            Eq("Absolute", forced.DesiredPathType, "and reads as Absolute");
            forced.Repath(@"C:\Proj\Links\F3.rvt");
            Eq(@"C:\Proj\Links\F3.rvt", forced.DesiredPath, "so later repaths stay absolute");
        }

        /// <summary>Relative links that Revit hands back with a full path (CAD links do), converted when they're read.</summary>
        static void RelativeOnRead()
        {
            const string deepProject = @"C:\Users\Someone\OneDrive - Company\Revit Testing Content - 2018\Project.rvt";
            Eq(@"Links\DWG Links\Drawing1.dwg",
                LinkRow.RelativeToProject(@"C:\Users\Someone\OneDrive - Company\Revit Testing Content - 2018\Links\DWG Links\Drawing1.dwg", deepProject),
                "a CAD link deep in a synced folder becomes relative, as in Manage Links");
            Eq(@"..\Shared\D.dwg", LinkRow.RelativeToProject(@"C:\Shared\D.dwg", Host), "a file beside the project's folder");
            Eq(@"Links\D.dwg", LinkRow.RelativeToProject(@"c:\proj\Links\D.dwg", Host), "drive letter case doesn't matter");
            Eq(@"D:\Other\D.dwg", LinkRow.RelativeToProject(@"D:\Other\D.dwg", Host), "another drive is left as read");
            Eq(@"\\server\share\D.dwg", LinkRow.RelativeToProject(@"\\server\share\D.dwg", Host), "a share is left as read for a project on C:");
            Eq(@"Links\D.dwg", LinkRow.RelativeToProject(@"\\srv\share\Proj\Links\D.dwg", @"\\srv\share\Proj\Host.rvt"), "a project and link on the same share");
            Eq(@"Links\D.dwg", LinkRow.RelativeToProject(@"Links\D.dwg", Host), "an already relative path is left alone");
            Eq("", LinkRow.RelativeToProject("", Host), "an empty path is left alone");

            string converted = LinkRow.RelativeToProject(@"C:\Proj\Links\E.dwg", Host);
            var readRelative = new LinkRow(Host, "320", LinkKind.Cad, converted, "Relative", "Loaded", "N/A", converted, "Relative", "Loaded");
            Check(!readRelative.IsDirty && !readRelative.IsPathChanged, "a converted path is the starting point, not an edit");
            Eq(@"Links\E.dwg", readRelative.DesiredPath, "and is what gets written with Write All Links");
            Eq("Relative", readRelative.DesiredPathType, "with the Relative type to match");
        }

        static void AfterWriting()
        {
            var a = Row("101", LinkKind.Revit, @"Links\A.rvt", "Relative", "Loaded");
            a.DesiredLoaded = false;
            a.Repath(@"C:\Proj\Z\Z.rvt");
            a.AcceptChanges();
            Check(!a.IsDirty, "AcceptChanges clears dirty");
            Eq(@"Z\Z.rvt", a.OriginalPath, "AcceptChanges updates the original path");
            Eq("Unloaded", a.OriginalState, "AcceptChanges updates the original state");
            a.Repath(@"C:\Proj\Q.rvt");
            a.ResetPath();
            Eq(@"Z\Z.rvt", a.DesiredPath, "ResetPath then returns to the written path");
            Eq(@"Links\A.rvt", a.LastSavedPath, "last saved data is untouched");
        }

        static void FindAndReplace()
        {
            Eq(@"\\new\Projects\new.rvt", LinkRow.ReplaceText(@"\\OLD\Projects\old.rvt", "old", "new", false), "every match is replaced, ignoring case");
            Eq(@"\\OLD\Projects\new.rvt", LinkRow.ReplaceText(@"\\OLD\Projects\old.rvt", "old", "new", true), "or only those that match case");
            Eq(@"Links\A.rvt", LinkRow.ReplaceText(@"Links\Rev C\A.rvt", @"Rev C\", "", false), "replacing with nothing removes the text");
            Eq(@"Links\A.rvt", LinkRow.ReplaceText(@"Links\A.rvt", "zzz", "y", false), "no match leaves the text alone");

            var onShare = Row("901", LinkKind.Revit, @"\\old-server\Projects\Site\Site.rvt", "Absolute", "Loaded");
            Check(onShare.PathContains("OLD-SERVER", false) && !onShare.PathContains("OLD-SERVER", true), "PathContains follows Match case");
            Check(onShare.ReplaceInPath("OLD-SERVER", "new-server", false), "a path containing the text is changed");
            Eq(@"\\new-server\Projects\Site\Site.rvt", onShare.DesiredPath, "to the replaced path");
            Check(onShare.IsDirty && onShare.DesiredPathType == "Absolute", "marked changed, still Absolute");
            Check(!onShare.ReplaceInPath("old-server", "x", false), "a path without the text isn't changed");
            var relativeLink = Row("902", LinkKind.Revit, @"Links\A.rvt", "Relative", "Loaded");
            Check(relativeLink.ReplaceInPath(@"Links\", @"D:\Shared\Links\", false), "a relative path can be replaced");
            Eq("Absolute", relativeLink.DesiredPathType, "and one made full becomes Absolute, as a typed path does");
            var wholePath = Row("903", LinkKind.Revit, "A.rvt", "Relative", "Loaded");
            Check(!wholePath.ReplaceInPath("a.rvt", "", false) && wholePath.DesiredPath == "A.rvt", "a replacement that would leave no path is ignored");
        }

        /// <summary>ShouldWrite decides what Make Changes writes, and what its preview counts.</summary>
        static void WhatGetsWritten()
        {
            var unchanged = Row("904", LinkKind.Cad, @"Links\B.dwg", "Relative", "Loaded");
            Check(!unchanged.ShouldWrite(false) && unchanged.ShouldWrite(true), "an unchanged link is written only when writing all");
            var changed = Row("906", LinkKind.Revit, @"Links\C.rvt", "Relative", "Loaded");
            changed.DesiredLoaded = false;
            Check(changed.ShouldWrite(false), "a changed link is always written");
            Check(!Row("905", LinkKind.Other, "K.txt", "Relative", "Loaded").ShouldWrite(true), "an Other reference never is");
        }

        /// <summary>Links that can't be changed here: Revit Server, cloud and IFC.</summary>
        static void ReadOnlyLinks()
        {
            var serverLink = Row("950", LinkKind.Revit, "RSN://server/Projects/Central.rvt", "Server", "Loaded");
            var cloudLink = Row("951", LinkKind.Revit, "Autodesk Docs://Project/Model.rvt", "Cloud", "Loaded");
            var cloudByPath = Row("952", LinkKind.Revit, "BIM 360://Project/Model.rvt", "Absolute", "Loaded");
            var ifcLink = Row("953", LinkKind.Revit, @"Links\Survey.ifc.rvt", "Relative", "Loaded");
            var plainLink = Row("954", LinkKind.Revit, @"Links\A.rvt", "Relative", "Loaded");
            var otherRef = Row("955", LinkKind.Other, "K.txt", "Relative", "Loaded");
            Check(serverLink.IsReadOnly && !serverLink.IsEditable && serverLink.ReadOnlyReason.StartsWith("A Revit Server model"), "a Revit Server model can't be changed here");
            Check(cloudLink.IsReadOnly && cloudLink.ReadOnlyReason.StartsWith("A cloud model"), "nor a cloud model");
            Check(cloudByPath.IsReadOnly && cloudByPath.ReadOnlyReason.StartsWith("A cloud model"), "known by its path as well");
            Check(ifcLink.IsReadOnly && ifcLink.ReadOnlyReason.StartsWith("Made from an IFC file"), "nor a link made from an IFC file");
            Check(!plainLink.IsReadOnly && plainLink.IsEditable, "a plain Revit link can be");
            Check(!otherRef.IsReadOnly && !otherRef.IsEditable, "an Other reference isn't marked read-only: its whole tab is");
            Check(!serverLink.ShouldWrite(true) && !cloudLink.ShouldWrite(true) && !ifcLink.ShouldWrite(true), "read-only links are never written, even writing all");
            Eq("Server", serverLink.DesiredPathType, "a server model keeps its path type");
            Eq(@"C:\Proj\Links\Survey.ifc.rvt", ifcLink.FileToCheck(), "an IFC link's file is still looked for");
        }

        /// <summary>What a linked Revit file's header says, as the Saved In column shows it.</summary>
        static void FileInfo()
        {
            var centralInfo = new LinkFileInfo("2025", true, true, false, true, false);
            Eq("2025 · Central", centralInfo.Text, "a central model");
            Eq("Saved in Revit 2025, a central model.", centralInfo.ToolTip, "its tooltip");
            var localInfo = new LinkFileInfo("2025", true, false, true, true, false);
            Eq("2025 · Local", localInfo.Text, "a local copy");
            Check(localInfo.ToolTip.Contains("Link the central model instead"), "suggests linking the central model");
            var laterInfo = new LinkFileInfo("2027", false, false, false, false, true);
            Eq("2027", laterInfo.Text, "a model that isn't workshared shows just its version");
            Check(laterInfo.ToolTip.Contains("won't load here") && !laterInfo.IsOlderVersion, "a later version won't load");
            var olderInfo = new LinkFileInfo("2022", false, false, false, false, false);
            Check(olderInfo.IsOlderVersion && olderInfo.ToolTip.Contains("upgrades it each time"), "an earlier one is upgraded each time");
            var badInfo = LinkFileInfo.Unreadable("The file is not a Revit file.");
            Eq("Unreadable", badInfo.Text, "a header that can't be read");
            Check(badInfo.ToolTip.EndsWith("The file is not a Revit file."), "says why");
        }
    }
}
