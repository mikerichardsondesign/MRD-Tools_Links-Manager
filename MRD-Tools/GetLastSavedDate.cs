using System;
using System.IO;

namespace MRDTools
{
    public class GetLastSavedDate
    {
        public static string LinksLastSavedDate(string linksAbsolutePath)
        {
            DateTime tempLinkLastSaved = DateTime.Now;
            string linkLastSaved = "";

            //if date comes out as 01.Jan.1601 00-00 then error text 'Date Not Found'
            try
            {
                tempLinkLastSaved = File.GetLastWriteTime(linksAbsolutePath);
                linkLastSaved = tempLinkLastSaved.ToString("dd-MMM-yyyy HH:mm");
            }
            catch (Exception)
            {
                linkLastSaved = "Date Not Found";
            }
            
            if (linkLastSaved == "01-Jan-1601 00:00")
            {
                linkLastSaved = "Date Not Found";
            }

            return linkLastSaved;
        }
    }
}
