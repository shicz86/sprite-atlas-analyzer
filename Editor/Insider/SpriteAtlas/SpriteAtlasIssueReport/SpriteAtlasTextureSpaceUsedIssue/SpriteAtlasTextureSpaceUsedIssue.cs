using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class SpriteAtlasTextureSpaceUsedIssue : AnalyzerIssueReportBase
    {
        CommonSpriteAtlasIssueView m_View;
        SpriteAtlasReportTable m_Table;
        List<EditorAtlasInfo> m_Data;
        List<EditorAtlasInfo> m_Filtered = new();
        Column[] m_Columns;
        int m_Memory = 400;
        SpriteAtlasTextureSpaceUsedIssueSettings m_Settings;

        public SpriteAtlasTextureSpaceUsedIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            m_View = new CommonSpriteAtlasIssueView();
            m_View.styleSheets.Add(CommonStyleSheet.iconStyleSheet);
            m_Settings = new SpriteAtlasTextureSpaceUsedIssueSettings(m_Memory);
            m_Settings.onMemorySizeChanged += OnMemorySizeChanged;
            SetReportListItemName();
            SetReportListemCount("0");
            m_View.ShowTable(false, "Analyze has not been done yet.");


            m_Table = m_View.table;
            MultiColumnViewCompat.EnableColumnSorting(m_Table, MultiColumnSortBehavior.Default);
            m_Table.columnSortingChanged += OnColumnSortingChanged;
            m_Table.AddManipulator(new ContextualMenuManipulator(OnContextualMenuManipulator));
            table.selectionChanged += OnSelectionChanged;
            SetupColumns();
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            InspectObject(m_Filtered[table.selectedIndex].GetObject());
        }

        void OnContextualMenuManipulator(ContextualMenuPopulateEvent obj)
        {
            var menuStatus = m_Filtered.Count > 0 && table.selectedIndex >= 0 &&
                table.selectedIndex < table.itemsSource.Count ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            obj.menu.AppendAction("Reanalyze Atlas", a => RecheckAtlas(), menuStatus);
        }

        void RecheckAtlas()
        {
            var item = m_Filtered[table.selectedIndex].GetObject();
            if (item != null)
            {
                RequestCapture(new [] {AssetDatabase.GetAssetPath(item)});
            }
        }

        void SetReportListItemName()
        {
            SetReportListItemName($"Texture Space Wastage > {m_Memory} KB");
        }

        void SetupColumns()
        {
            m_Columns = new []
            {
                new Column
                {
                    title = "Name",
                    width = MultiColumnViewCompat.PixelWidth(80),
                    sortable = true
                },
                new Column
                {
                    title = "Unused Memory",
                    width = MultiColumnViewCompat.PixelWidth(80),
                    sortable = true
                }
            };

            m_Columns[0].makeCell = () =>
            {
                var ele = new CellLabelWithIcon();
                ele.SetIconClassName("spriteatlas-icon");
                return ele;
            };
            m_Columns[0].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).SetLabelText(m_Filtered[i].name);
            };
            MultiColumnViewCompat.SetColumnComparison(m_Columns[0], (a, b) =>
            {
                return string.Compare(m_Filtered[a].name, m_Filtered[b].name, StringComparison.OrdinalIgnoreCase);
            });
            m_Columns[1].makeCell = () =>
            {
                return new CellLabelWithIcon();
            };
            m_Columns[1].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).SetLabelText(EditorUtility.FormatBytes((long)m_Filtered[i].unusedMemory));
            };
            MultiColumnViewCompat.SetColumnComparison(m_Columns[1], (a, b) =>
            {
                return m_Filtered[a].unusedMemory.CompareTo(m_Filtered[b].unusedMemory);
            });
            for (int i = 0; i < m_Columns.Length; ++i)
                table.columns.Add(m_Columns[i]);

        }

        void OnColumnSortingChanged()
        {
            MultiColumnViewCompat.OnListColumnSortingChanged(table, m_Filtered);
        }

        MultiColumnListView table => m_Table.multiColumnListView;
        public override VisualElement reportContent => m_View;
        public override VisualElement settingsContent => m_Settings;
        public override string reportTitle => "Atlas Space Usage";

        protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
        {
            if (reportDataSource is SpriteAtlasDataSource dataSource)
            {
                await SetDataSourceProvider(dataSource);
            }
        }

        public async Task SetDataSourceProvider(SpriteAtlasDataSource dataSource)
        {
            if (dataSource?.data != null)
            {
                await SetDataSource(dataSource.data);
            }
        }

        async Task SetDataSource(List<EditorAtlasInfo> dataSource)
        {
            if (dataSource == null)
                return;
            m_Data = dataSource;
            isFilteringReport = true;
            m_View.ShowTable(false, "Filtering data in progress...");
            m_Filtered = await FilterDataAsync(m_Data);
            table.itemsSource = m_Filtered;
            table.Rebuild();
            isFilteringReport = false;
            SetReportListemCount($"{m_Filtered.Count}");
            m_View.ShowTable(m_Filtered.Count > 0, $"No Sprite Atlas with greater than {m_Memory}KB wastage found.");
        }

        async Task<List<EditorAtlasInfo>> FilterDataAsync(List<EditorAtlasInfo> dataSource)
        {
            var result = new List<EditorAtlasInfo>();
            var task = Task.Run(() =>
            {
                for(int i = 0; i < dataSource.Count; ++i)
                {
                    var atlasInfo = dataSource[i];
                    if (atlasInfo.isVariant)
                        continue;
                    if (atlasInfo.unusedMemory > m_Memory * 1024)
                    {
                        result.Add(atlasInfo);
                    }
                }
            });
            await task;
            return result;
        }

        async void OnMemorySizeChanged(int obj)
        {
            m_Memory = obj;
            SetReportListItemName();
            await SetDataSource(m_Data);
        }
    }
}
