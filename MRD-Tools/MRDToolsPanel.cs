using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Media.Imaging;

[assembly: SupportedOSPlatform("windows6.1")]
namespace MRDTools
{
    class MRDToolsPanel : IExternalApplication
    {
        static string AddInPath()
        {
            string addInPath = "";

            try
            {
                addInPath = typeof(MRDToolsPanel).Assembly.Location;
            }
            catch (NotSupportedException)
            {
                System.Windows.MessageBox.Show("Error", "Assembly location can not be found");
            }
            
            return addInPath;
        }

        static string AddInDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        static string AddInDirectoryParent = Directory.GetParent(AddInDirectory).FullName;
             

        /// <summary>
        /// Tools Ribbon Start - Create Panels
        /// </summary>

        public Autodesk.Revit.UI.Result OnStartup(UIControlledApplication application)
        {



            string addInPath = AddInPath();

            string helpHtml = Path.Combine(AddInDirectoryParent, "External Access Links Manager.html");

            RibbonPanel panelMRDE = application.CreateRibbonPanel("MRD Tools");

            //create pull down button data
            PulldownButtonData pullBtnDataLinksManNotOpen = new PulldownButtonData("LinksManager", " Links Manager ");

            //create contextual help
            ContextualHelp contextHelp = new ContextualHelp(ContextualHelpType.Url, helpHtml);

            //for ribbon
            pullBtnDataLinksManNotOpen.LargeImage = new BitmapImage(new Uri(Path.Combine(AddInDirectory, "MRD-Links-32x32.png"), UriKind.Absolute));

            //for quick access if necessary
            pullBtnDataLinksManNotOpen.Image = new BitmapImage(new Uri(Path.Combine(AddInDirectory, "MRD-Links-16x16.png"), UriKind.Absolute));

            //create pull down button
            PulldownButton pullBtnLinksManNotOpen = panelMRDE.AddItem(pullBtnDataLinksManNotOpen) as PulldownButton;

            //help stuff
            pullBtnLinksManNotOpen.ToolTip = "Manage Links Outside of Projects";
            pullBtnLinksManNotOpen.LongDescription = "Use this Links Manager Tool to Review, Repath, Load and Unload Links in a Revit Project Without Having to Open the Project";          
            pullBtnLinksManNotOpen.SetContextualHelp(contextHelp);

            //standard tool
            PushButton btnLinksManNotOpenStandard = pullBtnLinksManNotOpen.AddPushButton(new PushButtonData("LinksManagerStandard", " Links Manager ", addInPath, "MRDTools.MRDLinksManager"));
            btnLinksManNotOpenStandard.ToolTip = "Use this Links Manager Tool to Review, Repath, Load and Unload Links in a Revit Project Without Having to Open the Project";
            btnLinksManNotOpenStandard.AvailabilityClassName = "MRDTools.AvailabilityTrueAlways";

            //batch tool
            PushButton btnLinksManNotOpenBatch = pullBtnLinksManNotOpen.AddPushButton(new PushButtonData("ElementsUpdateExcel", " Links Manager Batch", addInPath, "MRDTools.MRDLinksManagerBatch"));
            btnLinksManNotOpenBatch.ToolTip = "Use this Links Manager Tool to Batch Repath, Load and Unload Links in Revit Projects Without Having to Open them";
            btnLinksManNotOpenBatch.AvailabilityClassName = "MRDTools.AvailabilityTrueAlways";


            //Ribbon end - return result succeeded////////////////////////////////////////////////////////////
            //////////////////////////////////////////////////////////////////////////////////////////////////           

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
