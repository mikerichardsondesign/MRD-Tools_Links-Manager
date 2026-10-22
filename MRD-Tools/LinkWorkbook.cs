using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MRDTools
{
    /// <summary>
    /// The Excel export: a Proposed Link Status sheet (links as they'll be the next time each project opens)
    /// and a Current Link Status sheet (links as last saved).
    /// </summary>
    internal static class LinkWorkbook
    {
        /// <param name="fileName">A new .xlsx file to create.</param>
        /// <param name="saved">Every link as last saved, in the order to list them.</param>
        /// <param name="desired">Revit, CAD and DWF links as desired, in the order to list them.</param>
        public static void Save(string fileName, IEnumerable<LinkRow> saved, IEnumerable<LinkRow> desired)
        {
            using (ExcelPackage package = new ExcelPackage(new FileInfo(fileName)))
            {
                ExcelWorksheet proposedSheet = package.Workbook.Worksheets.Add("Proposed Link Status");
                SetupSheet(proposedSheet, "Proposed Link Data Properties");
                int row = WriteRows(proposedSheet, 3, desired, proposed: true);
                // Other links can't be changed, so they're proposed as they are
                WriteRows(proposedSheet, row, saved.Where(r => r.Kind == LinkKind.Other), proposed: false);

                ExcelWorksheet currentSheet = package.Workbook.Worksheets.Add("Current Link Status");
                SetupSheet(currentSheet, "Current Link Data Properties");
                WriteRows(currentSheet, 3, saved, proposed: false);

                package.Save();
            }
        }

        private static int WriteRows(ExcelWorksheet sheet, int row, IEnumerable<LinkRow> links, bool proposed)
        {
            foreach (LinkRow l in links)
            {
                sheet.Cells[row, 1].Value = l.ProjectName;
                sheet.Cells[row, 2].Value = l.RefId;
                sheet.Cells[row, 3].Value = proposed ? l.DesiredName : l.LastSavedName;
                sheet.Cells[row, 4].Value = proposed ? l.DesiredState : l.LastSavedState;
                sheet.Cells[row, 5].Value = proposed ? l.DesiredPath : l.LastSavedPath;
                sheet.Cells[row, 6].Value = proposed ? "N/A" : l.LastSavedTime;
                sheet.Cells[row, 7].Value = l.LinkTypeName;
                sheet.Cells[row, 8].Value = l.StableId;
                sheet.Cells[row, 9].Value = l.ProjectPath;
                row++;
            }
            return row;
        }

        private static void SetupSheet(ExcelWorksheet sheet, string title)
        {
            sheet.Cells.Style.Font.Size = 10;
            sheet.Cells.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
            sheet.Cells.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

            int[] widths = { 37, 15, 37, 15, 50, 20, 20, 37, 50 };
            for (int i = 0; i < widths.Length; i++)
                sheet.Column(i + 1).Width = widths[i];

            sheet.Cells["A1"].Style.Font.Bold = true;
            sheet.Cells["A1"].Value = title;

            string[] headers = { "Project Name", "Link Id", "Link Name", "Link State", "Link Path", "Date", "Link Type", "Link Stable Id", "Master File Path" };
            for (int i = 0; i < headers.Length; i++)
            {
                sheet.Cells[2, i + 1].Style.Font.Bold = true;
                sheet.Cells[2, i + 1].Value = headers[i];
            }

            sheet.Cells["A1:I1"].Merge = true;
            sheet.Cells["A1:I1"].Style.Border.Top.Style = ExcelBorderStyle.Thick;
            sheet.Cells["A1:I1"].Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
            sheet.Cells["A1:I1"].Style.Border.Left.Style = ExcelBorderStyle.Thick;
            sheet.Cells["A1:I1"].Style.Border.Right.Style = ExcelBorderStyle.Thick;

            sheet.Cells["A2:I2"].AutoFilter = true;
            sheet.Column(8).Hidden = true;
            sheet.Column(9).Hidden = true;
            sheet.HeaderFooter.OddFooter.RightAlignedText = "Page &P of &N";
        }
    }
}
