using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement
{
    class CellLabelWithIcon : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/CellLabelWithIcon/CellLabelWithIcon.uxml";
        Label m_Label;
        VisualElement m_Icon;

        public CellLabelWithIcon()
        {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).CloneTree(this);
            m_Label = this.Q<Label>("Label");
            m_Icon = this.Q<VisualElement>("Icon");
        }

        public void SetLabelText(string text) => m_Label.text = text ?? string.Empty;

        public void SetIconClassName(string iconClassName) => m_Icon.AddToClassList(iconClassName);

        public void RemoveIconClassName(string iconClassName) => m_Icon.RemoveFromClassList(iconClassName);

        public new class UxmlFactory : UxmlFactory<CellLabelWithIcon, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }
    }
}
