# Sprite Atlas Analyzer (Unity 2022.3)

Unity Editor package for analyzing Sprite Atlas memory usage, packing efficiency, and common configuration issues.

Based on the architecture of Unity 6 `com.unity.2d.tooling` Sprite Atlas Analyzer, adapted for **Unity 2022.3 LTS** with API compatibility fallbacks (reflection on internal `SpriteAtlasExtensions` where needed).

## Installation

1. Copy this folder into your project's `Packages/` directory, **or** add to `manifest.json`:

```json
{
  "dependencies": {
    "com.unity.2d.sprite-atlas-analyzer": "file:../com.unity.2d.sprite-atlas-analyzer"
  }
}
```

2. Open **Window > Analysis > Sprite Atlas Analyzer**.

## Built-in Reports

| Report | Purpose |
|--------|---------|
| All Sprite Atlases | Tree view: Atlas → texture page → sprite metrics |
| Atlas Texture Pages | Atlases exceeding page count threshold |
| Texture Space Wastage | Atlases with high unused memory |
| Sprite Count | Atlases with few sprites (merge candidates) |
| Source Textures with Compression | Double-compression risk |
| Atlas Secondary Texture | Inconsistent secondary texture counts |

## Usage

1. Click **Analyze** to scan Sprite Atlases under configured paths (default: `Assets`).
2. Click **...** to configure data source folders.
3. Select a report on the left; adjust thresholds in the right settings panel where available.
4. Click a row to ping the asset in the Inspector.
5. Right-click a row → **Reanalyze Atlas** for incremental refresh.

## API Compatibility Notes

- Uses `SpriteAtlasUtility.PackAtlases` + reflection `GetPreviewTextures` when atlases are not yet packed.
- Uses `TextureUtil.GetStorageMemorySizeLong` via reflection for accurate texture memory.
- Does **not** depend on `com.unity.2d.common` or Unity 6 `EntityId` APIs.
- Internal Unity APIs may change in future Editor versions; see `SpriteAtlasBridgeCompat.cs`.

## Extending

Implement `IAnalyzerReport` (or inherit `AnalyzerIssueReportBase`) with a parameterless constructor. The window discovers reports via `TypeCache`.

```csharp
class MyReport : AnalyzerIssueReportBase
{
    public MyReport() : base(new[] { typeof(SpriteAtlasDataSource) }) { }
    protected override void OnReportDataSourceChanged(IReportDataSource ds) { /* ... */ }
    public override VisualElement reportContent => m_View;
    public override VisualElement settingsContent => null;
    public override string reportTitle => "My Report";
}
```

## Package Structure

```
com.unity.2d.sprite-atlas-analyzer/
├── package.json
├── README.md
└── Editor/
    ├── Unity.2D.SpriteAtlasAnalyzer.Editor.asmdef
    └── Insider/
        ├── AnalyzerWindow/       # Main UI
        ├── Interface/            # IAnalyzerReport, IReportDataSource
        ├── SpriteAtlas/
        │   ├── DataSource/       # Collection + SpriteAtlasBridgeCompat
        │   └── SpriteAtlasIssueReport/
        └── Utilities/
```

## Requirements

- Unity **2022.3.62** or newer 2022.3 LTS
- UI Toolkit (built-in)
