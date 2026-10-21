using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace MRDTools
{
    /// <summary>
    /// Links Manager for repathing without opening
    /// </summary>

    [Transaction(TransactionMode.Manual)]
    public class MRDLinksManager : IExternalCommand
    {
        static AddInId appId = new AddInId(new Guid("48F3E8E8-F636-4009-8EEC-992B9A3F855B"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {                       
                #region Form Creation
                /// <summary>
                /// create and show form
                /// </summary>

                using (MRDLinksForm thisForm = new MRDLinksForm(commandData))
                    {
                        thisForm.ShowDialog();

                        if (thisForm.DialogResult == System.Windows.Forms.DialogResult.Cancel)
                        {
                            return Result.Cancelled;
                        }
                    }

                #endregion //Form Creation

                return Result.Succeeded;
        }                  
    }
}
