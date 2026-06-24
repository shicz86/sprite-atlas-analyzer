using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    [Serializable]
    class EditorTextureInfo : EditorResourceUsageInfo<Texture2D>
    {
        [SerializeField]
        TextureFormat m_TextureFormat;

        [SerializeField]
        List<EditorSpriteInfo> m_SpriteInfo = new();

        public EditorTextureInfo(int entityId, string assetPath)
            : base(entityId, assetPath) { }

        public EditorTextureInfo(int entityId, string assetPath, int width, int height, TextureFormat textureFormat)
            : base(entityId, assetPath)
        {
            m_TextureFormat = textureFormat;
            this.width = width;
            this.height = height;
            totalArea = width * height;
            memorySize = EstimateStorageMemory(width, height, textureFormat);
        }

        public void CollectInfo(Texture2D texture, TextureFormat platformFormat)
        {
            width = texture.width;
            height = texture.height;
            m_TextureFormat = platformFormat;
            totalArea = width * height;
            memorySize = EstimateStorageMemory(Mathf.CeilToInt(width), Mathf.CeilToInt(height), platformFormat);
        }

        static ulong EstimateStorageMemory(int width, int height, TextureFormat textureFormat)
        {
            if (width <= 0 || height <= 0)
                return 0;

            var t = new Texture2D(width, height, textureFormat, false) { hideFlags = HideFlags.HideAndDontSave };
            var memory = (ulong)TextureUtilCompat.GetStorageMemorySizeLong(t);
            Object.DestroyImmediate(t);
            return memory;
        }

        public void AddSpriteInfo(Sprite sprite, bool bindAtlas = false)
        {
            var spritePath = AssetDatabase.GetAssetPath(sprite);
            var spriteInfo = new EditorSpriteInfo(sprite.GetInstanceID(), spritePath);
            spriteInfo.CollectSpriteInfo(sprite, bindAtlas);
            m_SpriteInfo.Add(spriteInfo);
            usedArea += spriteInfo.usedArea;
        }

        public TextureFormat textureFormat => m_TextureFormat;
        public virtual List<EditorSpriteInfo> spriteInfo => m_SpriteInfo;
        public ulong textureMemorySize => memorySize;
    }
}
