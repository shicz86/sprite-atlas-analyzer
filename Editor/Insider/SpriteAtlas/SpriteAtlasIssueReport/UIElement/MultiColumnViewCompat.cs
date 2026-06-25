using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement
{
    public enum MultiColumnSortBehavior
    {
        Default,
        Custom
    }

    static class MultiColumnViewCompat
    {
        const string k_CellUxml =
            "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/CellLabelWithIcon/CellLabelWithIcon.uxml";

        static readonly PropertyInfo s_ColumnComparisonProperty =
            typeof(Column).GetProperty("comparison", BindingFlags.Instance | BindingFlags.Public);

        static readonly FieldInfo s_ColumnComparisonField =
            typeof(Column).GetField("comparison", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        static readonly ConditionalWeakTable<Column, Comparison<int>> s_FallbackColumnComparisons = new();

        public static Length PixelWidth(float pixels) => new Length(pixels, LengthUnit.Pixel);

        public static string GetBindingPath(Column column)
        {
            return column.name switch
            {
                "Name" => "name",
                "Sprites" => "sprites",
                "Total Memory" => "totalMemory",
                "Unused Memory" => "unusedMemory",
                "Area Usage %" => "usage",
                "Total Area" => "totalArea",
                "Used Area" => "usedArea",
                "Width" => "width",
                "Height" => "height",
                "Pages" => "pages",
                "Packing Mode" => "packingMode",
                "Texture Format" => "textureFormat",
                "TextureFormat" => "textureFormat",
                "Count" => "count",
                "Page Count" => "pages",
                _ => column.name
            };
        }

        /// <summary>
        /// Unity 2022.3 enables header sorting via sortingEnabled; Unity 6+ also uses sortingMode.
        /// </summary>
        public static void EnableColumnSorting(MultiColumnListView table, MultiColumnSortBehavior behavior = MultiColumnSortBehavior.Default)
        {
            if (table == null)
                return;

            table.sortingEnabled = true;
            SetSortingModeIfSupported(table, behavior);
            MarkColumnsSortable(table.columns);
            table.RegisterCallback<AttachToPanelEvent>(_ => EnsureSortingEnabled(table, behavior));
        }

        public static void EnableColumnSorting(MultiColumnTreeView table, MultiColumnSortBehavior behavior = MultiColumnSortBehavior.Custom)
        {
            if (table == null)
                return;

            table.sortingEnabled = true;
            SetSortingModeIfSupported(table, behavior);
            MarkColumnsSortable(table.columns);
            table.RegisterCallback<AttachToPanelEvent>(_ => EnsureSortingEnabled(table, behavior));
        }

        public static void SetColumnComparison(Column column, Comparison<int> comparison)
        {
            if (column == null || comparison == null)
                return;

            if (s_ColumnComparisonProperty != null)
            {
                s_ColumnComparisonProperty.SetValue(column, comparison);
                return;
            }

            if (s_ColumnComparisonField != null)
            {
                s_ColumnComparisonField.SetValue(column, comparison);
                return;
            }

            s_FallbackColumnComparisons.AddOrUpdate(column, comparison);
        }

        public static Comparison<int> GetColumnComparison(Column column)
        {
            if (column == null)
                return null;

            if (s_ColumnComparisonProperty != null)
                return s_ColumnComparisonProperty.GetValue(column) as Comparison<int>;

            if (s_ColumnComparisonField != null)
                return s_ColumnComparisonField.GetValue(column) as Comparison<int>;

            s_FallbackColumnComparisons.TryGetValue(column, out var comparison);
            return comparison;
        }

        public static bool HasActiveSort(MultiColumnListView table) => HasActiveSort(table?.sortedColumns);

        public static bool HasActiveSort(MultiColumnTreeView table) => HasActiveSort(table?.sortedColumns);

        static bool HasActiveSort(IEnumerable<SortColumnDescription> sortedColumns)
        {
            if (sortedColumns == null)
                return false;

            foreach (var _ in sortedColumns)
                return true;

            return false;
        }

        public static List<SortColumnDescription> CopySortedColumns(IEnumerable<SortColumnDescription> sortedColumns)
        {
            var copy = new List<SortColumnDescription>();
            if (sortedColumns == null)
                return copy;

            foreach (var sorted in sortedColumns)
            {
                copy.Add(CloneSortColumnDescription(sorted));
            }

            return copy;
        }

        public static void RestoreSortColumnDescriptions(MultiColumnListView table, IReadOnlyList<SortColumnDescription> saved)
        {
            if (table == null || saved == null)
                return;

            table.sortColumnDescriptions.Clear();
            for (int i = 0; i < saved.Count; ++i)
                table.sortColumnDescriptions.Add(CloneSortColumnDescription(saved[i]));
            table.MarkDirtyRepaint();
        }

        public static void RestoreSortColumnDescriptions(MultiColumnTreeView table, IReadOnlyList<SortColumnDescription> saved)
        {
            if (table == null || saved == null)
                return;

            table.sortColumnDescriptions.Clear();
            for (int i = 0; i < saved.Count; ++i)
                table.sortColumnDescriptions.Add(CloneSortColumnDescription(saved[i]));
            table.MarkDirtyRepaint();
        }

        static SortColumnDescription CloneSortColumnDescription(SortColumnDescription sorted)
        {
            var description = new SortColumnDescription
            {
                columnIndex = sorted.columnIndex,
                columnName = GetSortColumnName(sorted),
                direction = sorted.direction
            };
            return description;
        }

        static string GetSortColumnName(SortColumnDescription sorted)
        {
            if (!string.IsNullOrEmpty(sorted.columnName))
                return sorted.columnName;

            var column = sorted.column;
            if (column == null)
                return string.Empty;

            return !string.IsNullOrEmpty(column.name) ? column.name : column.title;
        }

        public static void SortListBySortedColumns<T>(MultiColumnListView table, List<T> list)
        {
            if (table == null || list == null || list.Count < 2 || !HasActiveSort(table))
                return;

            var indexed = new List<(T item, int index)>(list.Count);
            for (int i = 0; i < list.Count; ++i)
                indexed.Add((list[i], i));

            indexed.Sort((left, right) =>
            {
                foreach (var sorted in table.sortedColumns)
                {
                    var comparison = GetColumnComparison(sorted.column);
                    if (comparison == null)
                        comparison = GetColumnComparison(FindColumn(table, sorted));

                    if (comparison == null)
                        continue;

                    int result = comparison(left.index, right.index);
                    if (result != 0)
                        return result * (sorted.direction == SortDirection.Ascending ? 1 : -1);
                }

                return left.index.CompareTo(right.index);
            });

            for (int i = 0; i < list.Count; ++i)
                list[i] = indexed[i].item;
        }

        static Column FindColumn(MultiColumnListView table, SortColumnDescription sorted)
        {
            if (sorted.column != null)
                return sorted.column;

            if (table?.columns == null)
                return null;

            for (int i = 0; i < table.columns.Count; ++i)
            {
                var column = table.columns[i];
                if (!string.IsNullOrEmpty(sorted.columnName) &&
                    (column.name == sorted.columnName || column.title == sorted.columnName))
                    return column;

                if (sorted.columnIndex >= 0 && i == sorted.columnIndex)
                    return column;
            }

            return null;
        }

        public static void OnListColumnSortingChanged<T>(MultiColumnListView table, List<T> list)
        {
            if (table == null || list == null || list.Count < 2)
                return;

            if (s_ApplyingListSort.TryGetValue(table, out var gate) && gate.applying)
                return;

            var savedSort = CopySortedColumns(table.sortedColumns);
            if (savedSort.Count == 0)
                return;

            SortListBySortedColumns(table, list);

            gate = new SortApplyingGate { applying = true };
            s_ApplyingListSort.AddOrUpdate(table, gate);
            table.schedule.Execute(() =>
            {
                try
                {
                    if (table.panel == null)
                        return;

                    RestoreSortColumnDescriptions(table, savedSort);
                    table.RefreshItems();
                }
                finally
                {
                    gate.applying = false;
                }
            }).StartingIn(0);
        }

        sealed class SortApplyingGate
        {
            public bool applying;
        }

        static readonly ConditionalWeakTable<MultiColumnListView, SortApplyingGate> s_ApplyingListSort = new();

        static void EnsureSortingEnabled(MultiColumnListView table, MultiColumnSortBehavior behavior)
        {
            table.sortingEnabled = true;
            SetSortingModeIfSupported(table, behavior);
            MarkColumnsSortable(table.columns);
        }

        static void EnsureSortingEnabled(MultiColumnTreeView table, MultiColumnSortBehavior behavior)
        {
            table.sortingEnabled = true;
            SetSortingModeIfSupported(table, behavior);
            MarkColumnsSortable(table.columns);
        }

        static void SetSortingModeIfSupported(BaseVerticalCollectionView table, MultiColumnSortBehavior behavior)
        {
            var sortingModeType = Type.GetType("UnityEngine.UIElements.ColumnSortingMode, UnityEngine.UIElementsModule");
            if (sortingModeType == null)
                return;

            var sortingModeProp = table.GetType().GetProperty("sortingMode", BindingFlags.Instance | BindingFlags.Public);
            if (sortingModeProp == null || sortingModeProp.PropertyType != sortingModeType)
                return;

            var valueName = behavior == MultiColumnSortBehavior.Custom ? "Custom" : "Default";
            var value = Enum.Parse(sortingModeType, valueName);
            sortingModeProp.SetValue(table, value);
        }

        public static void MarkColumnsSortable(Columns columns)
        {
            if (columns == null)
                return;

            for (int i = 0; i < columns.Count; ++i)
                columns[i].sortable = true;
        }

        public static VisualElement MakeCellLabelWithIcon()
        {
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_CellUxml);
            return template != null ? template.CloneTree() : new CellLabelWithIcon();
        }
    }
}
