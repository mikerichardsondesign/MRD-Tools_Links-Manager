using System.Drawing;

namespace MRDTools
{
    /// <summary>
    /// Window icon for the link manager forms. It's embedded as a plain .ico rather than kept in the
    /// .resx files, because serialised objects in a .resx stop the .NET Framework (Revit 2024) build
    /// compiling under dotnet build.
    /// </summary>
    internal static class FormIcon
    {
        public static Icon Load()
        {
            using (var stream = typeof(FormIcon).Assembly.GetManifestResourceStream("MRDTools.MRD-Links.ico"))
                return new Icon(stream);
        }
    }
}
