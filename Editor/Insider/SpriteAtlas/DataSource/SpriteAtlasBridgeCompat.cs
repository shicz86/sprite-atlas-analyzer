using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor.Sprites;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    /// <summary>
    /// Compatibility bridge for Sprite Atlas editor APIs across Unity versions.
    /// Uses public APIs first, then reflection on internal UnityEditor.U2D types.
    /// </summary>
    static class SpriteAtlasBridgeCompat
    {
        static readonly MethodInfo s_GetPreviewTextures;
        static readonly MethodInfo s_GetTextureFormat;
        static readonly MethodInfo s_GetSpriteTextureExtensions;
        static readonly MethodInfo s_GetTexturePlatformSerializationName;
        static bool s_LoggedMissingPreviewApi;

        public const string k_DefaultPlatformName = "Default";

        static SpriteAtlasBridgeCompat()
        {
            var extensionsType = typeof(SpriteAtlasExtensions);
            s_GetPreviewTextures = extensionsType.GetMethod(
                "GetPreviewTextures",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(SpriteAtlas) },
                null);

            s_GetTextureFormat = extensionsType.GetMethod(
                "GetTextureFormat",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(SpriteAtlas), typeof(BuildTarget) },
                null);

            s_GetSpriteTextureExtensions = extensionsType.GetMethod(
                "GetSpriteTexture",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Sprite), typeof(bool) },
                null);

            s_GetTexturePlatformSerializationName = typeof(TextureImporter).GetMethod(
                "GetTexturePlatformSerializationName",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
        }

        public static Texture2D[] GetSpriteAtlasTextures(SpriteAtlas atlas, bool warnIfEmpty = false)
        {
            if (!atlas)
                return Array.Empty<Texture2D>();

            if (s_GetPreviewTextures == null)
            {
                if (warnIfEmpty && !s_LoggedMissingPreviewApi)
                {
                    s_LoggedMissingPreviewApi = true;
                    Debug.LogWarning("[SpriteAtlasAnalyzer] GetPreviewTextures API not found. Packed atlas preview may be unavailable on this Editor version.");
                }
                return Array.Empty<Texture2D>();
            }

            try
            {
                var textures = s_GetPreviewTextures.Invoke(null, new object[] { atlas }) as Texture2D[];
                if (textures != null && textures.Length > 0)
                    return textures;

                if (warnIfEmpty)
                {
                    Debug.LogWarning(
                        $"[SpriteAtlasAnalyzer] No preview textures for Sprite Atlas '{atlas.name}'. " +
                        "The atlas may need to be packed first, or FitData will be used when available.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpriteAtlasAnalyzer] GetPreviewTextures failed for '{atlas.name}': {ex.Message}");
            }

            return Array.Empty<Texture2D>();
        }

        public static Texture2D GetSpriteTexture(Sprite sprite, bool fromAtlas)
        {
            if (!sprite)
                return null;

            if (fromAtlas && s_GetSpriteTextureExtensions != null)
            {
                try
                {
                    var texture = s_GetSpriteTextureExtensions.Invoke(null, new object[] { sprite, fromAtlas }) as Texture2D;
                    if (texture)
                        return texture;
                }
                catch
                {
                    // fall through
                }
            }

            try
            {
                var texture = SpriteUtility.GetSpriteTexture(sprite, fromAtlas);
                if (texture)
                    return texture;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpriteAtlasAnalyzer] SpriteUtility.GetSpriteTexture failed for '{sprite.name}': {ex.Message}");
            }

            if (!fromAtlas && s_GetSpriteTextureExtensions != null)
            {
                try
                {
                    var texture = s_GetSpriteTextureExtensions.Invoke(null, new object[] { sprite, fromAtlas }) as Texture2D;
                    if (texture)
                        return texture;
                }
                catch
                {
                    // fall through
                }
            }

            if (!fromAtlas)
            {
                var path = AssetDatabase.GetAssetPath(sprite);
                if (!string.IsNullOrEmpty(path))
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                    foreach (var asset in assets)
                    {
                        if (asset is Texture2D tex)
                            return tex;
                    }
                }
            }

            return sprite.texture;
        }

        public static TextureFormat GetSpriteAtlasTextureFormat(SpriteAtlas atlas, BuildTarget buildTarget)
        {
            if (!atlas)
                return TextureFormat.RGBA32;

            if (s_GetTextureFormat != null)
            {
                try
                {
                    var format = s_GetTextureFormat.Invoke(null, new object[] { atlas, buildTarget });
                    if (format is TextureFormat tf)
                        return tf;
                }
                catch
                {
                    // fall through
                }
            }

            var settings = atlas.GetPlatformSettings(GetPlatformSerializationName(buildTarget));
            if (settings != null && settings.overridden)
                return (TextureFormat)settings.format;

            settings = atlas.GetPlatformSettings(GetDefaultPlatformSerializationName());
            if (settings != null)
                return (TextureFormat)settings.format;

            return TextureFormat.RGBA32;
        }

        public static string GetPlatformSerializationName(BuildTarget buildTarget)
        {
            var buildTargetName = buildTarget.ToString();
            if (s_GetTexturePlatformSerializationName != null)
            {
                try
                {
                    var platformName = s_GetTexturePlatformSerializationName.Invoke(null, new object[] { buildTargetName }) as string;
                    if (!string.IsNullOrEmpty(platformName))
                        return platformName;
                }
                catch
                {
                    // fall through
                }
            }

            return MapBuildTargetToPlatformName(buildTarget);
        }

        static string MapBuildTargetToPlatformName(BuildTarget buildTarget)
        {
            switch (buildTarget)
            {
                case BuildTarget.iOS:
                    return "iPhone";
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                    return "Standalone";
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.WebGL:
                    return "WebGL";
                case BuildTarget.tvOS:
                    return "tvOS";
                case BuildTarget.WSAPlayer:
                    return "Windows Store Apps";
                default:
                    return BuildPipeline.GetBuildTargetGroup(buildTarget).ToString();
            }
        }

        public static string GetDefaultPlatformSerializationName()
        {
            return k_DefaultPlatformName;
        }

        public static TextureImporterPlatformSettings GetActivePlatformSettings(SpriteAtlas atlas)
        {
            if (!atlas)
                return null;

            var activeTarget = EditorUserBuildSettings.activeBuildTarget;
            var activeSettings = atlas.GetPlatformSettings(GetPlatformSerializationName(activeTarget));
            if (activeSettings != null && activeSettings.overridden)
                return activeSettings;

            return atlas.GetPlatformSettings(GetDefaultPlatformSerializationName());
        }

        public static void PackAtlas(SpriteAtlas atlas)
        {
            if (!atlas)
                return;

            try
            {
                SpriteAtlasUtility.PackAtlases(
                    new[] { atlas },
                    EditorUserBuildSettings.activeBuildTarget);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpriteAtlasAnalyzer] PackAtlases failed for {atlas.name}: {ex.Message}");
            }
        }

        public static async Task PackAtlasAsync(SpriteAtlas atlas)
        {
            PackAtlas(atlas);
            await Task.Delay(10);
        }

        public static void CleanupAtlasPacking()
        {
            try
            {
                SpriteAtlasUtility.CleanupAtlasPacking();
            }
            catch
            {
                // optional on some versions
            }
        }
    }
}
