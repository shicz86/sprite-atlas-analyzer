using System;
using System.Reflection;
using UnityEngine;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    static class TextureUtilCompat
    {
        static readonly MethodInfo s_GetStorageMemorySizeLong;

        static TextureUtilCompat()
        {
            var textureUtilType = typeof(Editor).Assembly.GetType("UnityEditor.TextureUtil");
            if (textureUtilType != null)
            {
                s_GetStorageMemorySizeLong = textureUtilType.GetMethod(
                    "GetStorageMemorySizeLong",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(Texture) },
                    null);
            }
        }

        public static long GetStorageMemorySizeLong(Texture texture)
        {
            if (!texture)
                return 0;

            if (s_GetStorageMemorySizeLong != null)
            {
                try
                {
                    return (long)s_GetStorageMemorySizeLong.Invoke(null, new object[] { texture });
                }
                catch
                {
                    // fall through
                }
            }

            return UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
        }
    }
}
