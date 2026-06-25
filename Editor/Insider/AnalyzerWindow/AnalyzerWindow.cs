using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.SpriteAtlasAnalyzer
{
    class AnalyzerWindow : EditorWindow, IDataSourceProvider
    {
        const string k_SaveFilePath = "Library/com.unity.2d.sprite-atlas-analyzer/AnalyzerWindow/AnalyzerData.json";
        AnalyzerWindowSaveData m_SaveData = new();
        List<IAnalyzerReport> m_AllReports;
        List<IAnalyzerReport> m_Reports;
        List<IReportDataSource> m_DataSource;
        event Action m_OnDataSourceChange;
        IMGUIContainer m_AnalyzeButtonsIMGUI;
        bool m_AnalyzeButtonsEnabled = true;
        bool m_ShowAnalyzeDataSourceButton = true;
        bool m_AnalyzeButtonStylesInitialized;
        GUIStyle m_AnalyzeButtonStyle;
        GUIStyle m_AnalyzeStopButtonStyle;
        GUIStyle m_AnalyzeDataSourceButtonStyle;
        GUIStyle m_AnalyzeDataSourceStopButtonStyle;
        Button m_ClearDataSourceDataButton;
        ReportListView m_ReportListView;
        VisualElement m_ReportContentView;
        VisualElement m_ReportContentWithSettingsView;
        VisualElement m_ReportSettingView;
        VisualElement m_EmptyState;
        VisualElement m_DataSourceSetting;
        Button m_HelpButton;
        DataSourceList m_DataSourceListView;
        TwoPaneSplitView m_SplitViewReport;
        Label m_ReportHeaderLabel;
        VisualElement m_ReportArea;
        bool m_Capturing;

        [MenuItem("Window/Analysis/Sprite Atlas Analyzer")]
        static void OpenWindow()
        {
            var window = GetWindow<AnalyzerWindow>();
            window.Show();
        }

        internal async Task Analyze(string[] path)
        {
            if (m_Capturing)
            {
                for(int i = 0; i < m_SaveData.reportDataSource.Count; ++i)
                {
                    if(m_SaveData.reportDataSource[i].reportDataSource.capturing)
                        m_SaveData.reportDataSource[i].reportDataSource.StopCapture();
                }

                OnDataSourceCaptureEnd(null);
            }
            if (path == null)
            {
                if (!m_Capturing)
                {
                    Capture(null, false);
                }
            }
            else
            {
                // wait for previous capture to finish before we start a new one from link
                while (m_Capturing)
                    await Task.Delay(100);
                Capture(path, true);
            }
        }

        void Capture(string[] path, bool enableAll)
        {
            m_Capturing = true;
            RepaintAnalyzeButtons();
            int dataSourceCapturing = 0;
            enableAll |= m_SaveData.reportDataSource.Count == 1;
            for(int i = 0; i < m_SaveData.reportDataSource.Count; ++i)
            {
                if (m_SaveData.reportDataSource[i].enabled || enableAll)
                {
                    var reportDataSource = m_SaveData.reportDataSource[i].reportDataSource;
                    reportDataSource.onCaptureEnd -= OnDataSourceCaptureEnd;
                    var paths = path ?? m_SaveData.reportDataSource[i].assetSearchPath;
                    reportDataSource.Capture(Utilities.RemoveChildDirectories(paths));
                    ++dataSourceCapturing;
                }
            }

            for (int i = 0; i < m_SaveData.reportDataSource.Count; ++i)
            {
                if (m_SaveData.reportDataSource[i].enabled || enableAll)
                {
                    var reportDataSource = m_SaveData.reportDataSource[i].reportDataSource;
                    if(reportDataSource.capturing)
                        reportDataSource.onCaptureEnd += OnDataSourceCaptureEnd;
                    else
                        OnDataSourceCaptureEnd(reportDataSource);
                }
            }

            if (dataSourceCapturing == 0)
                OnDataSourceCaptureEnd(null);
        }

        protected void OnEnable()
        {
            titleContent = new GUIContent("Sprite Atlas Analyzer");
            m_SaveData = Utilities.LoadSaveDataFromFile<AnalyzerWindowSaveData>(k_SaveFilePath) ?? new AnalyzerWindowSaveData();
        }

        protected  void OnDisable()
        {
            for(int i = 0; i < m_AllReports?.Count; ++i)
            {
                m_AllReports[i].onInspectObject -= SelectUnityObject;
                m_AllReports[i].Dispose();
            }

            m_AllReports = null;

            for(int i = 0; i < m_DataSource?.Count; ++i)
            {
                m_DataSource[i].Dispose();
            }

            m_DataSource = null;
            Utilities.WriteSaveDataToFile(k_SaveFilePath, m_SaveData);
        }
        public void CreateGUI()
        {
            rootVisualElement.Add(GetWindowContent());
        }

        VisualElement GetWindowContent()
        {
            // Instantiate UXML
            VisualElement view = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.2d.sprite-atlas-analyzer/Editor/Insider/AnalyzerWindow/AnalyzerWindow.uxml").Instantiate();

            if(EditorGUIUtility.isProSkin)
                view.AddToClassList("dark");

            m_ReportListView = view.Q<ReportListView>("IssueList");
            m_ReportListView.selectionChanged += OnSelectionChanged;

            m_ReportArea = view.Q<VisualElement>("ReportArea");
            m_ReportArea.style.display = DisplayStyle.None;
            m_ReportContentView = view.Q<VisualElement>("ReportContainer");
            m_ReportContentView.style.display = DisplayStyle.None;
            m_ReportHeaderLabel = view.Q<Label>("HeaderLabel");

            m_SplitViewReport = view.Q<TwoPaneSplitView>("SplitViewReport");
            m_SplitViewReport.style.display = DisplayStyle.None;

            m_ReportContentWithSettingsView = view.Q<VisualElement>("ReportContainerWithSettings");
            m_ReportSettingView = view.Q<VisualElement>("ReportSettings");

            m_EmptyState = view.Q<VisualElement>("EmptyState");
            m_DataSourceSetting = view.Q<VisualElement>("DataSourceSetting");
            m_DataSourceSetting.style.display = DisplayStyle.None;
            m_DataSourceListView = view.Q<DataSourceList>("DataSourceListView");
            InitReports();
            m_DataSourceListView.SetDataSource(m_SaveData.reportDataSource);

            m_ClearDataSourceDataButton = m_DataSourceListView.Q<Button>("ClearDataSourcesButton");
            if (m_ClearDataSourceDataButton != null)
                m_ClearDataSourceDataButton.clicked += OnClearDataSourceDataButtonClicked;

            m_ReportListView.SetListDataSource(m_Reports);
            m_ReportListView.itemIndexChanged += OnItemIndexChanged;

            m_AnalyzeButtonsIMGUI = view.Q<IMGUIContainer>("AnalyzeButtons");
            m_AnalyzeButtonsIMGUI.onGUIHandler = DrawAnalyzeButtons;

            m_HelpButton = view.Q<Button>("HelpButton");
            m_HelpButton.clicked += () =>
            {
                EditorUtility.DisplayDialog(
                    "Sprite Atlas Analyzer",
                    "Analyze Sprite Atlases via Window > Analysis > Sprite Atlas Analyzer.\n\n" +
                    "See package README for built-in reports and custom extension guide.",
                    "OK");
            };
            return view;
        }

        void EnsureAnalyzeButtonStyles()
        {
            if (m_AnalyzeButtonStylesInitialized)
                return;

            m_AnalyzeButtonStyle = CreateFlatButtonStyle(
                new Color(14f / 255f, 142f / 255f, 87f / 255f),
                new Color(14f / 255f, 152f / 255f, 87f / 255f));
            m_AnalyzeStopButtonStyle = CreateFlatButtonStyle(
                new Color(235f / 255f, 87f / 255f, 87f / 255f),
                new Color(255f / 255f, 87f / 255f, 87f / 255f));
            m_AnalyzeDataSourceButtonStyle = CreateFlatButtonStyle(
                new Color(11f / 255f, 116f / 255f, 71f / 255f),
                new Color(12f / 255f, 125f / 255f, 76f / 255f));
            m_AnalyzeDataSourceStopButtonStyle = CreateFlatButtonStyle(
                new Color(198f / 255f, 73f / 255f, 73f / 255f),
                new Color(215f / 255f, 80f / 255f, 80f / 255f));
            m_AnalyzeButtonStylesInitialized = true;
        }

        static GUIStyle CreateFlatButtonStyle(Color normalColor, Color hoverColor)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
                fontStyle = FontStyle.Normal
            };

            var normalBackground = CreateSolidTexture(normalColor);
            var hoverBackground = CreateSolidTexture(hoverColor);
            style.normal.background = normalBackground;
            style.hover.background = hoverBackground;
            style.active.background = hoverBackground;
            style.focused.background = normalBackground;
            style.onNormal.background = normalBackground;
            style.onHover.background = hoverBackground;
            style.onActive.background = hoverBackground;
            style.onFocused.background = normalBackground;
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.onNormal.textColor = Color.white;
            style.onHover.textColor = Color.white;
            style.onActive.textColor = Color.white;
            style.onFocused.textColor = Color.white;
            return style;
        }

        static Texture2D CreateSolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        void DrawAnalyzeButtons()
        {
            EnsureAnalyzeButtonStyles();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                var previousEnabled = GUI.enabled;
                GUI.enabled = m_AnalyzeButtonsEnabled;

                var mainStyle = m_Capturing ? m_AnalyzeStopButtonStyle : m_AnalyzeButtonStyle;
                var rightStyle = m_Capturing ? m_AnalyzeDataSourceStopButtonStyle : m_AnalyzeDataSourceButtonStyle;
                if (GUILayout.Button(m_Capturing ? "Stop" : "Analyze", mainStyle, GUILayout.Width(150), GUILayout.Height(30)))
                    EditorApplication.delayCall += () => _ = Analyze(null);

                if (m_ShowAnalyzeDataSourceButton)
                {
                    GUI.enabled = m_AnalyzeButtonsEnabled && !m_Capturing;
                    if (GUILayout.Button("...", rightStyle, GUILayout.Width(20), GUILayout.Height(30)))
                        EditorApplication.delayCall += OnAnalyzeDataSourceButtonClicked;
                }

                GUI.enabled = previousEnabled;
                GUILayout.FlexibleSpace();
            }
        }

        void RepaintAnalyzeButtons()
        {
            m_AnalyzeButtonsIMGUI?.MarkDirtyRepaint();
        }

        void OnAnalyzeDataSourceButtonClicked()
        {
            m_ReportListView.selectedIndex = -1;
            m_DataSourceSetting.style.display = DisplayStyle.Flex;
            m_EmptyState.style.display = DisplayStyle.None;
            m_ReportArea.style.display = DisplayStyle.None;
            m_ReportHeaderLabel.text = "Data Source Configuration";
        }

        void OnItemIndexChanged(int arg1, int arg2)
        {
            m_SaveData.SaveReportPosition(m_Reports);
        }

        void InitReports()
        {
            if (m_DataSource == null)
            {
                m_DataSource = CollectReportDataSource();
                var dataSourceData = new List<DataSourceData>();
                for(int i = 0; i < m_DataSource.Count; ++i)
                {
                    if (m_SaveData?.saveData != null)
                        m_DataSource[i].Load(m_SaveData.saveData);
                    dataSourceData.Add(new DataSourceData
                    {
                        enabled = true,
                        typeName = m_DataSource[i].GetType().FullName,
                        reportDataSource = m_DataSource[i]
                    });
                    for(int j = 0; j < m_SaveData.reportDataSource?.Count; ++j)
                    {
                        if (m_SaveData.reportDataSource[j].typeName == dataSourceData[i].typeName)
                        {
                            dataSourceData[i].enabled = m_SaveData.reportDataSource[j].enabled;
                            dataSourceData[i].assetSearchPath = m_SaveData.reportDataSource[j].assetSearchPath;
                            break;
                        }
                    }
                }

                m_OnDataSourceChange?.Invoke();
                m_SaveData.reportDataSource = dataSourceData;
                if (m_DataSource.Count < 1)
                {
                    m_ShowAnalyzeDataSourceButton = false;
                }
            }

            if (m_AllReports == null)
            {
                m_AllReports = CollectAnalyzerReport(this);
                SetReportOrder();
                for(int i = 0; i < m_Reports.Count; ++i)
                {
                    //m_Reports[i].onShowReport += ShowReport;
                    m_Reports[i].onInspectObject += SelectUnityObject;
                }
            }
        }

        void SetReportOrder()
        {
            m_SaveData ??= new AnalyzerWindowSaveData();
            m_Reports = m_SaveData.OrderReport(m_AllReports);
        }

       async void OnDataSourceCaptureEnd(IReportDataSource dataSource)
        {
            if (m_Capturing)
            {
                for (int i = 0; i < m_DataSource.Count; ++i)
                {
                    if (m_DataSource[i].capturing)
                        return;
                }
                m_AnalyzeButtonsEnabled = false;
                RepaintAnalyzeButtons();
                var saveData = new SaveData();
                for(int i = 0; i < m_DataSource.Count; ++i)
                {
                    m_DataSource[i].Save(saveData);
                }

                m_SaveData.saveData = saveData;

                await UnloadResources();

                m_Capturing = false;
                m_AnalyzeButtonsEnabled = true;
                RepaintAnalyzeButtons();
            }
        }

        static async Task UnloadResources()
        {
            var op = Resources.UnloadUnusedAssets();
            var progress = new AnalyzerProgress();
            progress.StartProgressTrack();
            while (!op.isDone)
            {
                progress.UpdateProgressTrack(op.progress, "Unloading analyzed resources...");
                await Task.Delay(100);
            }
            progress.EndProgressTrack();
        }

        static List<IAnalyzerReport> CollectAnalyzerReport(IDataSourceProvider dataSourceProvider)
        {
            List<IAnalyzerReport> report = new List<IAnalyzerReport>();
            foreach (var moduleClassType in TypeCache.GetTypesDerivedFrom<IAnalyzerReport>())
            {
                if (moduleClassType.IsAbstract || !moduleClassType.IsClass)
                    continue;
                bool notDirectlyImplement = false;
                // we only want anyone that is directly implementing IAnalyzerReport
                foreach (var @interface in moduleClassType.GetInterfaces())
                {
                    if (!(@interface == typeof(IAnalyzerReport)) &&
                        typeof(IAnalyzerReport).IsAssignableFrom(@interface))
                    {
                        notDirectlyImplement = true;
                        break;
                    }
                }

                if (notDirectlyImplement)
                    continue;
                var constructorType = new Type[0];
                // Get the public instance constructor that takes ISpriteEditorModule parameter.
                var constructorInfoObj = moduleClassType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, null,
                    CallingConventions.HasThis, constructorType, null);
                if (constructorInfoObj != null)
                {
                    try
                    {
                        var newInstance = constructorInfoObj.Invoke(new object[0]) as IAnalyzerReport;
                        if (newInstance != null)
                        {
                            newInstance.SetDataSourceProvider(dataSourceProvider);
                            report.Add(newInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Unable to instantiate Analyzer Report " + moduleClassType.FullName + ". Exception:" + ex);
                    }
                }
                else
                    Debug.LogWarning(moduleClassType.FullName + " does not have a parameterless constructor");
            }

            return report;
        }

        static List<IReportDataSource> CollectReportDataSource()
        {
            List<IReportDataSource> report = new List<IReportDataSource>();
            foreach (var moduleClassType in TypeCache.GetTypesDerivedFrom<IReportDataSource>())
            {
                if (moduleClassType.IsAbstract || !moduleClassType.IsClass)
                    continue;
                var constructorType = new Type[0];
                // Get the public instance constructor that takes ISpriteEditorModule parameter.
                var constructorInfoObj = moduleClassType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public, null,
                    CallingConventions.HasThis, constructorType, null);
                if (constructorInfoObj != null)
                {
                    try
                    {
                        var newInstance = constructorInfoObj.Invoke(new object[0]) as IReportDataSource;
                        if (newInstance != null)
                        {
                            report.Add(newInstance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Unable to instantiate Analyzer Report " + moduleClassType.FullName + ". Exception:" + ex);
                    }
                }
                else
                    Debug.LogWarning(moduleClassType.FullName + " does not have a parameterless constructor");
            }

            return report;
        }

        public T GetDataSource<T>() where T : class, IReportDataSource
        {
            for(int i = 0; i < m_DataSource.Count; ++i)
            {
                if(m_DataSource[i] is T result)
                    return result;
            }

            return null;
        }

        public IReportDataSource GetDataSource(Type t)
        {
            for(int i = 0; i < m_DataSource.Count; ++i)
            {
                if(m_DataSource[i].GetType() == t)
                    return m_DataSource[i];
            }
            return null;
        }

        public event Action onDataSourceChanged
        {
            add => m_OnDataSourceChange += value;
            remove => m_OnDataSourceChange -= value;
        }

        public void SelectUnityObject(IAnalyzerReport report, Object obj)
        {
            if (!obj)
                return;

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        void OnSelectionChanged(IEnumerable<object> _)
        {
            var index = m_ReportListView.selectedIndex;
            if(index >= 0 && index < m_Reports.Count)
            {
                m_EmptyState.style.display = DisplayStyle.None;
                m_DataSourceSetting.style.display = DisplayStyle.None;
                m_ReportArea.style.display = DisplayStyle.Flex;
                var contentItem = m_Reports[index].reportContent;
                var settingsContent = m_Reports[index].settingsContent;
                m_ReportContentView.Clear();
                m_ReportSettingView.Clear();
                m_ReportContentWithSettingsView.Clear();
                m_ReportHeaderLabel.text = m_Reports[index].reportTitle;
                if (settingsContent == null)
                {
                    m_SplitViewReport.style.display = DisplayStyle.None;
                    m_ReportContentView.style.display = DisplayStyle.Flex;
                    m_ReportContentView.Add(contentItem);
                }
                else
                {
                    m_SplitViewReport.style.display = DisplayStyle.Flex;
                    m_ReportContentView.style.display = DisplayStyle.None;
                    m_ReportContentWithSettingsView.Add(contentItem);
                    m_ReportSettingView.Add(settingsContent);
                }
            }
            else
            {
                m_SplitViewReport.style.display = DisplayStyle.None;
                m_ReportContentView.style.display = DisplayStyle.None;
                m_EmptyState.style.display = DisplayStyle.Flex;
                m_ReportHeaderLabel.text = "No Report Selected";
            }
        }

        void OnClearDataSourceDataButtonClicked()
        {
            m_SaveData.saveData = new SaveData();
            for(int i = 0; i < m_DataSource.Count; ++i)
            {
                m_DataSource[i].Load(m_SaveData.saveData);
            }
            m_OnDataSourceChange?.Invoke();
        }

        public static void ClearSaveData()
        {
            Utilities.WriteSaveDataToFile(k_SaveFilePath, new AnalyzerWindowSaveData());
        }
    }
}
