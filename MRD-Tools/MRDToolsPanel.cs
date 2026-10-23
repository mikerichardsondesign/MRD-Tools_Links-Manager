using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Windows.Media.Imaging;

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
                System.Windows.MessageBox.Show("Assembly location can not be found", "Error");
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

            //one button opens the links manager, for one project or many
            PushButtonData btnDataLinksManNotOpen = new PushButtonData("LinksManager", " Links Manager ", addInPath, "MRDTools.MRDLinksManager");

            //for ribbon
            btnDataLinksManNotOpen.LargeImage = new BitmapImage(new Uri(Path.Combine(AddInDirectory, "MRD-Links-32x32.png"), UriKind.Absolute));

            //for quick access if necessary
            btnDataLinksManNotOpen.Image = new BitmapImage(new Uri(Path.Combine(AddInDirectory, "MRD-Links-16x16.png"), UriKind.Absolute));

            PushButton btnLinksManNotOpen = panelMRDE.AddItem(btnDataLinksManNotOpen) as PushButton;

            //help stuff
            btnLinksManNotOpen.ToolTip = "Manage Links Outside of Projects";
            btnLinksManNotOpen.LongDescription = "Review, Repath, Load and Unload the Links of One or More Revit Projects Without Having to Open Them";
            btnLinksManNotOpen.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, helpHtml));
            btnLinksManNotOpen.AvailabilityClassName = "MRDTools.AvailabilityTrueAlways";


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
