using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using MCPAccelerator.AutoCAD.AutoCADCommands.Converter;
using MCPAccelerator.Domain.BuildingModel;
using MCPAccelerator.Utils.GeometryModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

// NETLOAD
// C:\Users\Ofer\Desktop\MCPaccelerator\src\AutoCAD\AutoCADCommands\bin\Debug\net10.0\MCPAccelerator.AutoCAD.AutoCADCommands.dll
// AutoCAD Text Window (F2)

namespace MCPAccelerator.AutoCAD.AutoCADCommands
{
    public class Commands
    {
        // =====================================================================
        // Building creation
        // =====================================================================

        [CommandMethod("OL_CREATE_BUILDING")]
        public static void CreateBuilding()
        {
            var editor = GetEditor();

            // Prompt building name
            var nameResult = editor.GetString(
                new PromptStringOptions("\nEnter building name (or press Enter for default): ")
                { AllowSpaces = true });

            if (nameResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nCancelled.");
                return;
            }

            // Prompt unit system
            var unitKeywords = new PromptKeywordOptions("\nChoose unit system")
            {
                AllowNone = true
            };
            unitKeywords.Keywords.Add("Inches");
            unitKeywords.Keywords.Add("Meters");
            unitKeywords.Keywords.Default = "Inches";
            var unitResult = editor.GetKeywords(unitKeywords);

            if (unitResult.Status != PromptStatus.OK && unitResult.Status != PromptStatus.None)
            {
                editor.WriteMessage("\nCancelled.");
                return;
            }

            UnitSystem units = unitResult.StringResult == "Meters"
                ? UnitSystem.Meters()
                : UnitSystem.Inches();

            var building = BuildingSession.Add(
                string.IsNullOrWhiteSpace(nameResult.StringResult) ? null : nameResult.StringResult,
                units);

            editor.WriteMessage($"\nBuilding '{building.Name}' created ({building.Units.Unit}).");

            // Prompt number of stories
            var storyCountResult = editor.GetInteger(
                new PromptIntegerOptions("\nHow many stories? [default: 1]: ")
                { DefaultValue = 1, AllowNone = true });

            if (storyCountResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nCancelled.");
                return;
            }

            int storyCount = storyCountResult.Value;

            // Create each story
            for (int i = 0; i < storyCount; i++)
            {
                string defaultName = GetOrdinalName(i + 1) + " Story";
                double defaultBot = i * building.Units.DefaultStoryHeight;
                double defaultTop = (i + 1) * building.Units.DefaultStoryHeight;

                // Prompt story name
                var storyNameResult = editor.GetString(
                    new PromptStringOptions($"\nStory {i + 1} name (or Enter for '{defaultName}'): ")
                    { AllowSpaces = true });

                if (storyNameResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nCancelled.");
                    return;
                }

                string storyName = string.IsNullOrWhiteSpace(storyNameResult.StringResult)
                    ? defaultName
                    : storyNameResult.StringResult;

                // Prompt elevations
                var elevResult = editor.GetString(
                    new PromptStringOptions(
                        $"\nEnter elevations separated by spaces (or Enter for default: {defaultBot} {defaultTop}): ")
                    { AllowSpaces = true });

                if (elevResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nCancelled.");
                    return;
                }

                List<double> elevations;
                if (string.IsNullOrWhiteSpace(elevResult.StringResult))
                {
                    elevations = [defaultBot, defaultTop];
                }
                else
                {
                    elevations = ParseElevations(elevResult.StringResult);
                    if (elevations == null)
                    {
                        editor.WriteMessage("\nInvalid input. Must be numbers separated by spaces.");
                        return;
                    }
                    if (elevations.Count < 2)
                    {
                        editor.WriteMessage("\nA story requires at least 2 elevations (bot and top).");
                        return;
                    }
                }

                elevations.Sort();
                double bot = elevations.First();
                double top = elevations.Last();
                var intermediates = elevations.Count > 2
                    ? elevations.Skip(1).Take(elevations.Count - 2)
                    : null;

                building.AddStory(bot, top, storyName, intermediates);

                editor.WriteMessage($"\n  '{storyName}' added: elevations [{string.Join(", ", elevations)}]");
            }

            editor.WriteMessage($"\n\nBuilding '{building.Name}' ready with {building.Stories.Count} story(ies).");
        }

        // =====================================================================
        // Select floor plan elements for a story
        // =====================================================================

        [CommandMethod("OL_SELECT_FLOOR_PLAN")]
        public static void SelectFloorPlan()
        {
            var editor = GetEditor();

            var context = PromptBuildingAndStory("floor plan elements");
            if (context == null) return;

            // Single selection — user selects all elements at once
            var document = Application.DocumentManager.MdiActiveDocument;
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nSelect floor plan elements (walls, windows, doors), then press Enter: "
            };

            var selectionResult = editor.GetSelection(options);
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nSelection cancelled.");
                return;
            }

            // Filter into wall, window, door polylines
            var wallPolylines = new List<Polyline>();
            var windowPolylines = new List<Polyline>();
            var doorPolylines = new List<Polyline>();

            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objectId in selectionResult.Value.GetObjectIds())
                {
                    var entity = transaction.GetObject(objectId, OpenMode.ForRead) as Polyline;
                    if (entity == null || !entity.Closed)
                        continue;

                    string layer = entity.Layer;
                    if (layer.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0)
                        wallPolylines.Add(entity);
                    else if (layer.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0)
                        windowPolylines.Add(entity);
                    else if (layer.IndexOf("door", StringComparison.OrdinalIgnoreCase) >= 0)
                        doorPolylines.Add(entity);
                }

                transaction.Commit();
            }

            editor.WriteMessage($"\nFound: {wallPolylines.Count} wall(s), {windowPolylines.Count} window(s), {doorPolylines.Count} door(s)");

            if (wallPolylines.Count == 0 && windowPolylines.Count == 0 && doorPolylines.Count == 0)
            {
                editor.WriteMessage("\nNo matching polylines found in selection.");
                return;
            }

            // Convert — merges walls around openings, creates domain objects
            var result = FloorPlanConverter.Convert(
                context.Value.building, context.Value.story,
                wallPolylines, windowPolylines, doorPolylines);

            editor.WriteMessage($"\n\nAdded to '{context.Value.story.Name}' in '{context.Value.building.Name}':");
            editor.WriteMessage($"\n  Walls:   {result.WallsCreated}");
            editor.WriteMessage($"\n  Windows: {result.WindowsCreated}");
            editor.WriteMessage($"\n  Doors:   {result.DoorsCreated}");
            if (result.OpeningsSkipped > 0)
                editor.WriteMessage($"\n  Skipped: {result.OpeningsSkipped} opening(s) (validation failed)");
        }

        // =====================================================================
        // Helpers — prompts
        // =====================================================================

        /// <summary>
        /// Prompts the user to pick a building and a story within it.
        /// Returns null if cancelled or no buildings/stories exist.
        /// </summary>
        private static (Building building, Story story)? PromptBuildingAndStory(string elementType)
        {
            var editor = GetEditor();

            if (BuildingSession.Buildings.Count == 0)
            {
                editor.WriteMessage("\nNo buildings exist. Run OL_CREATE_BUILDING first.");
                return null;
            }

            // Pick building
            Building building;
            if (BuildingSession.Buildings.Count == 1)
            {
                building = BuildingSession.Buildings[0];
                editor.WriteMessage($"\nUsing building '{building.Name}'.");
            }
            else
            {
                editor.WriteMessage("\nAvailable buildings:");
                for (int i = 0; i < BuildingSession.Buildings.Count; i++)
                    editor.WriteMessage($"\n  [{i + 1}] {BuildingSession.Buildings[i].Name}");

                var pickResult = editor.GetInteger(
                    new PromptIntegerOptions($"\nSelect building number to assign {elementType}: "));

                if (pickResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nCancelled.");
                    return null;
                }

                int idx = pickResult.Value - 1;
                if (idx < 0 || idx >= BuildingSession.Buildings.Count)
                {
                    editor.WriteMessage("\nInvalid building number.");
                    return null;
                }

                building = BuildingSession.Buildings[idx];
            }

            if (building.Stories.Count == 0)
            {
                editor.WriteMessage($"\nBuilding '{building.Name}' has no stories.");
                return null;
            }

            // Pick story
            Story story;
            if (building.Stories.Count == 1)
            {
                story = building.Stories[0];
                editor.WriteMessage($"\nUsing story '{story.Name}'.");
            }
            else
            {
                editor.WriteMessage("\nAvailable stories:");
                for (int i = 0; i < building.Stories.Count; i++)
                {
                    var s = building.Stories[i];
                    editor.WriteMessage($"\n  [{i + 1}] {s.Name} (elevation {s.BotLevel.Elevation} - {s.TopLevel.Elevation})");
                }

                var pickResult = editor.GetInteger(
                    new PromptIntegerOptions($"\nSelect story number to assign {elementType}: "));

                if (pickResult.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("\nCancelled.");
                    return null;
                }

                int idx = pickResult.Value - 1;
                if (idx < 0 || idx >= building.Stories.Count)
                {
                    editor.WriteMessage("\nInvalid story number.");
                    return null;
                }

                story = building.Stories[idx];
            }

            return (building, story);
        }

        // =====================================================================
        // Helpers — utilities
        // =====================================================================

        private static Editor GetEditor()
        {
            return Application.DocumentManager.MdiActiveDocument.Editor;
        }

        private static List<double> ParseElevations(string input)
        {
            var parts = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var elevations = new List<double>();

            foreach (var part in parts)
            {
                if (!double.TryParse(part, out double value))
                    return null;
                elevations.Add(value);
            }

            return elevations;
        }

        private static string GetOrdinalName(int number)
        {
            if (number % 100 >= 11 && number % 100 <= 13)
                return number + "th";

            switch (number % 10)
            {
                case 1: return number + "st";
                case 2: return number + "nd";
                case 3: return number + "rd";
                default: return number + "th";
            }
        }
    }
}
