using System;
using UnityEditor.U2D.SpriteAtlasAnalyzer.UIElement.OKCancel;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class SpriteCountIssueSettings : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/SpriteAtlas/SpriteAtlasIssueReport/SpriteCountInAtlasIssue/SpriteCountIssueSettings.uxml";
        IntegerField m_SpriteCountField;
        event Action<int> OnSpriteCountChanged;
        int m_PageCount;
        OKCancelElement m_OkCancelElement;
        public SpriteCountIssueSettings(int pageCount)
        {
            m_PageCount = pageCount;
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml).CloneTree(this);
            m_OkCancelElement = this.Q<OKCancelElement>();
            m_OkCancelElement.SetOKButtonText("Apply");
            m_OkCancelElement.onOKClicked += OnOKClicked;
            m_OkCancelElement.ShowCancelButton(false);
            m_OkCancelElement.EnableOKButton(false);

            m_SpriteCountField = this.Q<IntegerField>("Count");
            m_SpriteCountField.RegisterValueChangedCallback(x =>
            {
                m_SpriteCountField.SetValueWithoutNotify(Math.Clamp(x.newValue,0, 1000));
                m_OkCancelElement.EnableOKButton(true);
            });
            m_SpriteCountField.SetValueWithoutNotify(m_PageCount);
        }

        public event Action<int> spriteCountChanged
        {
            add => OnSpriteCountChanged += value;
            remove => OnSpriteCountChanged -= value;
        }

        void OnOKClicked()
        {
            m_PageCount = m_SpriteCountField.value;
            OnSpriteCountChanged?.Invoke(m_SpriteCountField.value);
            m_OkCancelElement.EnableOKButton(true);
        }
    }
}
