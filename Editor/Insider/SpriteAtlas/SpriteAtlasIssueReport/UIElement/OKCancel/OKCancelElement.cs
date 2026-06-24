using System;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement.OKCancel
{
    class OKCancelElement : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/UIElement/OKCancel/OKCancelElement.uxml";
        Button m_OK;
        Button m_Cancel;

        public OKCancelElement()
        {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).CloneTree(this);
            m_OK = this.Q<Button>("OK");
            m_Cancel = this.Q<Button>("Cancel");
        }

        public event Action onOKClicked
        {
            add => m_OK.clicked += value;
            remove => m_OK.clicked -= value;
        }

        public event Action onCancelClicked
        {
            add => m_Cancel.clicked += value;
            remove => m_Cancel.clicked -= value;
        }

        public void SetOKButtonText(string text) => m_OK.text = text;
        public void SetCancelButtonText(string text) => m_Cancel.text = text;

        public void ShowCancelButton(bool show) =>
            m_Cancel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        public void EnableOKButton(bool enable) => m_OK.SetEnabled(enable);

        public new class UxmlFactory : UxmlFactory<OKCancelElement, UxmlTraits> { }
        public new class UxmlTraits : VisualElement.UxmlTraits { }
    }
}
