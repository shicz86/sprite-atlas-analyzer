using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class SpriteCountIssue : AnalyzerIssueReportBase
    {
        CommonSpriteAtlasIssueView m_View;
        SpriteAtlasReportTable m_Table;
        List<EditorAtlasInfo> m_Data;
        List<(EditorAtlasInfo atlasInfo, int spriteCount)> m_Filtered = new();
        Column[] m_Columns;
        int m_SpriteCount = 1;
        SpriteCountIssueSettings m_Settings;

        public SpriteCountIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            m_View = new CommonSpriteAtlasIssueView();
            m_View.styleSheets.Add(CommonStyleSheet.iconStyleSheet);

            SetReportListItemName();
            SetReportListemCount("0");
            m_View.ShowTable(false, "Analyze has not been done yet.");

            m_Settings = new SpriteCountIssueSettings(m_SpriteCount);
            m_Settings.spriteCountChanged += OnSettingsSpriteCountChanged;
            m_Table = m_View.table;
            MultiColumnViewCompat.EnableColumnSorting(m_Table, MultiColumnSortBehavior.Custom);
            m_Table.columnSortingChanged += OnColumnSortingChanged;
            m_Table.AddManipulator(new ContextualMenuManipulator(OnContextualMenuManipulator));
            table.selectionChanged += OnSelectionChanged;
            SetupColumns();
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            InspectObject(m_Filtered[table.selectedIndex].atlasInfo.GetObject());
        }

        void OnContextualMenuManipulator(ContextualMenuPopulateEvent obj)
        {
            var menuStatus = m_Filtered.Count > 0 && table.selectedIndex >= 0 &&
                table.selectedIndex < table.itemsSource.Count ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            obj.menu.AppendAction("Reanalyze Atlas", (a) => RecheckAtlas(),
                menuStatus);
        }

        void RecheckAtlas()
        {
            var item = m_Filtered[table.selectedIndex].atlasInfo.GetObject();
            if (item != null)
            {
                RequestCapture(new [] {AssetDatabase.GetAssetPath(item)});
            }
        }

        void SetReportListItemName()
        {
            SetReportListItemName($"Sprite Count <= {m_SpriteCount}");
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
                    title = "Count",
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
                (e as CellLabelWithIcon).SetLabelText(m_Filtered[i].atlasInfo.name);
            };
            MultiColumnViewCompat.SetColumnComparison(m_Columns[0], (a, b) =>
            {
                return string.Compare(
                    m_Filtered[a].atlasInfo.name,
                    m_Filtered[b].atlasInfo.name,
                    StringComparison.OrdinalIgnoreCase);
            });
            m_Columns[1].makeCell = () =>
            {
                var ele = new CellLabelWithIcon();
                return ele;
            };
            m_Columns[1].bindCell = (e, i) =>
            {
                (e as CellLabelWithIcon).SetLabelText(m_Filtered[i].spriteCount.ToString());
            };
            MultiColumnViewCompat.SetColumnComparison(m_Columns[1], (a, b) =>
            {
                return m_Filtered[a].spriteCount.CompareTo(m_Filtered[b].spriteCount);
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

        public override string reportTitle => "Atlas Sprite Count";
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
            m_View.ShowTable(m_Filtered.Count > 0, $"No Sprite Atlas contains less than or equals to {m_SpriteCount} Sprite(s) found.");
        }

        async Task<List<(EditorAtlasInfo atlasInfo, int spriteCount)>> FilterDataAsync(List<EditorAtlasInfo> dataSource)
        {
            var result = new List<(EditorAtlasInfo atlasInfo, int spriteCount)>();
            var task = Task.Run(() =>
            {
                for(int i = 0; i < dataSource.Count; ++i)
                {
                    var atlasInfo = dataSource[i];
                    if(atlasInfo.isVariant)
                        continue;
                    var totalSpriteCount = 0;
                    foreach(var textureInfo in atlasInfo.textureInfo)
                    {
                        totalSpriteCount += textureInfo.spriteInfo?.Count ?? 0;
                    }
                    if (totalSpriteCount <= m_SpriteCount)
                    {
                        result.Add(new(atlasInfo, totalSpriteCount));
                    }
                }
            });
            await task;
            return result;
        }

        async void OnSettingsSpriteCountChanged(int obj)
        {
            m_SpriteCount = obj;
            SetReportListItemName();
            await SetDataSource(m_Data);
        }
    }
}
