﻿using Lumina.Excel;

namespace RotationSolver.GameData.Getters
{
    /// <summary>
    /// Abstract base class for getting Excel rows.
    /// </summary>
    /// <typeparam name="T">Type of the Excel row.</typeparam>
    internal abstract class ExcelRowGetter<T>(Lumina.GameData gameData) where T : ExcelRow
    {
        protected readonly Lumina.GameData _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));

        /// <summary>
        /// Gets the count of filtered items.
        /// </summary>
        public int Count { get; private set; } = 0;

        /// <summary>
        /// Determines whether the specified item should be added to the list.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>True if the item should be added; otherwise, false.</returns>
        protected abstract bool AddToList(T item);

        /// <summary>
        /// Converts the specified item to code.
        /// </summary>
        /// <param name="item">The item to convert.</param>
        /// <returns>The code representation of the item.</returns>
        protected abstract string ToCode(T item);

        /// <summary>
        /// Called before creating the list of items.
        /// </summary>
        protected virtual void BeforeCreating() { }

        /// <summary>
        /// Gets the code representation of the filtered items.
        /// </summary>
        /// <returns>The code representation of the filtered items.</returns>
        public string GetCode()
        {
            var items = _gameData.GetExcelSheet<T>();

            if (items == null) return string.Empty;
            BeforeCreating();

            var filteredItems = items.Where(AddToList);
            Count = filteredItems.Count();

            return string.Join("\n", filteredItems.Select(ToCode));
        }
    }
}