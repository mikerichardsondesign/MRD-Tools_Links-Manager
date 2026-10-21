using System;
using System.IO;

namespace MRDTools
{
    public class GetRelativePath
    {
        //try first
        //https://stackoverflow.com/questions/703281/getting-path-relative-to-the-current-working-directory

        public static string GetRelativePathUriA(string linkFilePath, string masterFilePath) //masterFilePath may need to be a folder
        {
            Uri pathUri = new Uri(linkFilePath);
            // Folders must end in a slash
            if (!masterFilePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                masterFilePath += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(masterFilePath);

            //make string
            string returnString = Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));

            if (returnString.StartsWith(@"..\"))
                returnString = returnString.Substring(3);

            return returnString;
        }
    }
}
