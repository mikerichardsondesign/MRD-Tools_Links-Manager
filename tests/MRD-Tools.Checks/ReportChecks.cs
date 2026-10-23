using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OfficeOpenXml;
using static MRDTools.Checks.Checker;
using static MRDTools.Checks.Samples;

namespace MRDTools.Checks
{
    /// <summary>
    /// The text report and the Excel export: every link as last saved, and the Revit, CAD and DWF links as they'll be.
    /// </summary>
    static class ReportChecks
    {
        public static void Run(string outDir)
        {
            // One link written already (unloaded, at Z\Z.rvt), one changed since (the CAD link), and the rest as read
            var a = Row("101", LinkKind.Revit, @"Links\A.rvt", "Relative", "Loaded");
            a.DesiredLoaded = false;
            a.Repath(@"C:\Proj\Z\Z.rvt");
            a.AcceptChanges();
            var nf = Row("102", LinkKind.Revit, @"C:\Gone\B.rvt", "Absolute", "NotFound");
            var lu = Row("103", LinkKind.Revit, @"C:\X\C.rvt", "Absolute", "LocallyUnloaded");
            var cad = Row("300", LinkKind.Cad, @"C:\Cad\P.dwg", "Absolute", "Loaded");
            var dwf = Row("400", LinkKind.Dwf, @"C:\Dwf\M.dwf", "Absolute", "Loaded");
            var other = new LinkRow(Host, "200", LinkKind.Other, @"C:\Keynotes\K.txt", "Absolute", "Loaded", "N/A", @"C:\Keynotes\K.txt", "Absolute", "Loaded");
            cad.DesiredLoaded = false;
            var saved = new List<LinkRow> { a, nf, lu, cad, dwf, other };
            List<LinkRow> desired = saved.Where(r => r.IsEditable).ToList();
            Eq("101,102,103,300,400", string.Join(",", desired.Select(r => r.RefId)), "desired order");

            TextReport(outDir, saved, desired);
            Workbook(outDir, saved);
        }

        static void TextReport(string outDir, List<LinkRow> saved, List<LinkRow> desired)
        {
            string single = LinkReport.Build(saved, desired, includeProjectName: false);
            string batch = LinkReport.Build(saved, desired, includeProjectName: true);
            File.WriteAllText(Path.Combine(outDir, "report-single.txt"), single);
            File.WriteAllText(Path.Combine(outDir, "report-batch.txt"), batch);
            string nl = Environment.NewLine;
            Check(single.StartsWith("PLEASE NOTE THAT THIS OUTPUT"), "single report starts with the note");
            Check(single.Contains("REVIT - LAST SAVED LINK STATUS" + nl + "LINK ID\tLINK NAME\tLINK STATE\tLINK PATH\tDATE" + nl
                + "101\tA.rvt\tLoaded\tLinks\\A.rvt\t01-Jan-2026 10:00" + nl), "Revit last saved section");
            Check(single.Contains("CAD - CURRENT LINK STATUS WHICH WILL BE APPLIED UPON NEXT OPEN OF PROJECT" + nl + "LINK ID\tLINK NAME\tLINK STATE\tLINK PATH" + nl
                + "300\tP.dwg\tUnloaded\tC:\\Cad\\P.dwg" + nl), "CAD desired section");
            Check(single.Contains("OTHER LINKS - LAST SAVED LINK STATUS" + nl + "LINK ID\tLINK NAME\tLINK STATE\tLINK PATH" + nl
                + "200\tK.txt\tLoaded\tC:\\Keynotes\\K.txt" + nl), "Other section has no date");
            Check(batch.StartsWith("PLEASE NOTE THAT THIS OUTPUT"), "a report of several projects starts with the note too");
            Check(batch.Contains("PROJECT NAME\tLINK ID\tLINK NAME\tLINK STATE\tLINK PATH\tDATE" + nl + "Host.rvt\t101\t"), "batch report has the project name");
        }

        /// <summary>One Links sheet, like the window's list.</summary>
        static void Workbook(string outDir, List<LinkRow> saved)
        {
            string xlsx = Path.Combine(outDir, "links-test.xlsx");
            if (File.Exists(xlsx)) File.Delete(xlsx);
            LinkWorkbook.Save(xlsx, saved);
            using (var package = new ExcelPackage(new FileInfo(xlsx)))
            {
                Eq(1, package.Workbook.Worksheets.Count, "one sheet");
                ExcelWorksheet sheet = package.Workbook.Worksheets["Links"];
                Check(sheet != null, "called Links");
                Check(((string)sheet.Cells["A1"].Value).StartsWith("Links on Next Open and as Last Saved"), "title");
                string headers = string.Join("|", Enumerable.Range(1, 12).Select(c => sheet.Cells[2, c].Value as string));
                Eq("Project Name|Link Id|Link Type|Link Name|Path Type|Link State|Path on Next Open|Last Saved Path|File Date|Changed|Link Stable Id|Master File Path",
                    headers, "headers");
                Eq("101,102,103,300,400,200", string.Join(",", Enumerable.Range(3, 6).Select(r => sheet.Cells[r, 2].Value as string)),
                    "rows by link type, as the tabs are");
                Eq(null, sheet.Cells[9, 2].Value, "nothing after the last link");
                Eq(@"Z\Z.rvt", sheet.Cells[3, 7].Value, "path on next open");
                Eq(@"Links\A.rvt", sheet.Cells[3, 8].Value, "with the last saved path beside it");
                Eq("Relative", sheet.Cells[3, 5].Value, "path type");
                Eq("Dwg", sheet.Cells[6, 3].Value, "link type");
                Eq("Unloaded", sheet.Cells[6, 6].Value, "the CAD link's state on next open");
                Eq("Yes", sheet.Cells[6, 10].Value, "the changed CAD link is marked");
                Eq("", (sheet.Cells[3, 10].Value ?? "").ToString(), "an unchanged link isn't");
                Eq("FFFFF8E1", sheet.Cells[6, 1].Style.Fill.BackgroundColor.Rgb, "changed rows are amber");
                Check(sheet.Cells[3, 1].Style.Fill.PatternType == OfficeOpenXml.Style.ExcelFillStyle.None, "unchanged rows aren't");
                Eq("01-Jan-2026 10:00", sheet.Cells[3, 9].Value, "file date");
                Eq(Host, sheet.Cells[3, 12].Value, "master file path");
                Check(sheet.Column(11).Hidden && sheet.Column(12).Hidden, "id and path columns hidden");
            }
        }
    }
}
