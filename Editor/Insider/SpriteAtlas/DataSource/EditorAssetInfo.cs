using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    [Serializable]
    class EditorAssetInfo<T> where T : Object
    {
        [SerializeField]
        LazyLoadReference<Object> m_Object;
        [SerializeField]
        string m_AssetPath;
        [SerializeField]
        string m_Name;

        public EditorAssetInfo(int instanceId, string assetPath)
        {
            m_AssetPath = assetPath;
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            m_Object = obj != null ? (Object)obj : (LazyLoadReference<Object>)instanceId;
            m_Name = m_Object.GetAsset()?.name ?? obj?.name ?? $"{instanceId}";
        }

        public virtual string assetPath => m_AssetPath;

        public virtual string name
        {
            get => m_Name;
            set => m_Name = value;
        }

        public T GetObject() => m_Object.GetAsset() as T;

        public int instanceId => m_Object.instanceId;
        public string globalObjectIDString => m_Object.globalObjectIDString;
    }
}
