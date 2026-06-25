using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    [Serializable]
    struct LazyLoadReference<T> where T : Object
    {
        GlobalObjectId m_GlobalObjectID;
        [SerializeField]
        SerializableGuid m_GUID;
        [SerializeField]
        long m_LocalFileID;
        [SerializeField]
        bool m_ValidReference;
        [SerializeField]
        string m_GlobalObjectIDString;

        // Runtime cache only — not serialized (matches Unity 6 EntityId behavior after domain reload).
        int m_InstanceId;

        public LazyLoadReference(T obj)
        {
            m_GlobalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            m_GlobalObjectIDString = m_GlobalObjectID.ToString();
            if (obj)
            {
                m_ValidReference = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out m_LocalFileID);
                m_GUID = new SerializableGuid(guid);
                m_InstanceId = obj.GetInstanceID();
            }
            else
            {
                m_ValidReference = false;
                m_InstanceId = 0;
                m_LocalFileID = 0;
                m_GUID = new SerializableGuid(new GUID());
            }
        }

        public LazyLoadReference(int instanceId)
        {
            m_InstanceId = instanceId;
            m_GlobalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(instanceId);
            m_GlobalObjectIDString = m_GlobalObjectID.ToString();
            m_ValidReference = false;
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            if (obj != null)
            {
                m_InstanceId = obj.GetInstanceID();
                m_ValidReference = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out string guid, out m_LocalFileID);
                m_GUID = new SerializableGuid(guid);
            }
            else
            {
                m_InstanceId = instanceId;
                m_LocalFileID = 0;
                m_GUID = new SerializableGuid(new GUID());
            }
        }

        public static implicit operator LazyLoadReference<T>(T asset) => new LazyLoadReference<T>(asset);

        public static implicit operator LazyLoadReference<T>(int instanceId) => new LazyLoadReference<T>(instanceId);

        public T GetAsset()
        {
            ResolveInstanceIdIfNeeded();
            if (m_InstanceId != 0)
            {
                var obj = EditorUtility.InstanceIDToObject(m_InstanceId) as T;
                if (obj)
                    return obj;
            }

            if (!string.IsNullOrEmpty(m_GlobalObjectIDString))
            {
                if (m_GlobalObjectID.assetGUID == new GUID())
                    GlobalObjectId.TryParse(m_GlobalObjectIDString, out m_GlobalObjectID);

                var fromGlobal = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(m_GlobalObjectID) as T;
                if (fromGlobal)
                {
                    m_InstanceId = fromGlobal.GetInstanceID();
                    return fromGlobal;
                }
            }

            return null;
        }

        void ResolveInstanceIdIfNeeded()
        {
            if (m_InstanceId != 0 && EditorUtility.InstanceIDToObject(m_InstanceId) != null)
                return;

            if (m_InstanceId != 0)
            {
                var obj = EditorUtility.InstanceIDToObject(m_InstanceId);
                if (obj)
                    return;
            }

            m_InstanceId = 0;
            if (string.IsNullOrEmpty(m_GlobalObjectIDString))
                return;

            if (m_GlobalObjectID.assetGUID == new GUID())
                GlobalObjectId.TryParse(m_GlobalObjectIDString, out m_GlobalObjectID);

            var resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(m_GlobalObjectID);
            if (resolved)
                m_InstanceId = resolved.GetInstanceID();
        }

        public int instanceId
        {
            get
            {
                ResolveInstanceIdIfNeeded();
                return m_InstanceId;
            }
        }

        public string globalObjectIDString => m_GlobalObjectIDString;
    }
}
