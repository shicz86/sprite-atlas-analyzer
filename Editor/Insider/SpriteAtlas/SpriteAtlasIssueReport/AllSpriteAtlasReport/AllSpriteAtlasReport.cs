using System;
using System.Collections.Generic;
using UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class AllSpriteAtlasReport : IAnalyzerReport
    {
        const string k_Uxml = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/AllSpriteAtlasReport/SpriteAtlasRawData.uxml";
        IDataSourceProvider m_DataSourceProvider;
        SpriteAtlasDataSource m_SpriteAtlasDataSource;
        VisualElement m_ReportView;
        MultiColumnTreeView m_Table;
        Label m_NoDataLabel;
        List<TreeViewItemData<AllSpriteAtlasReportCellData>> m_Data = new();
        readonly List<(string bindingPath, int direction)> m_SortKeys = new();
        bool m_ApplyingSort;
        ReportListItem m_ReportListItem;
        event Action<IAnalyzerReport, Object> m_OnInspectObject;

        public AllSpriteAtlasReport()
        {
            m_ReportListItem = new();
            m_ReportListItem.SetName("All Sprite Atlases");
            m_ReportListItem.SetCount("0");
            m_ReportView = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).Instantiate();
            m_Table = m_ReportView.Q<MultiColumnTreeView>("Table");
            m_Table.AddManipulator(new ContextualMenuManipulator(OnContextualMenuManipulator));
            m_Table.selectionChanged += OnSelectionChanged;
            if(EditorGUIUtility.isProSkin)
                m_Table.AddToClassList("dark");
            else
                m_Table.AddToClassList("light");
            SetupTable();
            m_NoDataLabel = m_ReportView.Q<Label>("NoDataLabel");
            ShowTable(false, "Analyze has not been done yet.");
        }

        void ShowTable(bool show, string noShowReason)
        {
            m_Table.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            m_NoDataLabel.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            if (!string.IsNullOrEmpty(noShowReason) && !show)
                m_NoDataLabel.text = noShowReason;
        }

        AllSpriteAtlasReportCellData GetCellDataForRow(int rowIndex)
        {
            if (rowIndex < 0)
                return null;

            var id = m_Table.GetIdForIndex(rowIndex);
            if (id < 0)
                return null;

            return m_Table.GetItemDataForId<AllSpriteAtlasReportCellData>(id);
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            var cellData = GetCellDataForRow(m_Table.selectedIndex);
            if(cellData != null)
                m_OnInspectObject?.Invoke(this, ResolveInspectableObject(cellData));
        }

        static Object ResolveInspectableObject(AllSpriteAtlasReportCellData cellData)
        {
            var obj = cellData.asset.GetAsset();
            if (obj)
                return obj;

            if (cellData.texturePageIndex < 0 || cellData.icon != "texture-icon")
                return null;

            var atlas = cellData.atlasAsset.GetAsset() as SpriteAtlas;
            if (!atlas)
                return null;

            return SpriteAtlasBridgeCompat.GetAtlasMainTextureForPage(
                atlas,
                cellData.texturePageIndex,
                ensurePacked: true);
        }


        void OnContextualMenuManipulator(ContextualMenuPopulateEvent obj)
        {
            var menuStatus =  m_Data.Count > 0 && m_Table.selectedIndex >= 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            obj.menu.AppendAction("Reanalyze Atlas", a => RecheckAtlas(), menuStatus);
        }

        public VisualElement reportContent => m_ReportView;
        public VisualElement settingsContent => null;

        void RecheckAtlas()
        {
            var item = GetCellDataForRow(m_Table.selectedIndex);
            if (item != null)
            {
                m_DataSourceProvider.GetDataSource<SpriteAtlasDataSource>().Capture(new string[]
                {
                    AssetDatabase.GetAssetPath(item.atlasAsset.GetAsset())
                });
            }
        }

        void SetNameColumnCellIcon(VisualElement ele, AllSpriteAtlasReportCellData data)
        {
            var icon = ele.Q("Icon");
            icon.RemoveFromClassList("texture-icon");
            icon.RemoveFromClassList("sprite-icon");
            icon.RemoveFromClassList("spriteatlas-icon");
            if(data.icon?.Length > 0)
                icon.AddToClassList(data.icon);
        }

        void BindLabelToDataSource(Label label, string path, AllSpriteAtlasReportCellData cellData)
        {
            label.text = cellData?.GetDisplayText(path) ?? string.Empty;
        }

        void SetupTable()
        {
            MultiColumnViewCompat.EnableColumnSorting(m_Table, MultiColumnSortBehavior.Custom);
            m_Table.columnSortingChanged += OnColumnSortingChanged;
            for(int i = 0; i < m_Table.columns.Count; ++i)
            {
                var column = m_Table.columns[i];
                var bindingPath = MultiColumnViewCompat.GetBindingPath(column);

                if (column.name == "Name")
                {
                    column.bindCell = (element, rowIndex) =>
                    {
                        var label = element.Q<Label>();
                        var itemData = GetCellDataForRow(rowIndex);
                        BindLabelToDataSource(label, bindingPath, itemData);
                        SetNameColumnCellIcon(element, itemData);
                    };
                }
                else
                {
                    column.bindCell = (element, rowIndex) =>
                    {
                        var itemData = GetCellDataForRow(rowIndex);
                        var label = element.Q<Label>();
                        BindLabelToDataSource(label, bindingPath, itemData);
                    };
                }

                column.unbindCell = (element, _) =>
                {
                    var label = element.Q<Label>();
                    if (label != null)
                        label.text = string.Empty;
                    var icon = element.Q("Icon");
                    if (icon != null)
                    {
                        icon.RemoveFromClassList("texture-icon");
                        icon.RemoveFromClassList("sprite-icon");
                        icon.RemoveFromClassList("spriteatlas-icon");
                    }
                };

                column.makeCell = () => MultiColumnViewCompat.MakeCellLabelWithIcon();
                // 2022.3 has no ColumnSortingMode.Custom — avoid built-in sort using row-index lookups.
                MultiColumnViewCompat.SetColumnComparison(column, (a, b) => 0);
            }
        }

        void OnColumnSortingChanged()
        {
            if (m_ApplyingSort || m_Data.Count < 2)
                return;

            var savedSort = MultiColumnViewCompat.CopySortedColumns(m_Table.sortedColumns);
            if (savedSort.Count == 0)
                return;

            UpdateSortKeys();
            SortRootData();

            var expandedRootIds = CollectExpandedIds();
            m_ApplyingSort = true;
            m_Table.schedule.Execute(() => ApplySortedTreeView(savedSort, expandedRootIds)).StartingIn(0);
        }

        List<int> CollectExpandedIds()
        {
            var expandedIds = new List<int>();
            CollectExpandedIds(m_Data, expandedIds);
            return expandedIds;
        }

        void CollectExpandedIds(
            IEnumerable<TreeViewItemData<AllSpriteAtlasReportCellData>> nodes,
            List<int> expandedIds)
        {
            if (nodes == null)
                return;

            foreach (var node in nodes)
            {
                if (!m_Table.IsExpanded(node.id))
                    continue;

                expandedIds.Add(node.id);
                if (node.hasChildren)
                    CollectExpandedIds(node.children, expandedIds);
            }
        }

        void ApplySortedTreeView(IReadOnlyList<SortColumnDescription> savedSort, List<int> expandedIds)
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
                m_Table.SetRootItems(m_Data);
                m_Table.Rebuild();
                for (int i = 0; i < expandedIds.Count; ++i)
                    m_Table.ExpandItem(expandedIds[i]);
            }
            finally
            {
                m_Table.columnSortingChanged += OnColumnSortingChanged;
                m_ApplyingSort = false;
            }
        }

        void SortRootData()
        {
            if (!MultiColumnViewCompat.HasActiveSort(m_Table))
                return;

            var rawData = ReportSaveDataRoot<AllSpriteAtlasReportCellData>.CovertToSaveFormat(m_Data);
            rawData.Sort(SortSaveData);
            m_Data.Clear();
            m_Data.AddRange(ReportSaveDataRoot<AllSpriteAtlasReportCellData>.ConvertToTreeViewItemData(rawData));
        }

        public VisualElement listItem => m_ReportListItem;
        public string reportTitle => "All Sprite Atlases";

        public void SetDataSourceProvider(IDataSourceProvider dataSourceProvider)
        {
            m_DataSourceProvider = dataSourceProvider;
            m_DataSourceProvider.onDataSourceChanged += InitDataSource;
            InitDataSource();
        }

        void InitDataSource()
        {
            if (m_SpriteAtlasDataSource != null)
                m_SpriteAtlasDataSource.onDataSourceChanged -= OnSpriteAtlasDataSourceChanged;

            m_SpriteAtlasDataSource = m_DataSourceProvider.GetDataSource<SpriteAtlasDataSource>();
            if (m_SpriteAtlasDataSource != null)
            {
                m_SpriteAtlasDataSource.onDataSourceChanged += OnSpriteAtlasDataSourceChanged;
                UpdateTableView(false);
            }
        }

        void OnSpriteAtlasDataSourceChanged(IReportDataSource dataSource)
        {
            if (dataSource is not SpriteAtlasDataSource source || source != m_SpriteAtlasDataSource)
                return;
            UpdateTableView(false);
        }

        void UpdateTableView(bool keepExpand)
        {
            BuildAltasInfoDataTree();

            m_ReportListItem.SetCount($"{GetRootAtlasCount()}");
            var expandedIds = keepExpand ? CollectExpandedIds() : new List<int>();
            m_Table.SetRootItems(m_Data);
            m_Table.Rebuild();
            foreach(var expand in expandedIds)
                m_Table.ExpandItem(expand);
            ShowTable(m_Data.Count > 0, "No Sprite Atlas found.");
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

        void BuildAltasInfoDataTree()
        {
            if (m_SpriteAtlasDataSource == null)
                return;

            m_Data.Clear();
            var atlasData = m_SpriteAtlasDataSource.data;
            if (atlasData == null)
                return;

            int id = 0;
            for (int i = 0; i < atlasData.Count; ++i)
            {
                // place variant atlases under their main atlas
                if (atlasData[i].isVariant)
                    continue;
                var children = BuildSpriteInfoDataTree(ref id, atlasData[i]);
                for(int j = 0; j < atlasData.Count; ++j)
                {
                    // look for variant
                    if(i != j && atlasData[j].masterAtlasGuid == atlasData[i].atlasGuid)
                    {
                        var variantCellData = new AllSpriteAtlasReportCellData(atlasData[j]) { icon = "spriteatlas-icon" };
                        children.Add(new TreeViewItemData<AllSpriteAtlasReportCellData>(++id, variantCellData));
                    }
                }

                var atlasCellData = new AllSpriteAtlasReportCellData(atlasData[i]) { icon = "spriteatlas-icon" };
                m_Data.Add(new TreeViewItemData<AllSpriteAtlasReportCellData>(++id, atlasCellData,
                    children));
            }

            for (int i = 0; i < atlasData.Count; ++i)
            {
                if (!atlasData[i].isVariant || HasMasterInCapture(atlasData, atlasData[i]))
                    continue;

                var atlasCellData = new AllSpriteAtlasReportCellData(atlasData[i]) { icon = "spriteatlas-icon" };
                m_Data.Add(new TreeViewItemData<AllSpriteAtlasReportCellData>(++id, atlasCellData));
            }
        }

        static bool HasMasterInCapture(List<EditorAtlasInfo> atlasData, EditorAtlasInfo variantAtlas)
        {
            for (int i = 0; i < atlasData.Count; ++i)
            {
                if (!atlasData[i].isVariant && atlasData[i].atlasGuid == variantAtlas.masterAtlasGuid)
                    return true;
            }

            return false;
        }

        int GetRootAtlasCount()
        {
            if (m_Data.Count > 0)
                return m_Data.Count;

            var atlasData = m_SpriteAtlasDataSource?.data;
            if (atlasData == null)
                return 0;

            int count = 0;
            for (int i = 0; i < atlasData.Count; ++i)
            {
                if (!atlasData[i].isVariant || !HasMasterInCapture(atlasData, atlasData[i]))
                    count++;
            }

            return count;
        }

        int SortSaveData(
            ReportSaveData<AllSpriteAtlasReportCellData> a,
            ReportSaveData<AllSpriteAtlasReportCellData> b)
        {
            for (int i = 0; i < m_SortKeys.Count; ++i)
            {
                var (bindingPath, direction) = m_SortKeys[i];
                int result = AllSpriteAtlasReportCellData.Compare(a.data, b.data, bindingPath);
                if (result != 0)
                    return result * direction;
            }

            return AllSpriteAtlasReportCellData.Compare(a.data, b.data, null);
        }

        List<TreeViewItemData<AllSpriteAtlasReportCellData>> BuildSpriteInfoDataTree(ref int id, EditorAtlasInfo atlasInfo)
        {
            List<TreeViewItemData<AllSpriteAtlasReportCellData>> data = new List<TreeViewItemData<AllSpriteAtlasReportCellData>>();
            var textureInfo = atlasInfo.textureInfo;
            if (textureInfo != null)
            {
                for (int i = 0; i < textureInfo.Count; ++i)
                {
                    var pageInfo = textureInfo[i];
                    List<TreeViewItemData<AllSpriteAtlasReportCellData>> spriteNodes = null;
                    var pageSprites = pageInfo.spriteInfo;
                    if (pageSprites != null && pageSprites.Count > 0)
                    {
                        spriteNodes = new List<TreeViewItemData<AllSpriteAtlasReportCellData>>(pageSprites.Count);
                        for (int s = 0; s < pageSprites.Count; ++s)
                        {
                            spriteNodes.Add(new TreeViewItemData<AllSpriteAtlasReportCellData>(++id,
                                new AllSpriteAtlasReportCellData(atlasInfo, pageSprites[s])
                                {
                                    icon = "sprite-icon"
                                }));
                        }
                    }

                    data.Add(new TreeViewItemData<AllSpriteAtlasReportCellData>(++id,
                        new AllSpriteAtlasReportCellData(atlasInfo, pageInfo)
                        {
                            icon = "texture-icon",
                            texturePageIndex = i
                        },
                        spriteNodes));
                }
            }

            return data;
        }

        public void Dispose()
        {
            if(m_SpriteAtlasDataSource != null)
                m_SpriteAtlasDataSource.onDataSourceChanged -= OnSpriteAtlasDataSourceChanged;
            if(m_DataSourceProvider != null)
                m_DataSourceProvider.onDataSourceChanged -= InitDataSource;
        }

        public event Action<IAnalyzerReport, Object> onInspectObject
        {
            add => m_OnInspectObject += value;
            remove => m_OnInspectObject -= value;
        }

        public IAnalyzerReport GetReportForType(Type type)
        {
            if (type == GetType())
                return this;

            return null;
        }
    }
}
