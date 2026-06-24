using System;
using System.Collections.Generic;
using UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement;
using UnityEngine;
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
                m_OnInspectObject?.Invoke(this, cellData.asset.GetAsset());
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
                MultiColumnViewCompat.SetColumnComparison(column, (a, b) =>
                {
                    var aData = GetCellDataForRow(a);
                    var bData = GetCellDataForRow(b);
                    if (aData == null && bData == null)
                        return 0;
                    if (aData == null)
                        return -1;
                    if (bData == null)
                        return 1;
                    return AllSpriteAtlasReportCellData.Compare(aData, bData, bindingPath);
                });
            }
        }

        void OnColumnSortingChanged()
        {
            var savedSort = MultiColumnViewCompat.CopySortedColumns(m_Table.sortedColumns);
            UpdateTableView(true, true);
            MultiColumnViewCompat.RestoreSortColumnDescriptions(m_Table, savedSort);
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

        void UpdateTableView(bool keepExpand, bool sortUpdate = false)
        {
            BuildAltasInfoDataTree();
            m_ReportListItem.SetCount($"{GetRootAtlasCount()}");
            List<int> expandedIds = new();
            if (keepExpand)
            {
                foreach (var d in m_Data)
                {
                    if(m_Table.IsExpanded(d.id))
                        expandedIds.Add(d.id);
                }
            }
            m_Table.SetRootItems(m_Data);
            if (sortUpdate)
                m_Table.RefreshItems();
            else
                m_Table.Rebuild();
            foreach(var expand in expandedIds)
                m_Table.ExpandItem(expand);
            ShowTable(m_Data.Count > 0, "No Sprite Atlas found.");
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

            if (MultiColumnViewCompat.HasActiveSort(m_Table))
                m_Data.Sort(SortData);
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

        int SortData(TreeViewItemData<AllSpriteAtlasReportCellData> a, TreeViewItemData<AllSpriteAtlasReportCellData> b)
        {
            using (var enumerator = m_Table.sortedColumns.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    var sortColumn = enumerator.Current.column;
                    var bindingPath = sortColumn != null
                        ? MultiColumnViewCompat.GetBindingPath(sortColumn)
                        : enumerator.Current.columnName;
                    int result = AllSpriteAtlasReportCellData.Compare(
                        a.data,
                        b.data,
                        bindingPath);
                    if (result != 0)
                        return result * (enumerator.Current.direction == SortDirection.Ascending ? 1 : -1);
                }
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
                    List<TreeViewItemData<AllSpriteAtlasReportCellData>> spriteNodes = null;
                    if (textureInfo[i].spriteInfo != null)
                    {
                        spriteNodes = new();
                        foreach(var sprite in textureInfo[i].spriteInfo)
                        {
                            spriteNodes.Add(new TreeViewItemData<AllSpriteAtlasReportCellData>(++id, new AllSpriteAtlasReportCellData(atlasInfo, sprite)
                            {
                                icon = "sprite-icon"
                            }));
                        }
                        if (MultiColumnViewCompat.HasActiveSort(m_Table))
                            spriteNodes.Sort(SortData);
                    }
                    var textureNode = new TreeViewItemData<AllSpriteAtlasReportCellData>(++id,
                        new AllSpriteAtlasReportCellData(atlasInfo, textureInfo[i]) { icon = "texture-icon" }, spriteNodes);
                    data.Add(textureNode);

                }
            }

            if (MultiColumnViewCompat.HasActiveSort(m_Table))
                data.Sort(SortData);

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
