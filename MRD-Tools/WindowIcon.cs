using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MRDTools
{
    /// <summary>
    /// The links manager window's icon, embedded in the DLL as a plain .ico (see the csproj).
    /// </summary>
    internal static class WindowIcon
    {
        public static ImageSource Load()
        {
            try
            {
                using (var stream = typeof(WindowIcon).Assembly.GetManifestResourceStream("MRDTools.MRD-Links.ico"))
                    return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
