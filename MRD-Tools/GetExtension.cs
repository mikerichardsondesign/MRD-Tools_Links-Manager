using System;
using System.IO;

namespace MRDTools
{
    public class GetExtension
    {
        public static bool IfcRvtExtension(string source)
        {
            bool doubleExtension = false;

            string filenameNoExtension = Path.GetFileNameWithoutExtension(source);
            string ext1 = Path.GetExtension(filenameNoExtension);

            if (ext1 == ".ifc")
                doubleExtension = true;

            return doubleExtension;
        }

        public static bool RvtExtension(string source)
        {
            bool doubleExtension = false;

            string ext1 = Path.GetExtension(source);

            string filenameNoExtension = Path.GetFileNameWithoutExtension(source);
            string ext2 = filenameNoExtension.Substring(Math.Max(0, filenameNoExtension.Length - 5));

            if (ext1 == ".rvt" && ext2.StartsWith(".") != true)
                doubleExtension = true;

            return doubleExtension;
        }
    }
}
