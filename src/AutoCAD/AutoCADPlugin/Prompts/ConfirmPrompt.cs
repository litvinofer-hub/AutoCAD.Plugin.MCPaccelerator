using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts
{
    /// <summary>
    /// Yes/No confirmation prompt. Returns true only if the user explicitly
    /// picks Yes; any cancel or No returns false so destructive actions
    /// default to "do nothing."
    /// </summary>
    public static class ConfirmPrompt
    {
        public static bool Ask(string message, bool defaultYes = false)
        {
            var editor = AcadContext.Editor;

            var options = new PromptKeywordOptions("\n" + message) { AllowNone = true };
            options.Keywords.Add("Yes");
            options.Keywords.Add("No");
            options.Keywords.Default = defaultYes ? "Yes" : "No";

            var result = editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None)
                return false;

            // PromptStatus.None means the user pressed Enter and accepted the default
            if (result.Status == PromptStatus.None)
                return defaultYes;

            return result.StringResult == "Yes";
        }
    }
}
