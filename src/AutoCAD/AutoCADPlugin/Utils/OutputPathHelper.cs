using System.IO;
using System.Reflection;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Utils
{
    /// <summary>
    /// Resolves the output/ folder relative to the solution root.
    /// </summary>
    public static class OutputPathHelper
    {
        public static string GetOutputDir()
        {
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var dir = new DirectoryInfo(dllDir);

            while (dir != null)
            {
                if (Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
                    return Path.Combine(dir.FullName, "output");

                dir = dir.Parent;
            }

            return Path.Combine(dllDir, "output");
        }
    }
}
