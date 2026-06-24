using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    [Serializable]
    class EditorSpriteInfo : EditorResourceUsageInfo<Sprite>
    {
        const float k_OutlineTolerance = 0.01f;

        [SerializeField]
        float m_SpriteAtlasTextureScale;
        [SerializeField]
        SpritePackingMode m_SpritePackingMode;
        [SerializeField]
        TextureFormat m_TextureFormat;
        [SerializeField]
        string m_SpriteID;

        public EditorSpriteInfo(int instanceId, string assetPath)
            : base(instanceId, assetPath) { }

        public void CollectSpriteInfo(Sprite spriteObject, bool bindAtlas = false)
        {
            Profiler.BeginSample("CollectSpriteInfo");
            var sprite = spriteObject ?? GetObject();
            if (!sprite)
            {
                Profiler.EndSample();
                return;
            }

            var vertexCountBeforeBind = sprite.vertices.Length;

            if (bindAtlas)
                SpriteAtlasBridgeCompat.GetSpriteTexture(sprite, true);

            m_SpriteID = sprite.GetSpriteID().ToString();
            memorySize = (ulong)Profiler.GetRuntimeMemorySizeLong(sprite);
            m_SpriteAtlasTextureScale = sprite.spriteAtlasTextureScale;
            if (m_SpriteAtlasTextureScale <= 0f)
                m_SpriteAtlasTextureScale = 1f;

            var textureRect = GetSpriteTextureRect(sprite);
            width = Mathf.Ceil(textureRect.width * m_SpriteAtlasTextureScale);
            height = Mathf.Ceil(textureRect.height * m_SpriteAtlasTextureScale);

            m_SpritePackingMode = ResolvePackingMode(sprite, bindAtlas, vertexCountBeforeBind);

            if (m_SpritePackingMode == SpritePackingMode.Rectangle)
                usedArea = width * height;
            else
            {
                Profiler.BeginSample("CalculateMeshArea");
                usedArea = CalculateMeshArea(sprite) * sprite.pixelsPerUnit * sprite.pixelsPerUnit;
                Profiler.EndSample();
            }

            var t = SpriteAtlasBridgeCompat.GetSpriteTexture(sprite, false);
            if (t)
                m_TextureFormat = GraphicsFormatUtility.GetTextureFormat(t.graphicsFormat);
            Profiler.EndSample();
            if (width * height < usedArea)
                usedArea = width * height;
        }

        static SpritePackingMode ResolvePackingMode(Sprite sprite, bool bindAtlas, int vertexCountBeforeBind)
        {
            if (IsRectangleSpriteMesh(sprite))
            {
                // 2022.3: atlas bind can flatten a 3-vertex tight mesh into a 4-vertex rect hull.
                if (bindAtlas && vertexCountBeforeBind > 0 && vertexCountBeforeBind < 4)
                    return SpritePackingMode.Tight;

                return SpritePackingMode.Rectangle;
            }

            if (!bindAtlas
                && TryGetSpriteMeshType(sprite, out var creationMesh)
                && creationMesh == SpriteMeshType.FullRect)
                return SpritePackingMode.Rectangle;

            return SpritePackingMode.Tight;
        }

        static bool TryGetSpriteMeshType(Sprite sprite, out SpriteMeshType meshType)
        {
            meshType = SpriteMeshType.FullRect;
            var path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path))
                return false;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            meshType = settings.spriteMeshType;
            return true;
        }

        /// <summary>
        /// Unity 6000.3: vertices.Length == 4 && rect.size == textureRect.size.
        /// 2022.3 extension: bound Full Rect sprites may keep >4 vertices on the rect outline.
        /// </summary>
        static bool IsRectangleSpriteMesh(Sprite sprite)
        {
            var textureRect = GetSpriteTextureRect(sprite);
            if (sprite.rect.size != textureRect.size)
                return false;

            if (sprite.vertices.Length == 4)
                return true;

            return IsAxisAlignedRectOutline(sprite.vertices, sprite.rect);
        }

        static bool IsAxisAlignedRectOutline(Vector2[] vertices, Rect rect)
        {
            if (vertices == null || vertices.Length < 4)
                return false;

            for (int i = 0; i < vertices.Length; ++i)
            {
                var p = vertices[i];
                var onVertical = Mathf.Abs(p.x - rect.xMin) < k_OutlineTolerance
                                 || Mathf.Abs(p.x - rect.xMax) < k_OutlineTolerance;
                var onHorizontal = Mathf.Abs(p.y - rect.yMin) < k_OutlineTolerance
                                   || Mathf.Abs(p.y - rect.yMax) < k_OutlineTolerance;
                if (!onVertical && !onHorizontal)
                    return false;
            }

            return true;
        }

        static Rect GetSpriteTextureRect(Sprite sprite)
        {
            try
            {
                return sprite.textureRect;
            }
            catch
            {
                return sprite.rect;
            }
        }

        static float CalculateMeshArea(Sprite sprite)
        {
            var vertices = sprite.vertices;
            var triangles = sprite.triangles;
            float totalArea = 0f;
            for (int i = 0; i < triangles.Length;)
            {
                var v1 = vertices[triangles[i++]];
                var v2 = vertices[triangles[i++]];
                var v3 = vertices[triangles[i++]];
                var edge1 = v2 - v1;
                var edge2 = v3 - v1;
                totalArea += Mathf.Abs(edge1.x * edge2.y - edge1.y * edge2.x) * 0.5f;
            }

            return totalArea;
        }

        public virtual Texture2D GetSpriteTexture(bool fromAtlas)
        {
            var sprite = GetObject();
            if (!sprite)
                return null;
            return SpriteAtlasBridgeCompat.GetSpriteTexture(sprite, fromAtlas);
        }

        public SpritePackingMode spritePackingMode => m_SpritePackingMode;
        public TextureFormat textureFormat => m_TextureFormat;
    }
}
