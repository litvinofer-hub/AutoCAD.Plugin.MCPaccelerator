using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Utils
{
    /// <summary>
    /// Thin wrapper over AutoCAD's static globals so callers don't have to
    /// type <c>Application.DocumentManager.MdiActiveDocument.Editor</c>
    /// every time. Static because the underlying AutoCAD API is itself static.
    /// </summary>
    public static class AcadContext
    {
        public static Document Document => Application.DocumentManager.MdiActiveDocument;
        public static Editor Editor => Document.Editor;
    }
}
