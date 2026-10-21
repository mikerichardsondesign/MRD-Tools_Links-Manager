using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace MRDTools
{
    public class AvailabilityTrueAlways : IExternalCommandAvailability
    {
        /// <summary>
        /// Button Availability in Zero Doc state
        /// </summary>
        /// 

        public bool IsCommandAvailable(UIApplication a, CategorySet b)
        {
            return true;
        }
    }
}
