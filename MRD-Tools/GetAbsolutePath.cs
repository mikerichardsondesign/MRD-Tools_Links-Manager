using System.IO;

namespace MRDTools
{
    public class GetAbsolutePath
    {
        public static string LinksAbsolutePath(string linkFilePath, string masterFilePath)
        {
            //keynote and classification files are here by default. how to deal with this?
            //C:\ProgramData\Autodesk\RVT 2015\Libraries\UK
            //UniformatClassifications.txt
            //RevitKeynotes_GBR.txt
            //RevitKeynotes_Metric.txt
            //for now just remove last saved date

            //set up string to return at end
            string linkAbsolutePath = "";

            //get filepath of master project
            //possibly validate that has a filename on end by using file.exists() or directory.exists()
            string filePathNoFilename = Path.GetDirectoryName(masterFilePath);

            //test links file path to see if it's relative or absolute (path.ispathrooted?)
            // IsPathRooted('C:\mydir\myfile.ext') returns True 
            // IsPathRooted('\\myPc\mydir\myfile') returns True 
            // IsPathRooted('mydir\sudir\') returns False
            // how does ../ react to ispathrooted?
            bool rootedLinkPath = Path.IsPathRooted(linkFilePath);


            //if path is not rooted then combine path
            if (rootedLinkPath == false)
            {
                linkAbsolutePath = Path.Combine(filePathNoFilename, linkFilePath);
            }
            else
            {
                linkAbsolutePath = linkFilePath;
            }

            return linkAbsolutePath;
        }
    }
}
         
    

