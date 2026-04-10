using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts
{
    /// <summary>
    /// Prompts the user to pick a Building and then a Story within it.
    /// Auto-selects if there's only one option; otherwise lists choices and
    /// asks for a number. Returns null on cancel, invalid input, or when
    /// no buildings/stories exist.
    /// </summary>
    public static class BuildingContextPrompt
    {
        /// <summary>
        /// Prompts the user to pick a building from the session.
        /// Auto-selects if there's only one. Returns null on cancel,
        /// invalid input, or when no buildings exist.
        /// </summary>
        public static Building PickBuilding(string purpose)
        {
            var editor = AcadContext.Editor;

            if (BuildingSession.Buildings.Count == 0)
            {
                editor.WriteMessage("\nNo buildings exist. Run OL_CREATE_BUILDING first.");
                return null;
            }

            var building = PickFromList(
                BuildingSession.Buildings,
                listHeader: "Available buildings",
                prompt: $"Select building number to {purpose}: ",
                label: b => b.Name);
            if (building == null) return null;

            editor.WriteMessage($"\nUsing building '{building.Name}'.");
            return building;
        }

        public static (Building building, Story story)? PickBuildingAndStory(string purpose)
        {
            var editor = AcadContext.Editor;

            var building = PickBuilding($"assign {purpose}");
            if (building == null) return null;

            if (building.Stories.Count == 0)
            {
                editor.WriteMessage($"\nBuilding '{building.Name}' has no stories.");
                return null;
            }

            var story = PickFromList(
                building.Stories,
                listHeader: "Available stories",
                prompt: $"Select story number to assign {purpose}: ",
                label: s => $"{s.Name} (elevation {s.BotLevel.Elevation} - {s.TopLevel.Elevation})");
            if (story == null) return null;
            editor.WriteMessage($"\nUsing story '{story.Name}'.");

            return (building, story);
        }

        /// <summary>
        /// Generic single-item picker: auto-selects when there's only one item,
        /// otherwise prints a numbered list and asks the user for an index.
        /// </summary>
        private static T PickFromList<T>(
            IReadOnlyList<T> items,
            string listHeader,
            string prompt,
            Func<T, string> label) where T : class
        {
            var editor = AcadContext.Editor;

            if (items.Count == 1)
                return items[0];

            editor.WriteMessage($"\n{listHeader}:");
            for (int i = 0; i < items.Count; i++)
                editor.WriteMessage($"\n  [{i + 1}] {label(items[i])}");

            var result = editor.GetInteger(new PromptIntegerOptions("\n" + prompt));
            if (result.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\nCancelled.");
                return null;
            }

            int idx = result.Value - 1;
            if (idx < 0 || idx >= items.Count)
            {
                editor.WriteMessage("\nInvalid number.");
                return null;
            }

            return items[idx];
        }
    }
}
