using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.U2D.Sprites;
using UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class SpriteWithSecondaryTextureIssue : AnalyzerIssueReportBase
    {
        const string k_Uxml = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/SpriteWithSecondaryTextureIssue/SpriteWithSecondaryTextureIssue.uxml";
        const string k_SaveFilePath = "Library/com.unity.2d.sprite-atlas-analyzer/AnalyzerWindow/SpriteWithSecondaryTextureIssue.json";
        VisualElement m_IssueView;
        MultiColumnTreeView m_Table;
        List<TreeViewItemData<SpriteWithSecondayTextureCellData>> m_TableData = new();
        readonly List<(string bindingPath, int direction)> m_SortKeys = new();
        bool m_ApplyingSort;
        Label m_NoDataLabel;
        SpriteAtlasDataSource m_DataSource;
        public SpriteWithSecondaryTextureIssue(): base(new [] {typeof(SpriteAtlasDataSource)})
        {
            SetReportListItemName("Textures contain different secondary texture count in Sprite Atlas");
            SetReportListemCount("0");

            m_IssueView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).Instantiate();

            m_Table = m_IssueView.Q<MultiColumnTreeView>("Table");
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
            var data = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(m_Table.selectedIndex);
            if(data != null)
                InspectObject(data.instanceId.GetAsset());
        }

        public override VisualElement reportContent => m_IssueView;
        public override VisualElement settingsContent => null;
        public override string reportTitle => "Atlas Secondary Texture";

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
                await SetDataSource(dataSource);
            }
        }

        async Task SetDataSource(SpriteAtlasDataSource dataSource)
        {
            if (dataSource == null)
                return;
            isFilteringReport = true;
            m_DataSource = dataSource;
            await UpdateTableView(false);
        }

        async Task<List<TreeViewItemData<SpriteWithSecondayTextureCellData>>> FilterDataAsync(List<EditorAtlasInfo> dataSource)
        {
            SpriteDataProviderFactories spriteDataFactory = new();
            spriteDataFactory.Init();
            var result = new List<TreeViewItemData<SpriteWithSecondayTextureCellData>>();
            var uniqueDataPath = new Dictionary<string, (string assetName, int entityId, int textureCount)>();
            for (int i = 0; i < dataSource?.Count; ++i)
            {
                var atlasInfo = dataSource[i];
                if (atlasInfo.isVariant)
                    continue;
                for (int j = 0; j < atlasInfo?.textureInfo.Count; ++j)
                {
                    var textureInfo = atlasInfo.textureInfo[j];
                    for (int k = 0; k < textureInfo?.spriteInfo.Count; ++k)
                    {
                        await Task.Delay(10);
                        var si = textureInfo.spriteInfo[k];

                        if (!uniqueDataPath.ContainsKey(si.assetPath))
                        {
                            var sprite = AssetDatabase.LoadMainAssetAtPath(si.assetPath);
                            if (sprite != null)
                            {
                                var dp = spriteDataFactory.GetSpriteEditorDataProviderFromObject(sprite);
                                dp.InitSpriteEditorDataProvider();
                                var stdp = dp.GetDataProvider<ISecondaryTextureDataProvider>();
                                uniqueDataPath.Add(si.assetPath, (sprite.name, sprite.GetInstanceID(), stdp?.textures?.Length ?? 0));
                            }
                        }
                    }
                }
            }

            int rowId = 0;
            for (int i = 0; i < dataSource?.Count; ++i)
            {
                var atlasInfo = dataSource[i];
                if (atlasInfo.isVariant)
                    continue;
                if (atlasInfo?.textureInfo.Count <= 0)
                    continue;

                EditorSpriteInfo firstSprite = null;
                foreach (var textureInfo in atlasInfo.textureInfo)
                {
                    if (textureInfo?.spriteInfo.Count > 0)
                    {
                        firstSprite = textureInfo.spriteInfo[0];
                        break;
                    }
                }

                if (firstSprite == null || !uniqueDataPath.ContainsKey(firstSprite.assetPath))
                    continue;

                var sanity = uniqueDataPath[firstSprite.assetPath].textureCount;
                bool foundDifference = false;

                for (int j = 0; j < atlasInfo.textureInfo.Count; ++j)
                {
                    var textureInfo = atlasInfo.textureInfo[j];
                    for (int k = 0; k < textureInfo?.spriteInfo.Count; ++k)
                    {
                        await Task.Delay(10);
                        var si = textureInfo.spriteInfo[k];
                        if(!uniqueDataPath.ContainsKey(si.assetPath))
                            continue;
                        if (uniqueDataPath[si.assetPath].textureCount != sanity)
                        {
                            foundDifference = true;
                            break;
                        }
                    }
                    if (foundDifference)
                        break;
                }

                if (foundDifference)
                {
                    var children = BuildTextureTree(ref rowId, atlasInfo, uniqueDataPath);
                    result.Add(new (rowId++,
                        new SpriteWithSecondayTextureCellData
                        {
                            icon = "spriteatlas-icon",
                            name = atlasInfo.name,
                            count = children.Count,
                            instanceId =  atlasInfo.instanceId
                        }, children));
                    ++rowId;
                }
            }
            return result;
        }

        List<TreeViewItemData<SpriteWithSecondayTextureCellData>> BuildTextureTree(ref int rowId, EditorAtlasInfo atlasInfo,
            Dictionary<string, (string assetName, int entityId, int textureCount)> data)
        {
            List<TreeViewItemData<SpriteWithSecondayTextureCellData>> result = new();
            HashSet<string> uniquePath = new();
            for (int j = 0; j < atlasInfo?.textureInfo.Count; ++j)
            {
                var textureInfo = atlasInfo.textureInfo[j];
                for (int k = 0; k < textureInfo?.spriteInfo.Count; ++k)
                {
                    var spriteInfo = textureInfo.spriteInfo[k];
                    if (!uniquePath.Contains(spriteInfo.assetPath))
                    {
                        result.Add(new(rowId++,
                            new SpriteWithSecondayTextureCellData
                            {
                                icon = "texture-icon",
                                name = data[spriteInfo.assetPath].assetName,
                                count = data[spriteInfo.assetPath].textureCount,
                                instanceId = data[spriteInfo.assetPath].entityId
                            }));
                        uniquePath.Add(spriteInfo.assetPath);
                    }
                }
            }
            return result;
        }

        void SetNameColumnCellIcon(VisualElement ele, SpriteWithSecondayTextureCellData data)
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
                        var itemData = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(rowIndex);
                        (element as CellLabelWithIcon).SetLabelText(itemData.GetDisplayText(bindingPath));
                        SetNameColumnCellIcon(element, itemData);
                    };
                }
                else
                {
                    column.bindCell = (element, rowIndex) =>
                    {
                        var itemData = m_Table.GetItemDataForIndex<SpriteWithSecondayTextureCellData>(rowIndex);
                        (element as CellLabelWithIcon).SetLabelText(itemData.GetDisplayText(bindingPath));
                    };
                }

                column.makeCell = () =>
                {
                    return new CellLabelWithIcon();
                };
                MultiColumnViewCompat.SetColumnComparison(column, (a, b) => 0);
            }
        }

        void OnColumnSortingChanged()
        {
            if (m_ApplyingSort || m_TableData.Count < 2)
                return;

            var savedSort = MultiColumnViewCompat.CopySortedColumns(m_Table.sortedColumns);
            if (savedSort.Count == 0)
                return;

            UpdateSortKeys();
            ApplyActiveSort();

            var expandedRootIds = CollectExpandedRootIds();
            m_ApplyingSort = true;
            m_Table.schedule.Execute(() => ApplySortedTreeView(savedSort, expandedRootIds)).StartingIn(0);
        }

        List<int> CollectExpandedRootIds()
        {
            var expandedRootIds = new List<int>();
            for (int i = 0; i < m_TableData.Count; ++i)
            {
                if (m_Table.IsExpanded(m_TableData[i].id))
                    expandedRootIds.Add(m_TableData[i].id);
            }

            return expandedRootIds;
        }

        void ApplySortedTreeView(IReadOnlyList<SortColumnDescription> savedSort, List<int> expandedRootIds)
        {
            if (m_Table?.panel == null)
            {
                m_ApplyingSort = false;
                return;
            }

            try
            {
                m_Table.columnSortingChanged -= OnColumnSortingChanged;
                MultiColumnViewCompat.RestoreSortColumnDescriptions(m_Table, savedSort);
                m_Table.SetRootItems(m_TableData);
                m_Table.Rebuild();
                for (int i = 0; i < expandedRootIds.Count; ++i)
                    m_Table.ExpandItem(expandedRootIds[i]);
            }
            finally
            {
                m_Table.columnSortingChanged += OnColumnSortingChanged;
                m_ApplyingSort = false;
            }
        }

        void UpdateSortKeys()
        {
            m_SortKeys.Clear();
            foreach (var sorted in m_Table.sortedColumns)
            {
                var bindingPath = sorted.column != null
                    ? MultiColumnViewCompat.GetBindingPath(sorted.column)
                    : sorted.columnName;
                m_SortKeys.Add((bindingPath, sorted.direction == SortDirection.Ascending ? 1 : -1));
            }
        }

        void ApplyActiveSort()
        {
            if (!MultiColumnViewCompat.HasActiveSort(m_Table))
                return;

            UpdateSortKeys();
            var rawData = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.CovertToSaveFormat(m_TableData);
            rawData.Sort(SortSaveData);
            for (int i = 0; i < rawData.Count; ++i)
            {
                if (rawData[i].children != null && rawData[i].children.Count > 1)
                    rawData[i].children.Sort(SortSaveData);
            }

            m_TableData = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.ConvertToTreeViewItemData(rawData);
        }

        int SortSaveData(
            ReportSaveData<SpriteWithSecondayTextureCellData> x,
            ReportSaveData<SpriteWithSecondayTextureCellData> y)
        {
            for (int i = 0; i < m_SortKeys.Count; ++i)
            {
                var (bindingPath, direction) = m_SortKeys[i];
                int result = SpriteWithSecondayTextureCellData.Compare(x.data, y.data, bindingPath);
                if (result != 0)
                    return result * direction;
            }

            return SpriteWithSecondayTextureCellData.Compare(x.data, y.data, null);
        }

        async Task UpdateTableView(bool keepExpand)
        {
            ShowTable(false, "Filtering data in progress...");
            var saveFile = Utilities.LoadSaveDataFromFile<ReportSaveDataRoot<SpriteWithSecondayTextureCellData>>(k_SaveFilePath);
            if (saveFile == null || m_DataSource.lastCaptureTime != saveFile.lastCaptureTime)
            {
                m_TableData = await FilterDataAsync(m_DataSource.data);
                Utilities.WriteSaveDataToFile(k_SaveFilePath, new ReportSaveDataRoot<SpriteWithSecondayTextureCellData>()
                {
                    lastCaptureTime = m_DataSource.lastCaptureTime,
                    root = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.CovertToSaveFormat(m_TableData)
                });
            }
            else
            {
                m_TableData = ReportSaveDataRoot<SpriteWithSecondayTextureCellData>.ConvertToTreeViewItemData(saveFile.root);
            }

            ApplyActiveSort();

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
            m_Table.Rebuild();
            foreach(var expand in expandedIds)
                m_Table.ExpandItem(expand);
            isFilteringReport = false;
            SetReportListemCount($"{m_TableData.Count}");
            ShowTable(m_TableData.Count > 0, "No Sprite Atlas where source texture has different secondary texture found.");
        }
    }
}
