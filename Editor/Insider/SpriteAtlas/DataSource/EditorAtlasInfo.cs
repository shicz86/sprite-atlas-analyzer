using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.U2D;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    [Serializable]
    class EditorAtlasInfo : EditorResourceUsageInfo<SpriteAtlas>
    {
        [SerializeField]
        List<EditorTextureInfo> m_TextureInfo = new();
        [SerializeField]
        long m_FileModifiedTime;
        [SerializeField]
        long m_MetaFileModifiedTime;
        [SerializeField]
        Hash128 m_AssetHash;
        [SerializeField]
        TextureFormat m_TextureFormat;
        [SerializeField]
        SerializableGuid m_MasterAtlasGuid;
        [SerializeField]
        SerializableGuid m_AtlasGuid;
        [SerializeField]
        int m_ActiveBuildTarget;
        [SerializeField]
        int m_PackableSpriteCount;

        public EditorAtlasInfo(int instanceId, string assetPath)
            : base(instanceId, assetPath) { }

        public async Task CollectAtlasInfo()
        {
            Profiler.BeginSample("CollectAtlasInfo");
            var atlas = GetObject();
            var path = AssetDatabase.GetAssetPath(atlas);
            m_AtlasGuid = new SerializableGuid(AssetDatabase.GUIDFromAssetPath(path));
            m_FileModifiedTime = File.GetLastWriteTimeUtc(path).ToFileTimeUtc();
            m_AssetHash = AssetDatabase.GetAssetDependencyHash(path);
            path = AssetDatabase.GetTextMetaFilePathFromAssetPath(path);
            m_MetaFileModifiedTime = File.GetLastWriteTimeUtc(path).ToFileTimeUtc();
            m_TextureFormat = SpriteAtlasBridgeCompat.GetSpriteAtlasTextureFormat(atlas, EditorUserBuildSettings.activeBuildTarget);
            m_ActiveBuildTarget = (int)EditorUserBuildSettings.activeBuildTarget;
            memorySize = 0;
            usedArea = 0;
            totalArea = 0;
            width = 0;
            height = 0;
            m_TextureInfo.Clear();

            UnityEngine.Object[] packables = null;
            var masterAtlas = atlas.isVariant ? atlas.GetMasterAtlas() : null;
            if (masterAtlas != null)
            {
                var masterAtlasPath = AssetDatabase.GetAssetPath(masterAtlas);
                m_MasterAtlasGuid = new SerializableGuid(AssetDatabase.GUIDFromAssetPath(masterAtlasPath));
                packables = masterAtlas.GetPackables();
            }
            else
            {
                m_MasterAtlasGuid = null;
                packables = atlas.GetPackables();
            }

            var sprites = CollectSpritesFromPackables(packables);
            m_PackableSpriteCount = sprites.Count;
            var textures = GetAtlasMainTextures(atlas);

            if ((sprites.Count > 0 && textures.Count == 0) || !atlas.IsIncludeInBuild())
            {
                var atlasToFit = masterAtlas ?? atlas;
                var collected = await TryCollectFromFitData(atlasToFit, sprites);
                if (!collected)
                {
                    await SpriteAtlasBridgeCompat.PackAtlasAsync(atlasToFit);
                    if (atlas.isVariant && atlas != atlasToFit)
                        await SpriteAtlasBridgeCompat.PackAtlasAsync(atlas);
                    textures = GetAtlasMainTextures(atlas, warnIfEmpty: true);
                    if (textures.Count > 0)
                        await CollectFromPackedTextures(atlas, masterAtlas, sprites, textures);
                    else if (sprites.Count > 0)
                        CollectFromSyntheticPage(atlasToFit, sprites);
                }
            }
            else
            {
                await CollectFromPackedTextures(atlas, masterAtlas, sprites, textures);
            }

            EnsurePackableSpritesMapped(sprites);

            m_TextureInfo.Sort((x, y) => string.Compare(x.name, y.name, StringComparison.Ordinal));
            for (int i = 0; i < m_TextureInfo.Count; ++i)
            {
                var textureInfo = m_TextureInfo[i];
                // Sum area/memory before width/height: totalArea getter falls back to width*height
                // when m_TotalArea is 0, which would double-count on the first page.
                usedArea += textureInfo.usedArea;
                totalArea += textureInfo.totalArea;
                memorySize += textureInfo.textureMemorySize;
                width += textureInfo.width;
                height += textureInfo.height;
                textureInfo.name = $"MainTex - ({i})";
            }

            Profiler.EndSample();
        }

        async Task<bool> TryCollectFromFitData(
            SpriteAtlas atlasToFit,
            List<(Sprite sprite, GUID assetGUID)> sprites)
        {
            using var fitData = SpriteAtlasFitDataCompat.TryCreate(atlasToFit, sprites.Count);
            if (fitData == null)
                return false;

            await fitData.WaitForJob();

            var editorTextureInfoDict = new Dictionary<int, EditorTextureInfo>();
            for (int i = 0; i < fitData.Count; ++i)
            {
                var page = fitData.GetPage(i);
                if (page < 0)
                    continue;

                if (!editorTextureInfoDict.TryGetValue(page, out var existing))
                {
                    var size = fitData.GetPageSize(i);
                    editorTextureInfoDict[page] = new EditorTextureInfo(0, null, size.x, size.y, m_TextureFormat);
                    editorTextureInfoDict[page].name = $"{page}";
                }

                var editorTextureInfo = editorTextureInfoDict[page];
                foreach (var sprite in sprites)
                {
                    var spriteGUID = sprite.sprite.GetSpriteID();
                    if (spriteGUID == fitData.GetSpriteID(i))
                    {
                        editorTextureInfo.AddSpriteInfo(sprite.sprite);
                        break;
                    }
                }
            }

            foreach (var textureInfo in editorTextureInfoDict.Values)
            {
                if (textureInfo.spriteInfo.Count > 0)
                    m_TextureInfo.Add(textureInfo);
            }

            return m_TextureInfo.Count > 0;
        }

        void CollectFromSyntheticPage(
            SpriteAtlas atlas,
            List<(Sprite sprite, GUID assetGUID)> sprites)
        {
            Debug.LogWarning(
                $"[SpriteAtlasAnalyzer] FitData and Pack preview unavailable for '{atlas.name}'. " +
                "Using estimated single-page layout; metrics may differ from Unity 6 Analyzer.");

            var pageSize = GetAtlasMaxTextureSize(atlas);
            var editorTextureInfo = new EditorTextureInfo(0, null, pageSize, pageSize, m_TextureFormat);
            editorTextureInfo.name = "0";

            foreach (var sprite in sprites)
                editorTextureInfo.AddSpriteInfo(sprite.sprite);

            if (editorTextureInfo.spriteInfo.Count > 0)
                m_TextureInfo.Add(editorTextureInfo);
        }

        static int GetAtlasMaxTextureSize(SpriteAtlas atlas)
        {
            var platformSettings = SpriteAtlasBridgeCompat.GetActivePlatformSettings(atlas);
            if (platformSettings != null && platformSettings.maxTextureSize > 0)
                return platformSettings.maxTextureSize;

            return 2048;
        }

        static List<(Sprite sprite, GUID assetGUID)> CollectSpritesFromPackables(UnityEngine.Object[] packables)
        {
            var sprites = new List<(Sprite sprite, GUID assetGUID)>();
            if (packables == null)
                return sprites;

            for (int i = 0; i < packables.Length; ++i)
            {
                var packable = packables[i];
                if (!packable)
                    continue;
                if (packable is DefaultAsset folder)
                {
                    var folderPath = AssetDatabase.GetAssetPath(folder);
                    var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
                    for (int j = 0; j < spriteGuids.Length; ++j)
                    {
                        var spritePath = AssetDatabase.GUIDToAssetPath(spriteGuids[j]);
                        var assets = AssetDatabase.LoadAllAssetsAtPath(spritePath);
                        foreach (var asset in assets)
                        {
                            if (asset is Sprite sprite)
                            {
                                GUID.TryParse(spriteGuids[j], out GUID assetGUID);
                                sprites.Add((sprite, assetGUID));
                            }
                        }
                    }
                }
                else if (packable is Texture2D)
                {
                    var texturePath = AssetDatabase.GetAssetPath(packable);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(texturePath);
                    var assetGUID = AssetDatabase.GUIDFromAssetPath(texturePath);
                    foreach (var asset in assets)
                    {
                        if (asset is Sprite sprite)
                            sprites.Add((sprite, assetGUID));
                    }
                }
                else if (packable is Sprite sprite)
                {
                    sprites.Add((sprite, AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(sprite))));
                }
                else
                {
                    Debug.LogError("[SpriteAtlasAnalyzer] Unknown packable type: " + packable.GetType());
                }
            }

            return sprites;
        }

        async Task CollectFromPackedTextures(
            SpriteAtlas atlas,
            SpriteAtlas masterAtlas,
            List<(Sprite sprite, GUID assetGUID)> sprites,
            List<Texture2D> textures)
        {
            var spriteDict = new Dictionary<int, List<Sprite>>();
            var masterAtlasToVariantTextureDict = new Dictionary<int, int>();
            if (masterAtlas != null)
            {
                var masterTextures = GetAtlasMainTextures(masterAtlas);
                if (masterTextures.Count != textures.Count)
                    Debug.LogError($"[SpriteAtlasAnalyzer] Master atlas {masterAtlas.name} and variant {atlas.name} texture count mismatch.");

                for (int i = 0; i < masterTextures.Count && i < textures.Count; ++i)
                    masterAtlasToVariantTextureDict[masterTextures[i].GetInstanceID()] = textures[i].GetInstanceID();
            }

            for (int j = 0; j < sprites.Count; ++j)
            {
                if (j % 32 == 0)
                    await Task.Delay(1);

                var sprite = sprites[j].sprite;
                var texture = SpriteAtlasBridgeCompat.GetSpriteTexture(sprite, true);
                if (!texture)
                    continue;

                var textureId = texture.GetInstanceID();
                if (masterAtlasToVariantTextureDict.TryGetValue(textureId, out var variantId))
                    textureId = variantId;

                if (!spriteDict.TryGetValue(textureId, out var spriteList))
                {
                    spriteList = new List<Sprite>();
                    spriteDict[textureId] = spriteList;
                }
                spriteList.Add(sprite);
            }

            for (int i = 0; i < textures.Count; ++i)
            {
                var texture = textures[i];
                var texturePath = AssetDatabase.GetAssetPath(texture);
                var editorTextureInfo = new EditorTextureInfo(texture.GetInstanceID(), texturePath);
                editorTextureInfo.CollectInfo(texture, m_TextureFormat);
                if (!spriteDict.TryGetValue(texture.GetInstanceID(), out var spriteList) || spriteList.Count == 0)
                    spriteList = BuildSpriteListForTexture(texture, sprites);
                foreach (var sprite in spriteList)
                    editorTextureInfo.AddSpriteInfo(sprite, bindAtlas: true);
                m_TextureInfo.Add(editorTextureInfo);
            }
        }

        static List<Sprite> BuildSpriteListForTexture(
            Texture2D texture,
            List<(Sprite sprite, GUID assetGUID)> sprites)
        {
            var list = new List<Sprite>();
            if (texture == null || sprites == null)
                return list;

            var texturePath = AssetDatabase.GetAssetPath(texture);
            var textureId = texture.GetInstanceID();
            for (int j = 0; j < sprites.Count; ++j)
            {
                var packedTexture = SpriteAtlasBridgeCompat.GetSpriteTexture(sprites[j].sprite, true);
                if (!packedTexture)
                    continue;

                if (packedTexture == texture
                    || packedTexture.GetInstanceID() == textureId
                    || AssetDatabase.GetAssetPath(packedTexture) == texturePath)
                {
                    list.Add(sprites[j].sprite);
                }
            }

            return list;
        }

        void EnsurePackableSpritesMapped(List<(Sprite sprite, GUID assetGUID)> sprites)
        {
            if (sprites == null || sprites.Count == 0 || m_TextureInfo.Count == 0)
                return;

            int mapped = 0;
            for (int i = 0; i < m_TextureInfo.Count; ++i)
                mapped += m_TextureInfo[i].spriteInfo?.Count ?? 0;

            if (mapped > 0)
                return;

            for (int i = 0; i < sprites.Count; ++i)
                m_TextureInfo[0].AddSpriteInfo(sprites[i].sprite, bindAtlas: true);
        }

        public int GetDisplaySpriteCount()
        {
            int count = 0;
            for (int i = 0; i < m_TextureInfo.Count; ++i)
                count += m_TextureInfo[i].spriteInfo?.Count ?? 0;
            return count > 0 ? count : m_PackableSpriteCount;
        }

        static List<Texture2D> GetAtlasMainTextures(SpriteAtlas atlas, bool warnIfEmpty = false)
        {
            var textures = SpriteAtlasBridgeCompat.GetSpriteAtlasTextures(atlas, warnIfEmpty);
            var mainTextures = new List<Texture2D>();
            for (int i = 0; i < textures.Length; ++i)
            {
                var texName = textures[i].name;
                int j = 0;
                for (; j < textures.Length; ++j)
                {
                    if (j == i)
                        continue;
                    if (texName.StartsWith(textures[j].name))
                        break;
                }
                if (j >= textures.Length)
                    mainTextures.Add(textures[i]);
            }
            return mainTextures;
        }

        public virtual List<EditorTextureInfo> textureInfo => m_TextureInfo;
        public TextureFormat textureFormat => m_TextureFormat;
        public long metaFileModifiedTime => m_MetaFileModifiedTime;
        public Hash128 assetHash => m_AssetHash;
        public long fileModifiedTime => m_FileModifiedTime;
        public bool isVariant => masterAtlasGuid != null && masterAtlasGuid.isValid;
        public virtual SerializableGuid masterAtlasGuid => m_MasterAtlasGuid;
        public virtual SerializableGuid atlasGuid => m_AtlasGuid;

        public List<EditorSpriteInfo> spriteInfo
        {
            get
            {
                List<EditorSpriteInfo> sprites = new();
                foreach (var textureInfo in m_TextureInfo)
                    sprites.AddRange(textureInfo.spriteInfo);
                return sprites;
            }
        }

        public static bool HasAtlasChange(EditorAtlasInfo prevCaptureAtlasInfo, string atlasPath)
        {
            var hash = AssetDatabase.GetAssetDependencyHash(atlasPath);
            var fileTime = File.GetLastWriteTimeUtc(atlasPath).ToFileTimeUtc();
            var metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(atlasPath);
            var metaTime = File.GetLastWriteTimeUtc(metaPath).ToFileTimeUtc();
            return prevCaptureAtlasInfo?.fileModifiedTime != fileTime
                   || prevCaptureAtlasInfo?.metaFileModifiedTime != metaTime
                   || prevCaptureAtlasInfo?.assetHash != hash
                   || prevCaptureAtlasInfo?.m_ActiveBuildTarget != (int)EditorUserBuildSettings.activeBuildTarget;
        }
    }
}
