using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MRDTools
{
    /// <summary>
    /// The Excel export: one Links sheet laid out like the window's list, with each link's path on next open beside
    /// its last saved path. Changed links are marked and highlighted as they are in the window.
    /// Its path on next open, path type and link state can be edited in Excel and read back with Read.
    /// </summary>
    internal static class LinkWorkbook
    {
        const string ProjectNameHeader = "Project Name";
        const string LinkIdHeader = "Link Id";
        const string LinkTypeHeader = "Link Type";
        const string PathTypeHeader = "Path Type";
        const string LinkStateHeader = "Link State";
        const string NextOpenHeader = "Path on Next Open";
        const string ProjectPathHeader = "Master File Path";

        static readonly string[] Headers =
        {
            ProjectNameHeader, LinkIdHeader, LinkTypeHeader, "Link Name", PathTypeHeader, LinkStateHeader,
            NextOpenHeader, "Last Saved Path", "File Date", "Changed", "Link Stable Id", ProjectPathHeader
        };

        static readonly int[] Widths = { 34, 12, 11, 34, 12, 16, 55, 55, 18, 10, 37, 50 };

        // The window's amber for changed links
        static readonly Color ChangedFill = Color.FromArgb(0xFF, 0xF8, 0xE1);

        /// <param name="fileName">A new .xlsx file to create.</param>
        /// <param name="links">The links to list, in the order they were read.</param>
        public static void Save(string fileName, IEnumerable<LinkRow> links)
        {
            // By project, as the projects were added, then Revit, CAD, DWF and Other links as in the window's tabs
            List<LinkRow> rows = links.ToList();
            List<string> projectOrder = rows.Select(r => r.ProjectPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            List<LinkRow> ordered = rows
                .OrderBy(r => projectOrder.FindIndex(p => string.Equals(p, r.ProjectPath, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(r => r.Kind)
                .ToList();

            using (ExcelPackage package = new ExcelPackage(new FileInfo(fileName)))
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets.Add("Links");
                SetupSheet(sheet, $"Links on Next Open and as Last Saved · {DateTime.Now:dd MMM yyyy HH:mm}"
                    + " · Edit Path on Next Open, Path Type or Link State, save, then bring the changes back with IMPORT FROM EXCEL");

                int row = 3;
                foreach (LinkRow l in ordered)
                {
                    sheet.Cells[row, 1].Value = l.ProjectName;
                    sheet.Cells[row, 2].Value = l.RefId;
                    sheet.Cells[row, 3].Value = l.LinkTypeName;
                    sheet.Cells[row, 4].Value = l.DesiredName;
                    sheet.Cells[row, 5].Value = l.DesiredPathType;
                    sheet.Cells[row, 6].Value = l.DesiredState;
                    sheet.Cells[row, 7].Value = l.DesiredPath;
                    sheet.Cells[row, 8].Value = l.LastSavedPath;
                    sheet.Cells[row, 9].Value = l.LastSavedTime;
                    sheet.Cells[row, 10].Value = l.IsDirty ? "Yes" : "";
                    sheet.Cells[row, 11].Value = l.StableId;
                    sheet.Cells[row, 12].Value = l.ProjectPath;
                    if (l.IsDirty)
                    {
                        sheet.Cells[row, 1, row, Headers.Length].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[row, 1, row, Headers.Length].Style.Fill.BackgroundColor.SetColor(ChangedFill);
                    }
                    row++;
                }

                package.Save();
            }
        }

        /// <summary>One link as a workbook lists it, perhaps edited in Excel since it was exported.</summary>
        internal sealed class WorkbookLink
        {
            /// <summary>The sheet row it's on, for saying which rows couldn't be used.</summary>
            public int Row;
            public string ProjectPath;
            public string ProjectName;
            public string RefId;
            public string LinkType;
            public string PathType;
            public string State;
            public string DesiredPath;
        }

        /// <summary>
        /// Reads the links back from a workbook made by Save. Columns are found by their headings, so they can be moved,
        /// and rows without a Link Id are skipped. The file is read even while Excel has it open, but only as it was last
        /// saved there. Throws InvalidDataException when it isn't a links workbook.
        /// </summary>
        public static List<WorkbookLink> Read(string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var package = new ExcelPackage(stream))
            {
                ExcelWorksheet sheet = package.Workbook.Worksheets.FirstOrDefault(s => s.Name == "Links") ?? package.Workbook.Worksheets.FirstOrDefault();
                if (sheet?.Dimension == null) throw new InvalidDataException("The workbook is empty.");

                // The headings are on row 2 of an export, but look a little further in case rows were added above them
                int lastColumn = sheet.Dimension.End.Column;
                int headerRow = 0;
                Dictionary<string, int> columns = null;
                for (int r = 1; r <= Math.Min(10, sheet.Dimension.End.Row) && headerRow == 0; r++)
                {
                    var found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int c = 1; c <= lastColumn; c++)
                    {
                        string heading = CellText(sheet, r, c);
                        if (heading.Length > 0 && !found.ContainsKey(heading)) found[heading] = c;
                    }
                    if (found.ContainsKey(LinkIdHeader) && found.ContainsKey(NextOpenHeader))
                    {
                        headerRow = r;
                        columns = found;
                    }
                }
                if (headerRow == 0)
                    throw new InvalidDataException($"It has no {LinkIdHeader} and {NextOpenHeader} columns, so it isn't a links workbook.");

                string Cell(int r, string heading) => columns.TryGetValue(heading, out int c) ? CellText(sheet, r, c) : "";

                var links = new List<WorkbookLink>();
                for (int r = headerRow + 1; r <= sheet.Dimension.End.Row; r++)
                {
                    string refId = Cell(r, LinkIdHeader);
                    if (refId.Length == 0) continue;
                    links.Add(new WorkbookLink
                    {
                        Row = r,
                        ProjectPath = Cell(r, ProjectPathHeader),
                        ProjectName = Cell(r, ProjectNameHeader),
                        RefId = refId,
                        LinkType = Cell(r, LinkTypeHeader),
                        PathType = Cell(r, PathTypeHeader),
                        State = Cell(r, LinkStateHeader),
                        DesiredPath = Cell(r, NextOpenHeader),
                    });
                }
                return links;
            }
        }

        /// <summary>A cell's value as text, trimmed. An id Excel has turned into a number reads back without decimals.</summary>
        static string CellText(ExcelWorksheet sheet, int row, int column) =>
            (Convert.ToString(sheet.Cells[row, column].Value, CultureInfo.InvariantCulture) ?? "").Trim();

        private static void SetupSheet(ExcelWorksheet sheet, string title)
        {
            sheet.Cells.Style.Font.Size = 10;
            sheet.Cells.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            sheet.Cells.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            for (int i = 0; i < Widths.Length; i++)
                sheet.Column(i + 1).Width = Widths[i];

            string lastColumn = ExcelCellAddress.GetColumnLetter(Headers.Length);
            sheet.Cells["A1"].Style.Font.Bold = true;
            sheet.Cells["A1"].Value = title;
            sheet.Cells[$"A1:{lastColumn}1"].Merge = true;
            sheet.Cells[$"A1:{lastColumn}1"].Style.Border.Top.Style = ExcelBorderStyle.Thick;
            sheet.Cells[$"A1:{lastColumn}1"].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
            sheet.Cells[$"A1:{lastColumn}1"].Style.Border.Left.Style = ExcelBorderStyle.Thick;
            sheet.Cells[$"A1:{lastColumn}1"].Style.Border.Right.Style = ExcelBorderStyle.Thick;

            for (int i = 0; i < Headers.Length; i++)
            {
                sheet.Cells[2, i + 1].Style.Font.Bold = true;
                sheet.Cells[2, i + 1].Value = Headers[i];
            }
            sheet.Cells[$"A2:{lastColumn}2"].AutoFilter = true;
            sheet.View.FreezePanes(3, 1);

            // The stable id and the project's full path are kept for matching rows back to links, out of the way
            sheet.Column(11).Hidden = true;
            sheet.Column(12).Hidden = true;
            sheet.HeaderFooter.OddFooter.RightAlignedText = "Page &P of &N";

            // Printed landscape, one page wide, with the headings on every page
            sheet.PrinterSettings.Orientation = eOrientation.Landscape;
            sheet.PrinterSettings.FitToPage = true;
            sheet.PrinterSettings.FitToWidth = 1;
            sheet.PrinterSettings.FitToHeight = 0;
            sheet.PrinterSettings.RepeatRows = new ExcelAddress("$2:$2");
        }
    }
}
