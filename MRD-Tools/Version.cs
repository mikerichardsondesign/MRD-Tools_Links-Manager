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
            TaskDialog.Show("Version Control", "Version 2025.1.0");
            return Result.Succeeded;

            //Version 2015.1.0 - Initial release
            //Version 2015.1.1 - File last saved added
            //Version 2017.1.0 - Updated to Revit 2017 (revit api references and .net framework 4.5.2). Possibly open at last directory added?
            //Version 2018.1.0 - Updated to Revit 2018

            //Version 2019.1.0 - Export / Import Excel Paths. 
            //.net framework updated to 4.7.1 from 4.5 (revit 2019)

            //Version 2020.1.0 - Updated to Revit 2020 
            //Version 2021.1.0 - Updated to Revit 2021 
            //Version 2022.1.0 - Updated to Revit 2022
            //Version 2023.1.0 - Updated to Revit 2023
            //Version 2024.1.0 - Updated to Revit 2024
            //Version 2025.1.0 - Updated to Revit 2025. framework updated to .net 8. EPPlus added as a git package
        }
    }
}
