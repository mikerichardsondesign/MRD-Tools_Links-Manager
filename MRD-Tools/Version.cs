using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace MRDTools
{
    /// <summary>
    /// Version control. Can add button if necessary
    /// </summary>
    
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class Version : IExternalCommand
    {
        static AddInId appId = new AddInId(new Guid("46561824-3F99-4D25-9C0A-FCC50FFAF63C"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elementSet)
        {
            Autodesk.Revit.ApplicationServices.Application app = commandData.Application.Application;
            // Read from the assembly so it follows <Version> in the csproj
            System.Version version = typeof(Version).Assembly.GetName().Version;
            TaskDialog.Show("Version Control", $"Version {version.ToString(3)}");
            return Result.Succeeded;

            //Version 1.0.0 - Initial release
            //Version 2.0.0 - Updated to Revit 2017 (revit api references and .net framework 4.5.2).
            //Version 3.0.0 - Updated to Revit 2018
            //Version 4.0.0 - Export / Import Excel Paths. 
            //.net framework updated to 4.7.1 from 4.5 (revit 2019)
            //Version 5.0.0 - Updated to Revit 2020 
            //Version 6.0.0 - Updated to Revit 2021 
            //Version 7.0.0 - Updated to Revit 2022
            //Version 8.0.0 - Updated to Revit 2023
            //Version 9.0.0 - Updated to Revit 2024
            //Version 9.1.0 - One project for Revit 2024-2027 (net48 / net8 / net10). An interim build before 10.0.0.
            //Fixes opening reports and Excel files on .NET 8+, swapped message box titles, relative repaths
            //across drives or shares, and the batch summary reporting success when projects had failed
        }
    }
}
