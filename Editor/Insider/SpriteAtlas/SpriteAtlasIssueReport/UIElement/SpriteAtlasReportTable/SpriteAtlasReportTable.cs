using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement
{
    class SpriteAtlasReportTable : MultiColumnListView
    {
        const string k_Uss = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/SpriteAtlasReportTable/SpriteAtlasReportTable.uss";

        public SpriteAtlasReportTable()
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_Uss));
            showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        }

        public MultiColumnListView multiColumnListView => this;

        public new class UxmlFactory : UxmlFactory<SpriteAtlasReportTable, UxmlTraits> { }
        public new class UxmlTraits : MultiColumnListView.UxmlTraits { }
    }
}
