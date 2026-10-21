using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace MRDTools
{
    /// <summary>
    /// Links Manager for batch repathing without opening
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MRDLinksManagerBatch : IExternalCommand
    {
        static AddInId appId = new AddInId(new Guid("5A915920-4A52-4A00-A743-EBBF7072F665"));
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Form Creation
            /// <summary>
            /// create and show form
            /// </summary>

            using (MRDLinksBatchForm thisForm = new MRDLinksBatchForm(commandData))
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
