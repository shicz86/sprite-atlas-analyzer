using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class ReportListView : ListView
    {
        const string k_UssPath = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/AnalyzerWindow/ReportListView/ReportListView.uss";
        List<IAnalyzerReport> m_ReportListItems;

        public ReportListView()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_UssPath));
            makeItem += MakeItem;
            bindItem += BindItem;
            unbindItem += UnbindItem;
        }

        public string headerTitle { get; set; } = "Reports";

        public void SetListDataSource(List<IAnalyzerReport> reportListItems)
        {
            m_ReportListItems = reportListItems;
            itemsSource = m_ReportListItems;
            Rebuild();
        }

        void UnbindItem(VisualElement arg1, int arg2) => arg1.Clear();

        void BindItem(VisualElement arg1, int arg2) => arg1.Add(m_ReportListItems[arg2].listItem);

        VisualElement MakeItem()
        {
            var item = new VisualElement();
            item.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_UssPath));
            item.AddToClassList("report-list-item-container");
            return item;
        }

        public new class UxmlFactory : UxmlFactory<ReportListView, UxmlTraits> { }

        public new class UxmlTraits : ListView.UxmlTraits
        {
            UxmlStringAttributeDescription m_HeaderTitle = new() { name = "header-title", defaultValue = "Reports" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                ((ReportListView)ve).headerTitle = m_HeaderTitle.GetValueFromBag(bag, cc);
            }
        }
    }
}
