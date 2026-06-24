using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    [Serializable]
    class SpriteWithSecondayTextureCellData
    {
        public string name;
        public int count;
        public LazyLoadReference<Object> instanceId;
        public string icon;

        public string GetDisplayText(string bindingPath)
        {
            return bindingPath switch
            {
                "name" => name,
                "count" => count.ToString(),
                _ => string.Empty
            };
        }

        public static int Compare(SpriteWithSecondayTextureCellData a, SpriteWithSecondayTextureCellData b, string propertyToCompare)
        {
            switch (propertyToCompare)
            {
                case "name":
                case null:
                    return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                case "count":
                    return a.count.CompareTo(b.count);
                default:
                    return 0;
            }
        }
    }
}
