using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    [Serializable]
    class SourceTextureWithCompressionCellData
    {
        public string name;
        public string textureFormat;
        public LazyLoadReference<Object> asset;
        public EditorAtlasInfo atlasInfo;
        public string icon;

        public string GetDisplayText(string bindingPath)
        {
            return bindingPath switch
            {
                "name" => name,
                "textureFormat" => textureFormat,
                _ => string.Empty
            };
        }

        public static int Compare(SourceTextureWithCompressionCellData a, SourceTextureWithCompressionCellData b, string propertyToCompare)
        {
            if (a == null && b == null)
                return 0;
            if (a == null)
                return -1;
            if (b == null)
                return 1;

            switch (propertyToCompare)
            {
                case "name":
                case null:
                    return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                case "textureFormat":
                    return string.Compare(a.textureFormat, b.textureFormat, StringComparison.Ordinal);
                default:
                    return 0;
            }
        }
    }
}
