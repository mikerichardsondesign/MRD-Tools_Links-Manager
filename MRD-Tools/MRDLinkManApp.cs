using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Interop;

namespace MRDTools
{
    /// <summary>
    /// Links Manager for repathing without opening: one window for one project or many
    /// </summary>

    [Transaction(TransactionMode.Manual)]
    public class MRDLinksManager : IExternalCommand
    {
        static AddInId appId = new AddInId(new Guid("48F3E8E8-F636-4009-8EEC-992B9A3F855B"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var window = new LinksManagerWindow();

            // Owned by Revit's main window, so it opens centred on Revit and stays in front of it
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
