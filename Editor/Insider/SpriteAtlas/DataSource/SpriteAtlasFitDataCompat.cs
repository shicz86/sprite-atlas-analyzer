using System;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.U2D;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    /// <summary>
    /// Fit-data collection matching Unity 6 SpriteAtlasBridge.SpriteAtlasFitDataAsync behavior.
    /// Tries com.unity.2d.common first, then UnityEditor.U2D.SpritePacking.SpritePackUtility.
    /// </summary>
    sealed class SpriteAtlasFitDataCompat : IDisposable
    {
        const string k_CommonBridgeType = "UnityEditor.U2D.Common.SpriteAtlasBridge, Unity.2D.Common.Editor";

        readonly object m_CommonTask;
        readonly object m_NativeArray;
        readonly JobHandle m_JobHandle;
        readonly MethodInfo m_GetItem;
        readonly MethodInfo m_NativeDispose;
        readonly Func<object, bool> m_IsNativeCreated;
        readonly Func<int> m_NativeLength;
        readonly FieldInfo m_PageField;
        readonly FieldInfo m_TextureWidthField;
        readonly FieldInfo m_TextureHeightField;
        readonly FieldInfo m_SpriteGuidField;
        readonly FieldInfo m_GuidField;

        SpriteAtlasFitDataCompat(object commonTask)
        {
            m_CommonTask = commonTask;
        }

        SpriteAtlasFitDataCompat(
            object nativeArray,
            JobHandle jobHandle,
            MethodInfo getItem,
            MethodInfo nativeDispose,
            Func<object, bool> isNativeCreated,
            Func<int> nativeLength,
            FieldInfo pageField,
            FieldInfo textureWidthField,
            FieldInfo textureHeightField,
            FieldInfo spriteGuidField,
            FieldInfo guidField)
        {
            m_NativeArray = nativeArray;
            m_JobHandle = jobHandle;
            m_GetItem = getItem;
            m_NativeDispose = nativeDispose;
            m_IsNativeCreated = isNativeCreated;
            m_NativeLength = nativeLength;
            m_PageField = pageField;
            m_TextureWidthField = textureWidthField;
            m_TextureHeightField = textureHeightField;
            m_SpriteGuidField = spriteGuidField;
            m_GuidField = guidField;
        }

        public static SpriteAtlasFitDataCompat TryCreate(SpriteAtlas atlas, int spriteCount)
        {
            if (!atlas || spriteCount <= 0)
                return null;

            var fromCommon = TryCreateFromCommonPackage(atlas, spriteCount);
            if (fromCommon != null)
                return fromCommon;

            return TryCreateFromSpritePackUtility(atlas, spriteCount);
        }

        static SpriteAtlasFitDataCompat TryCreateFromCommonPackage(SpriteAtlas atlas, int spriteCount)
        {
            var bridgeType = Type.GetType(k_CommonBridgeType);
            if (bridgeType == null)
                return null;

            var method = bridgeType.GetMethod(
                "SpriteAtlasFitDataAsync",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(SpriteAtlas), typeof(int) },
                null);
            if (method == null)
                return null;

            try
            {
                var task = method.Invoke(null, new object[] { atlas, spriteCount });
                return task != null ? new SpriteAtlasFitDataCompat(task) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SpriteAtlasAnalyzer] com.unity.2d.common FitData failed for {atlas.name}: {ex.Message}");
                return null;
            }
        }

        static SpriteAtlasFitDataCompat TryCreateFromSpritePackUtility(SpriteAtlas atlas, int spriteCount)
        {
            var assembly = typeof(SpriteAtlasUtility).Assembly;
            var packUtilityType = assembly.GetType("UnityEditor.U2D.SpritePacking.SpritePackUtility");
            var fitInfoType = assembly.GetType("UnityEditor.U2D.SpritePacking.SpriteFitInfo");
            if (packUtilityType == null || fitInfoType == null)
                return null;

            var fitMethod = packUtilityType.GetMethod(
                "FitSpriteAtlas",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(NativeArray<>).MakeGenericType(fitInfoType) },
                null);
            if (fitMethod == null)
                return null;

            var pageField = fitInfoType.GetField("page");
            var textureWidthField = fitInfoType.GetField("textureWidth");
            var textureHeightField = fitInfoType.GetField("textureHeight");
            var spriteGuidField = fitInfoType.GetField("spriteGuid");
            var guidField = fitInfoType.GetField("guid");
            if (pageField == null || textureWidthField == null || textureHeightField == null
                || spriteGuidField == null || guidField == null)
                return null;

            object fitData = null;
            try
            {
                var nativeArrayType = typeof(NativeArray<>).MakeGenericType(fitInfoType);
                fitData = Activator.CreateInstance(nativeArrayType, spriteCount, Allocator.Persistent);
                var assetPath = AssetDatabase.GetAssetPath(atlas);
                var jobHandle = (JobHandle)fitMethod.Invoke(null, new[] { assetPath, fitData });

                var getItem = nativeArrayType.GetMethod("get_Item");
                var nativeDispose = nativeArrayType.GetMethod("Dispose");
                var isCreatedMethod = nativeArrayType.GetMethod("get_IsCreated");
                var lengthProperty = nativeArrayType.GetProperty("Length");

                return new SpriteAtlasFitDataCompat(
                    fitData,
                    jobHandle,
                    getItem,
                    nativeDispose,
                    arr => (bool)isCreatedMethod.Invoke(arr, null),
                    () => (int)lengthProperty.GetValue(fitData),
                    pageField,
                    textureWidthField,
                    textureHeightField,
                    spriteGuidField,
                    guidField);
            }
            catch (Exception ex)
            {
                if (fitData != null)
                {
                    try
                    {
                        fitData.GetType().GetMethod("Dispose")?.Invoke(fitData, null);
                    }
                    catch
                    {
                        // ignored
                    }
                }

                Debug.LogWarning($"[SpriteAtlasAnalyzer] SpritePackUtility.FitSpriteAtlas failed for {atlas.name}: {ex.Message}");
                return null;
            }
        }

        public int Count
        {
            get
            {
                if (m_CommonTask != null)
                    return (int)m_CommonTask.GetType().GetProperty("Count").GetValue(m_CommonTask);

                return m_NativeArray != null && m_IsNativeCreated(m_NativeArray) ? m_NativeLength() : 0;
            }
        }

        public int GetPage(int index)
        {
            if (m_CommonTask != null)
                return (int)m_CommonTask.GetType().GetMethod("GetPage").Invoke(m_CommonTask, new object[] { index });

            if (m_NativeArray == null || !m_IsNativeCreated(m_NativeArray) || index >= m_NativeLength())
                return -1;

            return (int)m_PageField.GetValue(m_GetItem.Invoke(m_NativeArray, new object[] { index }));
        }

        public Vector2Int GetPageSize(int index)
        {
            if (m_CommonTask != null)
                return (Vector2Int)m_CommonTask.GetType().GetMethod("GetPageSize").Invoke(m_CommonTask, new object[] { index });

            if (m_NativeArray == null || !m_IsNativeCreated(m_NativeArray) || index >= m_NativeLength())
                return default;

            var item = m_GetItem.Invoke(m_NativeArray, new object[] { index });
            return new Vector2Int(
                (int)m_TextureWidthField.GetValue(item),
                (int)m_TextureHeightField.GetValue(item));
        }

        public GUID GetSpriteID(int index)
        {
            if (m_CommonTask != null)
                return (GUID)m_CommonTask.GetType().GetMethod("GetSpriteID").Invoke(m_CommonTask, new object[] { index });

            if (m_NativeArray == null || !m_IsNativeCreated(m_NativeArray) || index >= m_NativeLength())
                return default;

            return (GUID)m_SpriteGuidField.GetValue(m_GetItem.Invoke(m_NativeArray, new object[] { index }));
        }

        public GUID GetGUID(int index)
        {
            if (m_CommonTask != null)
                return (GUID)m_CommonTask.GetType().GetMethod("GetGUID").Invoke(m_CommonTask, new object[] { index });

            if (m_NativeArray == null || !m_IsNativeCreated(m_NativeArray) || index >= m_NativeLength())
                return default;

            return (GUID)m_GuidField.GetValue(m_GetItem.Invoke(m_NativeArray, new object[] { index }));
        }

        public async Task WaitForJob()
        {
            if (m_CommonTask != null)
            {
                var waitMethod = m_CommonTask.GetType().GetMethod("WaitForJob");
                if (waitMethod != null)
                {
                    var task = waitMethod.Invoke(m_CommonTask, null) as Task;
                    if (task != null)
                        await task;
                }
                return;
            }

            while (!m_JobHandle.IsCompleted)
                await Task.Delay(10);
            m_JobHandle.Complete();
        }

        public void Dispose()
        {
            if (m_CommonTask is IDisposable disposable)
            {
                disposable.Dispose();
                return;
            }

            if (m_NativeArray != null && m_NativeDispose != null)
            {
                try
                {
                    m_NativeDispose.Invoke(m_NativeArray, null);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
