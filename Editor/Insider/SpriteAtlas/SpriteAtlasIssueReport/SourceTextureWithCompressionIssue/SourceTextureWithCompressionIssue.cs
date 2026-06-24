using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class SourceTextureWithCompressionIssue : AnalyzerIssueReportBase
    {
        const string k_Uxml = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/SourceTextureWithCompressionIssue/SourceTextureWithCompressionIssue.uxml";
        const string k_SaveFilePath = "Library/com.unity.2d.sprite-atlas-analyzer/AnalyzerWindow/SourceTextureWithCompressionIssue.json";
        VisualElement m_IssueView;
        MultiColumnTreeView m_Table;
        List<TreeViewItemData<SourceTextureWithCompressionCellData>> m_TableData = new();
        Label m_NoDataLabel;
        SpriteAtlasDataSource m_DataSource;
        public SourceTextureWithCompressionIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            SetReportListItemName("Source Textures with compression in Sprite Atlas");
            SetReportListemCount("0");

            m_IssueView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).Instantiate();

            m_Table = m_IssueView.Q<MultiColumnTreeView>("Table");
            m_Table.AddManipulator(new ContextualMenuManipulator(OnContextualMenuManipulator));
            m_Table.selectionChanged += OnSelectionChanged;
            if(EditorGUIUtility.isProSkin)
                m_IssueView.AddToClassList("dark");
            else
                m_IssueView.AddToClassList("light");
            SetupTable();
            m_NoDataLabel = m_IssueView.Q<Label>("NoDataLabel");
            ShowTable(false, "Analyze has not been done yet.");
        }

        void ShowTable(bool show, string noShowReason)
        {
            m_Table.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            m_NoDataLabel.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            if (!string.IsNullOrEmpty(noShowReason) && !show)
                m_NoDataLabel.text = noShowReason;
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            var item = m_Table.GetItemDataForIndex<SourceTextureWithCompressionCellData>(m_Table.selectedIndex);
            if(item != null)
                InspectObject(item.asset.GetAsset());
        }

        void OnContextualMenuManipulator(ContextualMenuPopulateEvent evt)
        {
            var menuStatus = m_TableData.Count > 0 && m_Table.selectedIndex >= 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            evt.menu.AppendAction("Reanalyze Atlas", (a) => RecheckAtlas(),
                menuStatus);
        }

        void RecheckAtlas()
        {
            var item = m_Table.GetItemDataForIndex<SourceTextureWithCompressionCellData>(m_Table.selectedIndex);
            if (item != null)
            {
                RequestCapture(new [] {AssetDatabase.GetAssetPath(item.atlasInfo.GetObject())});
            }
        }

        public override VisualElement reportContent => m_IssueView;
        public override VisualElement settingsContent => null;
        public override string reportTitle => "Source Textures with Compression";

        protected override async void OnReportDataSourceChanged(IReportDataSource reportDataSource)
        {
            if (reportDataSource is SpriteAtlasDataSource dataSource)
            {
                await SetDataSourceProvider(dataSource);
            }
        }

        async Task SetDataSourceProvider(SpriteAtlasDataSource dataSource)
        {
            if (dataSource?.data != null)
            {
                await SetDataSource(dataSource);
            }
        }

        async Task SetDataSource(SpriteAtlasDataSource dataSource)
        {
            if (dataSource == null)
                return;
            m_DataSource = dataSource;
            await UpdateTableView(false);
        }

        async Task UpdateTableView(bool keepExpand, bool sortUpdate = false)
        {
            if (m_DataSource == null)
                return;
            ShowTable(false, "Filtering data in progress...");
            var saveFile = Utilities.LoadSaveDataFromFile<ReportSaveDataRoot<SourceTextureWithCompressionCellData>>(k_SaveFilePath);
            if (!sortUpdate)
            {
                if (saveFile == null || m_DataSource.lastCaptureTime != saveFile.lastCaptureTime)
                {
                    m_TableData = await FilterDataAsync(m_DataSource.data);
                    Utilities.WriteSaveDataToFile(k_SaveFilePath, new ReportSaveDataRoot<SourceTextureWithCompressionCellData>()
                    {
                        lastCaptureTime = m_DataSource.lastCaptureTime,
                        root = ReportSaveDataRoot<SourceTextureWithCompressionCellData>.CovertToSaveFormat(m_TableData)
                    });
                }
                else
                {
                    m_TableData =ReportSaveDataRoot<SourceTextureWithCompressionCellData>.ConvertToTreeViewItemData(saveFile.root);
                }
            }

            SortTableData();

            List<int> expandedIds = new();
            if (keepExpand)
            {
                foreach (var d in m_TableData)
                {
                    if(m_Table.IsExpanded(d.id))
                        expandedIds.Add(d.id);
                }
            }
            m_Table.SetRootItems(m_TableData);
            if (sortUpdate)
                m_Table.RefreshItems();
            else
                m_Table.Rebuild();
            foreach(var expand in expandedIds)
                m_Table.ExpandItem(expand);
            SetReportListemCount($"{m_TableData.Count}");
            ShowTable(m_TableData.Count > 0, "No Sprite Atlas where source texture has compression found.");
        }

        void SortTableData()
        {
            if (MultiColumnViewCompat.HasActiveSort(m_Table))
            {
                var rawData = ReportSaveDataRoot<SourceTextureWithCompressionCellData>.CovertToSaveFormat(m_TableData);
                SortTableData(rawData);
                m_TableData = ReportSaveDataRoot<SourceTextureWithCompressionCellData>.ConvertToTreeViewItemData(rawData);
            }
        }

        void SortTableData(List<ReportSaveData<SourceTextureWithCompressionCellData>> rawData)
        {
            rawData.Sort(SortData);
            foreach(var row in rawData)
            {
                if(row.children != null && row.children.Count > 0)
                {
                    row.children.Sort(SortData);
                    SortTableData(row.children);
                }
            }
        }

        int SortData(ReportSaveData<SourceTextureWithCompressionCellData> x, ReportSaveData<SourceTextureWithCompressionCellData> y)
        {
            using (var enumerator = m_Table.sortedColumns.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    int result = SourceTextureWithCompressionCellData.Compare(
                        x.data,
                        y.data,
                        MultiColumnViewCompat.GetBindingPath(enumerator.Current.column));
                    if (result != 0)
                        return result * (enumerator.Current.direction == SortDirection.Ascending ? 1 : -1);
                }
            }

            return SourceTextureWithCompressionCellData.Compare(x.data, y.data, null);
        }

        async Task<List<TreeViewItemData<SourceTextureWithCompressionCellData>>> FilterDataAsync(List<EditorAtlasInfo> dataSource)
        {
            isFilteringReport = true;
            var result = new List<TreeViewItemData<SourceTextureWithCompressionCellData>>();
            var uniqueTexture = new HashSet<int>();
            int id = 0;
            for (int i = 0; i < dataSource?.Count; ++i)
            {
                var atlasInfo = dataSource[i];
                if(atlasInfo.isVariant)
                    continue;

                List <TreeViewItemData<SourceTextureWithCompressionCellData >> children = new ();
                foreach (var textureInfo in atlasInfo?.textureInfo)
                {
                    for (int j = 0; j < textureInfo?.spriteInfo.Count; ++j)
                    {
                        var si = textureInfo.spriteInfo[j];
                        var texture = si.GetSpriteTexture(false);
                        if (texture)
                        {
                            int textureId = texture.GetInstanceID();
                            if(uniqueTexture.Contains(textureId))
                                continue;
                            uniqueTexture.Add(textureId);
                            if (GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat))
                            {
                                children.Add(new (id++,
                                    new SourceTextureWithCompressionCellData
                                    {
                                        icon = "texture-icon",
                                        name = texture.name,
                                        textureFormat = texture.graphicsFormat.ToString(),
                                        asset = texture,
                                        atlasInfo = atlasInfo
                                    }));
                            }
                        }
                    }
                    await Task.Delay(10);
                }
                if(children.Count > 0)
                {
                    var atlasInfoData = new SourceTextureWithCompressionCellData
                    {
                        icon = "spriteatlas-icon",
                        name = atlasInfo.name,
                        textureFormat = atlasInfo.textureFormat.ToString(),
                        asset = atlasInfo.GetObject(),
                        atlasInfo = atlasInfo
                    };
                    result.Add(new TreeViewItemData<SourceTextureWithCompressionCellData>(id++, atlasInfoData, children));
                }
            }
            isFilteringReport = false;
            return result;
        }

        static void SetNameColumnCellIcon(VisualElement ele, SourceTextureWithCompressionCellData data)
        {
            var icon = ele.Q("Icon");
            icon.RemoveFromClassList("texture-icon");
            icon.RemoveFromClassList("sprite-icon");
            icon.RemoveFromClassList("spriteatlas-icon");
            if(data.icon?.Length > 0)
                icon.AddToClassList(data.icon);
        }

        void SetupTable()
        {
            MultiColumnViewCompat.EnableColumnSorting(m_Table, MultiColumnSortBehavior.Custom);
            m_Table.columnSortingChanged += OnColumnSortingChanged;
            m_Table.SetRootItems(m_TableData);
            for(int i = 0; i < m_Table.columns.Count; ++i)
            {
                var column = m_Table.columns[i];
                var bindingPath = MultiColumnViewCompat.GetBindingPath(column);
                if (column.name == "Name")
                {
                    column.bindCell = (element, rowIndex) =>
                    {
                        var itemData = m_Table.GetItemDataForIndex<SourceTextureWithCompressionCellData>(rowIndex);
                        (element as CellLabelWithIcon).SetLabelText(itemData.GetDisplayText(bindingPath));
                        SetNameColumnCellIcon(element, itemData);
                    };
                }
                else
                {
                    column.bindCell = (element, rowIndex) =>
                    {
                        var itemData = m_Table.GetItemDataForIndex<SourceTextureWithCompressionCellData>(rowIndex);
                        (element as CellLabelWithIcon).SetLabelText(itemData.GetDisplayText(bindingPath));
                    };
                }

                column.makeCell = () =>
                {
                    return new CellLabelWithIcon();
                };
                MultiColumnViewCompat.SetColumnComparison(column, (a, b) =>
                {
                    var itemA = m_Table.GetItemDataForIndex<SourceTextureWithCompressionCellData>(a);
                    var itemB = m_Table.GetItemDataForIndex<SourceTextureWithCompressionCellData>(b);
                    return SourceTextureWithCompressionCellData.Compare(itemA, itemB, bindingPath);
                });
            }
        }

        async void OnColumnSortingChanged()
        {
            var savedSort = MultiColumnViewCompat.CopySortedColumns(m_Table.sortedColumns);
            await UpdateTableView(true, true);
            MultiColumnViewCompat.RestoreSortColumnDescriptions(m_Table, savedSort);
        }
    }
}
