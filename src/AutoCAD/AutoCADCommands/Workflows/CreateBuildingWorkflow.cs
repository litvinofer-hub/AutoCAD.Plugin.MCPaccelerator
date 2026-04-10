using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADCommands.Utils;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;

namespace MCPAccelerator.AutoCAD.AutoCADCommands.Workflows
{
    /// <summary>
    /// Orchestrates the OL_CREATE_BUILDING command:
    /// name → unit system → number of stories → (per story: name + elevations).
    ///
    /// Instance class so intermediate state (editor, building under construction)
    /// can live as private fields instead of being threaded through parameters.
    /// </summary>
    public class CreateBuildingWorkflow
    {
        private readonly Editor _editor = AcadContext.Editor;
        private Building _building;

        public void Run()
        {
            string name = PromptName();
            if (name == null) return;

            UnitSystem units = PromptUnits();
            if (units == null) return;

            _building = BuildingSession.Add(name, units);
            _editor.WriteMessage($"\nBuilding '{_building.Name}' created ({_building.Units.Unit}).");

            int? storyCount = PromptStoryCount();
            if (storyCount == null) return;

            for (int i = 0; i < storyCount.Value; i++)
            {
                if (!CreateOneStory(i)) return;
            }

            _editor.WriteMessage(
                $"\n\nBuilding '{_building.Name}' ready with {_building.Stories.Count} story(ies).");
        }

        // -------------------------------------------------------------------
        // Step 1: building name
        // -------------------------------------------------------------------

        /// <summary>Returns the entered name (possibly empty for default), or null if cancelled.</summary>
        private string PromptName()
        {
            var result = _editor.GetString(
                new PromptStringOptions("\nEnter building name (or press Enter for default): ")
                { AllowSpaces = true });

            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            return result.StringResult ?? "";
        }

        // -------------------------------------------------------------------
        // Step 2: unit system
        // -------------------------------------------------------------------

        private UnitSystem PromptUnits()
        {
            var options = new PromptKeywordOptions("\nChoose unit system") { AllowNone = true };
            options.Keywords.Add("Inches");
            options.Keywords.Add("Meters");
            options.Keywords.Default = "Inches";

            var result = _editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            return result.StringResult == "Meters"
                ? UnitSystem.Meters()
                : UnitSystem.Inches();
        }

        // -------------------------------------------------------------------
        // Step 3: story count
        // -------------------------------------------------------------------

        private int? PromptStoryCount()
        {
            var result = _editor.GetInteger(
                new PromptIntegerOptions("\nHow many stories? [default: 1]: ")
                { DefaultValue = 1, AllowNone = true });

            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            return result.Value;
        }

        // -------------------------------------------------------------------
        // Step 4: per-story creation
        // -------------------------------------------------------------------

        /// <summary>
        /// Prompts for one story's name + elevations, then adds it to the building.
        /// Returns false to abort the entire command (cancel / invalid input).
        /// </summary>
        private bool CreateOneStory(int index)
        {
            string defaultName = Ordinal.For(index + 1) + " Story";
            double defaultBot = index * _building.Units.DefaultStoryHeight;
            double defaultTop = (index + 1) * _building.Units.DefaultStoryHeight;

            string storyName = PromptStoryName(index + 1, defaultName);
            if (storyName == null) return false;

            List<double> elevations = PromptElevations(defaultBot, defaultTop);
            if (elevations == null) return false;

            elevations.Sort();
            double bot = elevations.First();
            double top = elevations.Last();
            var intermediates = elevations.Count > 2
                ? elevations.Skip(1).Take(elevations.Count - 2)
                : null;

            _building.AddStory(bot, top, storyName, intermediates);
            _editor.WriteMessage(
                $"\n  '{storyName}' added: elevations [{string.Join(", ", elevations)}]");
            return true;
        }

        private string PromptStoryName(int storyNumber, string defaultName)
        {
            var result = _editor.GetString(
                new PromptStringOptions($"\nStory {storyNumber} name (or Enter for '{defaultName}'): ")
                { AllowSpaces = true });

            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            return string.IsNullOrWhiteSpace(result.StringResult)
                ? defaultName
                : result.StringResult;
        }

        /// <summary>
        /// Prompts for a space-separated elevation list. Returns the list, or null
        /// on cancel / invalid input / fewer than 2 elevations.
        /// </summary>
        private List<double> PromptElevations(double defaultBot, double defaultTop)
        {
            var result = _editor.GetString(
                new PromptStringOptions(
                    $"\nEnter elevations separated by spaces (or Enter for default: {defaultBot} {defaultTop}): ")
                { AllowSpaces = true });

            if (result.Status != PromptStatus.OK)
            {
                _editor.WriteMessage("\nCancelled.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(result.StringResult))
                return new List<double> { defaultBot, defaultTop };

            var elevations = ElevationParser.Parse(result.StringResult);
            if (elevations == null)
            {
                _editor.WriteMessage("\nInvalid input. Must be numbers separated by spaces.");
                return null;
            }

            if (elevations.Count < 2)
            {
                _editor.WriteMessage("\nA story requires at least 2 elevations (bot and top).");
                return null;
            }

            return elevations;
        }
    }
}
