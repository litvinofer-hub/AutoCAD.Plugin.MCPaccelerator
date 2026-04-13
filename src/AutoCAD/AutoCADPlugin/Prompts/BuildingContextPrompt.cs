using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.EditorInput;
using MCPAccelerator.AutoCAD.AutoCADPlugin.Utils;
using MCPAccelerator.Domain.BuildingModel;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin.Prompts
{
    /// <summary>
    /// Reusable command-line prompts for picking a <see cref="Building"/> and/or
    /// <see cref="Story"/> from the current <see cref="BuildingSession"/>.
    ///
    /// Behaviour:
    /// - If only one item exists, it is auto-selected (no prompt shown).
    /// - If multiple items exist, a numbered list is printed and the user
    ///   enters an index.
    /// - Returns <c>null</c> when the session is empty, the user cancels,
    ///   or the entered number is out of range.
    ///
    /// The <c>purpose</c> parameter is spliced into the prompt text so the
    /// user sees *why* they're picking (e.g. "Select building number to print:").
    /// </summary>
    public static class BuildingContextPrompt
    {
        /// <summary>
        /// Prompts the user to choose a building from the session.
        /// </summary>
        /// <param name="purpose">
        /// Verb or short phrase describing the action, shown in the prompt
        /// (e.g. "print", "delete", "assign floor plan elements").
        /// </param>
        /// <returns>The selected <see cref="Building"/>, or <c>null</c> if
        /// no buildings exist or the user cancelled.</returns>
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

        /// <summary>
        /// Prompts the user to choose a building and then a story within it.
        /// Calls <see cref="PickBuilding"/> first; if that succeeds, shows
        /// the building's stories with their elevation ranges.
        /// </summary>
        /// <param name="purpose">
        /// Short phrase shown in prompts (e.g. "floor plan elements",
        /// "axial system"). Appears as "Select building number to assign {purpose}"
        /// and "Select story number to assign {purpose}".
        /// </param>
        /// <returns>A (building, story) tuple, or <c>null</c> if cancelled
        /// or the building has no stories.</returns>
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
        /// Generic numbered-list picker for the AutoCAD command line.
        ///
        /// - 1 item  → returns it immediately (no prompt).
        /// - N items → prints a numbered list, asks for an integer,
        ///             validates the index, and returns the chosen item.
        ///
        /// The caller provides a <paramref name="label"/> function that
        /// turns each item into the display string shown in the list
        /// (e.g. <c>b => b.Name</c>).
        /// </summary>
        /// <returns>The selected item, or <c>null</c> on cancel / invalid input.</returns>
        private static T PickFromList<T>(
            IReadOnlyList<T> items,
            string listHeader,
            string prompt,
            Func<T, string> label) where T : class
        {
            var editor = AcadContext.Editor;

            // Single item — skip the prompt entirely.
            if (items.Count == 1)
                return items[0];

            // Print numbered list.
            editor.WriteMessage($"\n{listHeader}:");
            for (int i = 0; i < items.Count; i++)
                editor.WriteMessage($"\n  [{i + 1}] {label(items[i])}");

            // Ask the user for a 1-based index.
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
