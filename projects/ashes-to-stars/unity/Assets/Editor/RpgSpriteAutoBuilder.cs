// RpgSpriteAutoBuilder.cs
// Place in: Assets/Editor/RpgSpriteAutoBuilder.cs
// Target: Unity 2022.3 LTS+ / Unity 6
//
// Features:
// - Automatic grid detection (no Columns / Rows required)
// - Automatic top-left -> bottom-right frame order
// - Per-frame character alignment using dominant alpha component
// - Pivot = robust character center X + feet Y
// - Automatic animation type guess from filename
// - Custom frame order and per-frame duration
// - Loop / PingPong
// - Simple preview
// - AnimationClip creation
// - AnimatorController creation/update

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public class RpgSpriteAutoBuilder : EditorWindow
{
    [Serializable]
    private class GridDetection
    {
        public int columns;
        public int rows;
        public float score;
        public List<RectInt> cells = new List<RectInt>();
    }

    private class CellStats
    {
        public bool hasContent;
        public int opaqueCount;
        public int minX;
        public int maxX;
        public int minY;
        public int maxY;
        public bool touchesLeft;
        public bool touchesRight;
        public bool touchesBottom;
        public bool touchesTop;
        public int horizontalBands;
        public int verticalBands;
    }

    private Texture2D spriteSheet;

    // Detection
    private int alphaThreshold = 12;
    private int maxColumns = 12;
    private int maxRows = 8;
    private int minimumCellSize = 24;
    private GridDetection detection;

    // Animation
    private string animationName = "Idle";
    private string frameOrderText = "";
    private string frameDurationsText = "";
    private bool loop = true;
    private bool pingPong = false;

    // Import / output
    private float pixelsPerUnit = 100f;
    private bool pointFilter = true;
    private bool noCompression = true;
    private bool createAnimatorController = true;
    private string outputFolder = "Assets/GeneratedAnimations";

    // Preview
    private bool previewPlaying = false;
    private double previewStartTime;
    private int previewFramePosition = 0;

    [MenuItem("Tools/RPG Sprite Auto Builder")]
    public static void Open()
    {
        GetWindow<RpgSpriteAutoBuilder>("RPG Sprite Auto Builder");
    }

    private void OnEnable()
    {
        EditorApplication.update += PreviewUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= PreviewUpdate;
    }

    private void OnSelectionChange()
    {
        // BATCH BUILD 버튼의 선택 개수 표시 갱신용.
        Repaint();
    }

    private void PreviewUpdate()
    {
        if (previewPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        GUILayout.Space(6);
        EditorGUILayout.LabelField("RPG Sprite Auto Builder", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "이미지 규칙 자동 판별 + 캐릭터 중심/발 정렬 + 애니메이션 자동 생성",
            EditorStyles.wordWrappedMiniLabel
        );

        GUILayout.Space(8);

        Texture2D newSheet = (Texture2D)EditorGUILayout.ObjectField(
            "Sprite Sheet",
            spriteSheet,
            typeof(Texture2D),
            false
        );

        if (newSheet != spriteSheet)
        {
            spriteSheet = newSheet;
            detection = null;
            previewPlaying = false;
            previewFramePosition = 0;

            if (spriteSheet != null)
                GuessAnimationSettingsFromFilename();
        }

        GUILayout.Space(8);
        DrawDetectionSection();

        GUILayout.Space(8);
        DrawAnimationSection();

        GUILayout.Space(8);
        DrawPreviewSection();

        GUILayout.Space(8);
        DrawOutputSection();

        GUILayout.Space(12);

        using (new EditorGUI.DisabledScope(spriteSheet == null))
        {
            if (GUILayout.Button("AUTO BUILD", GUILayout.Height(42)))
            {
                AutoBuild();
            }
        }

        GUILayout.Space(4);

        Texture2D[] selectedSheets =
            Selection.objects.OfType<Texture2D>().ToArray();

        using (new EditorGUI.DisabledScope(selectedSheets.Length == 0))
        {
            string batchLabel = selectedSheets.Length > 0
                ? "BATCH BUILD (선택된 텍스처 " + selectedSheets.Length + "개)"
                : "BATCH BUILD (프로젝트 창에서 PNG 다중 선택)";

            if (GUILayout.Button(batchLabel, GUILayout.Height(28)))
            {
                BatchBuild(selectedSheets);
            }
        }

        GUILayout.Space(6);

        EditorGUILayout.HelpBox(
            "기본 사용법: PNG 선택 → AUTO BUILD.\n" +
            "격자는 자동 판별합니다. 결과가 이상하면 Advanced의 최대 행/열이나 Alpha Threshold만 조절하세요.",
            MessageType.Info
        );
    }

    private void DrawDetectionSection()
    {
        EditorGUILayout.LabelField("Auto Detection", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(spriteSheet == null))
        {
            if (GUILayout.Button("Analyze Sprite Sheet"))
            {
                AnalyzeOnly();
            }
        }

        if (detection != null)
        {
            EditorGUILayout.LabelField("Detected Grid",
                detection.columns + " x " + detection.rows);
            EditorGUILayout.LabelField("Frames",
                (detection.columns * detection.rows).ToString());
            EditorGUILayout.LabelField("Detection Score",
                detection.score.ToString("F1", CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Frame Order",
                "Top → Bottom / Left → Right");
            EditorGUILayout.LabelField("Alignment",
                "Character center X + Feet Y");
        }

        GUILayout.Space(3);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Advanced Detection", EditorStyles.miniBoldLabel);
        alphaThreshold = EditorGUILayout.IntSlider("Alpha Threshold", alphaThreshold, 1, 128);
        maxColumns = EditorGUILayout.IntSlider("Max Columns", maxColumns, 2, 20);
        maxRows = EditorGUILayout.IntSlider("Max Rows", maxRows, 1, 12);
        minimumCellSize = EditorGUILayout.IntSlider("Min Cell Size", minimumCellSize, 8, 128);
        EditorGUILayout.EndVertical();
    }

    private void DrawAnimationSection()
    {
        EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);

        animationName = EditorGUILayout.TextField("Animation Name", animationName);
        frameOrderText = EditorGUILayout.TextField("Frame Order", frameOrderText);
        frameDurationsText = EditorGUILayout.TextField("Frame Times (sec)", frameDurationsText);

        loop = EditorGUILayout.Toggle("Loop", loop);
        pingPong = EditorGUILayout.Toggle("Ping Pong", pingPong);

        EditorGUILayout.HelpBox(
            "예) Order: 0,1,2,3,4,5,6,7\n" +
            "예) Time: 0.08,0.08,0.06,0.04,0.12,0.06,0.08,0.15\n" +
            "시간 하나만 입력하면 모든 프레임에 동일 적용됩니다.",
            MessageType.None
        );
    }

    private void DrawPreviewSection()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (spriteSheet == null || detection == null || detection.cells.Count == 0)
        {
            EditorGUILayout.HelpBox("Analyze 후 프레임 미리보기가 표시됩니다.", MessageType.None);
            return;
        }

        List<int> order;
        List<float> times;

        try
        {
            order = ParseOrder(frameOrderText, detection.cells.Count);
            times = ParseDurations(frameDurationsText, order.Count);
        }
        catch
        {
            order = Enumerable.Range(0, detection.cells.Count).ToList();
            times = Enumerable.Repeat(0.1f, order.Count).ToList();
        }

        List<int> playbackOrder = new List<int>(order);
        List<float> playbackTimes = new List<float>(times);

        if (pingPong && order.Count > 2)
        {
            for (int i = order.Count - 2; i >= 1; i--)
            {
                playbackOrder.Add(order[i]);
                playbackTimes.Add(times[i]);
            }
        }

        if (playbackOrder.Count == 0)
            return;

        int framePos = Mathf.Clamp(previewFramePosition, 0, playbackOrder.Count - 1);

        if (previewPlaying)
        {
            float total = playbackTimes.Sum();
            if (total > 0.0001f)
            {
                double elapsed = EditorApplication.timeSinceStartup - previewStartTime;

                if (loop)
                    elapsed %= total;
                else
                    elapsed = Math.Min(elapsed, total - 0.0001);

                float cursor = 0f;
                framePos = playbackOrder.Count - 1;

                for (int i = 0; i < playbackOrder.Count; i++)
                {
                    cursor += playbackTimes[i];
                    if (elapsed < cursor)
                    {
                        framePos = i;
                        break;
                    }
                }

                previewFramePosition = framePos;

                if (!loop && elapsed >= total - 0.001)
                    previewPlaying = false;
            }
        }

        int spriteIndex = playbackOrder[Mathf.Clamp(framePos, 0, playbackOrder.Count - 1)];
        spriteIndex = Mathf.Clamp(spriteIndex, 0, detection.cells.Count - 1);

        Rect previewArea = GUILayoutUtility.GetRect(220, 220, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(previewArea, new Color(0.11f, 0.11f, 0.11f, 1f));

        RectInt cell = detection.cells[spriteIndex];

        Rect uv = new Rect(
            cell.x / (float)spriteSheet.width,
            cell.y / (float)spriteSheet.height,
            cell.width / (float)spriteSheet.width,
            cell.height / (float)spriteSheet.height
        );

        Rect fitted = FitRect(previewArea, cell.width / (float)Mathf.Max(1, cell.height));
        GUI.DrawTextureWithTexCoords(fitted, spriteSheet, uv, true);

        EditorGUILayout.LabelField(
            "Preview Frame",
            spriteIndex + "  (" + (framePos + 1) + "/" + playbackOrder.Count + ")"
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(previewPlaying ? "Stop" : "Play"))
        {
            previewPlaying = !previewPlaying;
            previewStartTime = EditorApplication.timeSinceStartup;
            previewFramePosition = 0;
        }

        if (GUILayout.Button("<"))
        {
            previewPlaying = false;
            previewFramePosition =
                (previewFramePosition - 1 + playbackOrder.Count) % playbackOrder.Count;
        }

        if (GUILayout.Button(">"))
        {
            previewPlaying = false;
            previewFramePosition =
                (previewFramePosition + 1) % playbackOrder.Count;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawOutputSection()
    {
        EditorGUILayout.LabelField("Output / Import", EditorStyles.boldLabel);

        pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", pixelsPerUnit);
        pointFilter = EditorGUILayout.Toggle("Point Filter", pointFilter);
        noCompression = EditorGUILayout.Toggle("No Compression", noCompression);
        createAnimatorController =
            EditorGUILayout.Toggle("Create Animator", createAnimatorController);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
    }

    private Rect FitRect(Rect container, float aspect)
    {
        if (aspect <= 0f)
            return container;

        float containerAspect = container.width / Mathf.Max(1f, container.height);

        if (containerAspect > aspect)
        {
            float width = container.height * aspect;
            return new Rect(
                container.x + (container.width - width) * 0.5f,
                container.y,
                width,
                container.height
            );
        }
        else
        {
            float height = container.width / aspect;
            return new Rect(
                container.x,
                container.y + (container.height - height) * 0.5f,
                container.width,
                height
            );
        }
    }

    private void GuessAnimationSettingsFromFilename()
    {
        if (spriteSheet == null)
            return;

        string path = AssetDatabase.GetAssetPath(spriteSheet);
        string name = Path.GetFileNameWithoutExtension(path);
        string lower = name.ToLowerInvariant();

        if (ContainsAny(lower, "idle", "stand", "wait"))
        {
            animationName = "Idle";
            loop = true;
            pingPong = false;
            frameDurationsText = "0.12";
        }
        else if (ContainsAny(lower, "run", "walk", "move"))
        {
            animationName = lower.Contains("walk") ? "Walk" : "Run";
            loop = true;
            pingPong = false;
            frameDurationsText = lower.Contains("walk") ? "0.10" : "0.07";
        }
        else if (ContainsAny(lower, "attack", "atk", "slash", "hit"))
        {
            animationName = "Attack";
            loop = false;
            pingPong = false;
            frameDurationsText = "0.08";
        }
        else if (ContainsAny(lower, "skill", "cast", "spell", "ability"))
        {
            animationName = "Skill";
            loop = false;
            pingPong = false;
            frameDurationsText = "0.09";
        }
        else if (ContainsAny(lower, "die", "death", "dead"))
        {
            animationName = "Die";
            loop = false;
            pingPong = false;
            frameDurationsText = "0.12";
        }
        else
        {
            string[] tokens = name.Split(new[] { '_', '-', ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            animationName = tokens.Length > 0 ? tokens[tokens.Length - 1] : "Animation";
            loop = false;
            pingPong = false;
            frameDurationsText = "0.10";
        }

        frameOrderText = "";
    }

    private bool ContainsAny(string text, params string[] values)
    {
        foreach (string value in values)
        {
            if (text.Contains(value))
                return true;
        }
        return false;
    }

    private void AnalyzeOnly()
    {
        if (spriteSheet == null)
            return;

        try
        {
            string path = AssetDatabase.GetAssetPath(spriteSheet);
            PrepareTextureForAnalysis(path);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                throw new Exception("Texture를 다시 불러오지 못했습니다.");

            Color32[] pixels = tex.GetPixels32();
            detection = DetectBestGrid(tex, pixels);

            if (detection == null)
                throw new Exception("스프라이트 격자를 판별하지 못했습니다.");

            int frameCount = detection.cells.Count;

            if (string.IsNullOrWhiteSpace(frameOrderText) ||
                !IsOrderValidForCount(frameOrderText, frameCount))
            {
                frameOrderText = string.Join(",",
                    Enumerable.Range(0, frameCount).Select(x => x.ToString()));
            }

            if (string.IsNullOrWhiteSpace(frameDurationsText))
                frameDurationsText = GuessDefaultFrameTime().ToString("0.###",
                    CultureInfo.InvariantCulture);

            Repaint();

            Debug.Log(
                "[RPG Sprite Auto Builder] Detected " +
                detection.columns + "x" + detection.rows +
                " / " + frameCount + " frames / score " +
                detection.score.ToString("F1", CultureInfo.InvariantCulture)
            );
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Analyze Error", ex.Message, "OK");
        }
    }

    private void AutoBuild()
    {
        if (spriteSheet == null)
        {
            EditorUtility.DisplayDialog("Error", "Sprite Sheet를 선택하세요.", "OK");
            return;
        }

        try
        {
            string message = BuildCore();
            EditorUtility.DisplayDialog("완료", message, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Build Error", ex.Message, "OK");
        }
    }

    // 현재 spriteSheet 하나를 슬라이스하고 클립/애니메이터까지 생성한다.
    // 성공 시 요약 문자열 반환, 실패 시 throw — AutoBuild/BatchBuild가 감싼다.
    private string BuildCore()
    {
        {
            string path = AssetDatabase.GetAssetPath(spriteSheet);
            if (string.IsNullOrEmpty(path))
                throw new Exception("Sprite Sheet 경로를 찾지 못했습니다.");

            PrepareTextureForAnalysis(path);

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                throw new Exception("Texture를 불러오지 못했습니다.");

            Color32[] pixels = tex.GetPixels32();

            detection = DetectBestGrid(tex, pixels);
            if (detection == null || detection.cells.Count < 2)
                throw new Exception("2개 이상의 프레임을 자동 판별하지 못했습니다.");

            int count = detection.cells.Count;

            if (string.IsNullOrWhiteSpace(frameOrderText) ||
                !IsOrderValidForCount(frameOrderText, count))
            {
                frameOrderText = string.Join(",",
                    Enumerable.Range(0, count).Select(x => x.ToString()));
            }

            if (string.IsNullOrWhiteSpace(frameDurationsText))
                frameDurationsText =
                    GuessDefaultFrameTime().ToString("0.###", CultureInfo.InvariantCulture);

            SliceWithAutoPivots(path, tex, pixels, detection);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => ExtractTrailingNumber(s.name))
                .ToArray();

            if (sprites.Length != count)
            {
                throw new Exception(
                    "슬라이스 결과 프레임 수가 예상과 다릅니다. " +
                    "Detected=" + count + ", Imported=" + sprites.Length
                );
            }

            List<int> order = ParseOrder(frameOrderText, sprites.Length);
            List<float> durations = ParseDurations(frameDurationsText, order.Count);

            EnsureFolder(outputFolder);

            AnimationClip clip =
                CreateAnimationClip(path, sprites, order, durations);

            if (createAnimatorController)
                CreateOrUpdateAnimator(path, clip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = clip;

            return detection.columns + " x " + detection.rows +
                " / " + count + "프레임\n" +
                animationName + " 애니메이션 생성 완료";
        }
    }

    private void BatchBuild(Texture2D[] sheets)
    {
        int ok = 0;
        List<string> failed = new List<string>();

        try
        {
            for (int i = 0; i < sheets.Length; i++)
            {
                spriteSheet = sheets[i];
                detection = null;
                previewPlaying = false;

                EditorUtility.DisplayProgressBar(
                    "Batch Build",
                    sheets[i].name + " (" + (i + 1) + "/" + sheets.Length + ")",
                    i / (float)sheets.Length
                );

                try
                {
                    // 파일명 기반 자동 추정(애니 이름/루프/프레임 시간),
                    // 프레임 순서는 시트마다 새로 계산한다.
                    GuessAnimationSettingsFromFilename();
                    BuildCore();
                    ok++;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    failed.Add(sheets[i].name);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string summary = ok + "/" + sheets.Length + " 시트 성공";
        if (failed.Count > 0)
            summary += "\n실패: " + string.Join(", ", failed);

        EditorUtility.DisplayDialog("Batch Build", summary, "OK");
        Repaint();
    }

    private void PrepareTextureForAnalysis(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new Exception("TextureImporter를 가져오지 못했습니다.");

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            changed = true;
        }

        if (!importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        // 원본이 maxTextureSize보다 크면 다운스케일된 픽셀로 분석하게 되어
        // 슬라이스 좌표가 원본과 어긋난다. 분석/슬라이스는 항상 원본 해상도로.
        if (importer.maxTextureSize < 8192)
        {
            importer.maxTextureSize = 8192;
            changed = true;
        }

        if (pixelsPerUnit > 0f &&
            Math.Abs(importer.spritePixelsPerUnit - pixelsPerUnit) > 0.001f)
        {
            importer.spritePixelsPerUnit = pixelsPerUnit;
            changed = true;
        }

        if (pointFilter && importer.filterMode != FilterMode.Point)
        {
            importer.filterMode = FilterMode.Point;
            changed = true;
        }

        if (noCompression &&
            importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private GridDetection DetectBestGrid(Texture2D tex, Color32[] pixels)
    {
        GridDetection best = null;

        int maxC = Mathf.Min(maxColumns, tex.width / Mathf.Max(1, minimumCellSize));
        int maxR = Mathf.Min(maxRows, tex.height / Mathf.Max(1, minimumCellSize));

        maxC = Mathf.Max(1, maxC);
        maxR = Mathf.Max(1, maxR);

        for (int rows = 1; rows <= maxR; rows++)
        {
            for (int cols = 1; cols <= maxC; cols++)
            {
                int frameCount = rows * cols;

                if (frameCount < 2 || frameCount > 64)
                    continue;

                float avgCellW = tex.width / (float)cols;
                float avgCellH = tex.height / (float)rows;

                if (avgCellW < minimumCellSize || avgCellH < minimumCellSize)
                    continue;

                List<RectInt> cells = BuildCells(tex.width, tex.height, cols, rows);

                float score = ScoreGrid(tex, pixels, cells, cols, rows);

                // Small preference for common animation sheet sizes.
                if (frameCount == 8) score += 7f;
                else if (frameCount == 6) score += 5f;
                else if (frameCount == 4) score += 4f;
                else if (frameCount == 10 || frameCount == 12) score += 3f;
                else if (frameCount == 16) score += 2f;

                if (best == null || score > best.score)
                {
                    best = new GridDetection
                    {
                        columns = cols,
                        rows = rows,
                        score = score,
                        cells = cells
                    };
                }
            }
        }

        return best;
    }

    private List<RectInt> BuildCells(int texWidth, int texHeight, int cols, int rows)
    {
        List<RectInt> result = new List<RectInt>(cols * rows);

        // Naming / animation order: top row first, left -> right.
        for (int rowTop = 0; rowTop < rows; rowTop++)
        {
            int topY = Mathf.RoundToInt(texHeight - rowTop * texHeight / (float)rows);
            int bottomY = Mathf.RoundToInt(
                texHeight - (rowTop + 1) * texHeight / (float)rows
            );

            for (int col = 0; col < cols; col++)
            {
                int leftX = Mathf.RoundToInt(col * texWidth / (float)cols);
                int rightX = Mathf.RoundToInt((col + 1) * texWidth / (float)cols);

                result.Add(new RectInt(
                    leftX,
                    bottomY,
                    Mathf.Max(1, rightX - leftX),
                    Mathf.Max(1, topY - bottomY)
                ));
            }
        }

        return result;
    }

    private float ScoreGrid(
        Texture2D tex,
        Color32[] pixels,
        List<RectInt> cells,
        int cols,
        int rows)
    {
        int emptyCells = 0;
        int edgeTouchCells = 0;
        int multiBandCells = 0;

        List<float> widthRatios = new List<float>();
        List<float> heightRatios = new List<float>();
        List<float> fillRatios = new List<float>();

        foreach (RectInt cell in cells)
        {
            CellStats stats = GetCellStats(tex, pixels, cell);

            if (!stats.hasContent)
            {
                emptyCells++;
                continue;
            }

            int contentW = stats.maxX - stats.minX + 1;
            int contentH = stats.maxY - stats.minY + 1;

            widthRatios.Add(contentW / (float)Mathf.Max(1, cell.width));
            heightRatios.Add(contentH / (float)Mathf.Max(1, cell.height));
            fillRatios.Add(stats.opaqueCount /
                           (float)Mathf.Max(1, cell.width * cell.height));

            if (stats.touchesLeft || stats.touchesRight ||
                stats.touchesBottom || stats.touchesTop)
            {
                edgeTouchCells++;
            }

            // If one candidate cell actually contains two separate animation
            // frames, it often has two large alpha bands with a wide empty
            // gap between them. This helps distinguish 4x2 from 4x1 / 2x2.
            if (stats.horizontalBands > 1 || stats.verticalBands > 1)
                multiBandCells++;
        }

        float emptyRatio = emptyCells / (float)cells.Count;
        float edgeRatio = edgeTouchCells / (float)Mathf.Max(1, cells.Count - emptyCells);
        float multiBandRatio = multiBandCells /
            (float)Mathf.Max(1, cells.Count - emptyCells);

        float score = 100f;

        // Wrong over-segmentation usually creates empty cells.
        score -= emptyRatio * 220f;

        // Correctly sliced sprites normally have some transparent padding.
        score -= edgeRatio * 45f;

        // Strong signal for under-segmentation, but not absolute because
        // an attack effect can legitimately be detached from the body.
        score -= multiBandRatio * 22f;

        if (widthRatios.Count > 0)
        {
            float meanW = widthRatios.Average();
            float meanH = heightRatios.Average();

            // Under-segmented cells tend to contain multiple characters
            // and fill nearly the whole cell.
            if (meanW > 0.93f) score -= (meanW - 0.93f) * 180f;
            if (meanH > 0.93f) score -= (meanH - 0.93f) * 180f;

            // Prefer a reasonable amount of transparent breathing room.
            if (meanW < 0.12f) score -= (0.12f - meanW) * 80f;
            if (meanH < 0.12f) score -= (0.12f - meanH) * 80f;

            float variationW = StandardDeviation(widthRatios);
            float variationH = StandardDeviation(heightRatios);

            // Animation frames are not identical, but wildly inconsistent
            // bounds often mean a wrong candidate grid.
            score -= Mathf.Max(0f, variationW - 0.22f) * 35f;
            score -= Mathf.Max(0f, variationH - 0.22f) * 35f;
        }

        // Internal separator lines should be mostly transparent.
        float separatorOpacity = MeasureSeparatorOpacity(tex, pixels, cols, rows);
        score -= separatorOpacity * 120f;

        // Penalize extremely dense cells, often a sign that several frames
        // were merged into one candidate cell.
        if (fillRatios.Count > 0)
        {
            float meanFill = fillRatios.Average();
            if (meanFill > 0.70f)
                score -= (meanFill - 0.70f) * 100f;
        }

        return score;
    }

    private float MeasureSeparatorOpacity(
        Texture2D tex,
        Color32[] pixels,
        int cols,
        int rows)
    {
        long samples = 0;
        long opaque = 0;
        int band = 1;

        for (int c = 1; c < cols; c++)
        {
            int x = Mathf.RoundToInt(c * tex.width / (float)cols);

            for (int dx = -band; dx <= band; dx++)
            {
                int sx = Mathf.Clamp(x + dx, 0, tex.width - 1);

                for (int y = 0; y < tex.height; y++)
                {
                    samples++;
                    if (pixels[y * tex.width + sx].a > alphaThreshold)
                        opaque++;
                }
            }
        }

        for (int r = 1; r < rows; r++)
        {
            int y = Mathf.RoundToInt(r * tex.height / (float)rows);

            for (int dy = -band; dy <= band; dy++)
            {
                int sy = Mathf.Clamp(y + dy, 0, tex.height - 1);

                int rowStart = sy * tex.width;

                for (int x = 0; x < tex.width; x++)
                {
                    samples++;
                    if (pixels[rowStart + x].a > alphaThreshold)
                        opaque++;
                }
            }
        }

        if (samples == 0)
            return 0f;

        return opaque / (float)samples;
    }

    private float StandardDeviation(List<float> values)
    {
        if (values == null || values.Count <= 1)
            return 0f;

        float mean = values.Average();
        float sum = 0f;

        foreach (float v in values)
        {
            float d = v - mean;
            sum += d * d;
        }

        return Mathf.Sqrt(sum / values.Count);
    }

    private CellStats GetCellStats(Texture2D tex, Color32[] pixels, RectInt cell)
    {
        CellStats s = new CellStats
        {
            hasContent = false,
            opaqueCount = 0,
            minX = cell.width,
            maxX = -1,
            minY = cell.height,
            maxY = -1
        };

        int edgeBandX = Mathf.Max(1, Mathf.RoundToInt(cell.width * 0.015f));
        int edgeBandY = Mathf.Max(1, Mathf.RoundToInt(cell.height * 0.015f));

        bool[] activeColumns = new bool[cell.width];
        bool[] activeRows = new bool[cell.height];

        for (int ly = 0; ly < cell.height; ly++)
        {
            int ty = cell.y + ly;
            if (ty < 0 || ty >= tex.height)
                continue;

            int rowStart = ty * tex.width;

            for (int lx = 0; lx < cell.width; lx++)
            {
                int tx = cell.x + lx;
                if (tx < 0 || tx >= tex.width)
                    continue;

                if (pixels[rowStart + tx].a <= alphaThreshold)
                    continue;

                s.hasContent = true;
                s.opaqueCount++;
                activeColumns[lx] = true;
                activeRows[ly] = true;

                if (lx < s.minX) s.minX = lx;
                if (lx > s.maxX) s.maxX = lx;
                if (ly < s.minY) s.minY = ly;
                if (ly > s.maxY) s.maxY = ly;

                if (lx < edgeBandX) s.touchesLeft = true;
                if (lx >= cell.width - edgeBandX) s.touchesRight = true;
                if (ly < edgeBandY) s.touchesBottom = true;
                if (ly >= cell.height - edgeBandY) s.touchesTop = true;
            }
        }

        int bridgeX = Mathf.Max(2, Mathf.RoundToInt(cell.width * 0.025f));
        int bridgeY = Mathf.Max(2, Mathf.RoundToInt(cell.height * 0.025f));

        s.verticalBands = CountAlphaBands(activeColumns, bridgeX);
        s.horizontalBands = CountAlphaBands(activeRows, bridgeY);

        return s;
    }

    private int CountAlphaBands(bool[] active, int bridgeGap)
    {
        if (active == null || active.Length == 0)
            return 0;

        List<Vector2Int> bands = new List<Vector2Int>();

        int i = 0;
        while (i < active.Length)
        {
            while (i < active.Length && !active[i])
                i++;

            if (i >= active.Length)
                break;

            int start = i;

            while (i < active.Length && active[i])
                i++;

            bands.Add(new Vector2Int(start, i - 1));
        }

        if (bands.Count <= 1)
            return bands.Count;

        int merged = 1;
        Vector2Int current = bands[0];

        for (int b = 1; b < bands.Count; b++)
        {
            Vector2Int next = bands[b];
            int gap = next.x - current.y - 1;

            if (gap <= bridgeGap)
            {
                current.y = next.y;
            }
            else
            {
                merged++;
                current = next;
            }
        }

        return merged;
    }

    private void SliceWithAutoPivots(
        string path,
        Texture2D tex,
        Color32[] pixels,
        GridDetection grid)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new Exception("TextureImporter를 가져오지 못했습니다.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);

        if (pointFilter)
            importer.filterMode = FilterMode.Point;

        if (noCompression)
            importer.textureCompression = TextureImporterCompression.Uncompressed;

        string baseName = Path.GetFileNameWithoutExtension(path);

        List<SpriteRect> rects = new List<SpriteRect>();

        for (int i = 0; i < grid.cells.Count; i++)
        {
            RectInt cell = grid.cells[i];

            Vector2 pivot = CalculateCharacterPivot(tex, pixels, cell);

            SpriteRect spriteRect = new SpriteRect
            {
                name = baseName + "_" + i.ToString("00"),
                rect = new Rect(cell.x, cell.y, cell.width, cell.height),
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                spriteID = GUID.Generate()
            };

            rects.Add(spriteRect);
        }

        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();

        ISpriteEditorDataProvider provider =
            factory.GetSpriteEditorDataProviderFromObject(importer);

        if (provider == null)
            throw new Exception(
                "Sprite Data Provider를 가져오지 못했습니다. " +
                "Unity 2D Sprite 패키지가 설치되어 있는지 확인하세요."
            );

        provider.InitSpriteEditorDataProvider();
        provider.SetSpriteRects(rects.ToArray());

        ISpriteNameFileIdDataProvider nameProvider =
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>();

        if (nameProvider != null)
        {
            List<SpriteNameFileIdPair> pairs = rects
                .Select(r => new SpriteNameFileIdPair(r.name, r.spriteID))
                .ToList();

            nameProvider.SetNameFileIdPairs(pairs);
        }

        provider.Apply();
        importer.SaveAndReimport();
    }

    private Vector2 CalculateCharacterPivot(
        Texture2D tex,
        Color32[] pixels,
        RectInt cell)
    {
        // We prefer the largest connected alpha component.
        // This ignores detached particles, dust, glow, shadows, etc.
        List<int> component = FindLargestOpaqueComponent(tex, pixels, cell);

        if (component.Count == 0)
            return new Vector2(0.5f, 0.05f);

        int minX = cell.width - 1;
        int maxX = 0;
        int minY = cell.height - 1;
        int maxY = 0;

        foreach (int localIndex in component)
        {
            int x = localIndex % cell.width;
            int y = localIndex / cell.width;

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        int charHeight = Mathf.Max(1, maxY - minY + 1);

        // Body band:
        // excludes the very bottom contact foot and the uppermost weapon/hat area.
        int bodyLow = minY + Mathf.RoundToInt(charHeight * 0.12f);
        int bodyHigh = minY + Mathf.RoundToInt(charHeight * 0.78f);

        int[] xHistogram = new int[cell.width];
        int histogramTotal = 0;

        foreach (int localIndex in component)
        {
            int x = localIndex % cell.width;
            int y = localIndex / cell.width;

            if (y >= bodyLow && y <= bodyHigh)
            {
                xHistogram[x]++;
                histogramTotal++;
            }
        }

        float centerX;

        if (histogramTotal > 0)
        {
            // Weighted median is much less affected by a long sword,
            // staff, cape or spell effect than a simple bounding-box center.
            int halfway = histogramTotal / 2;
            int cumulative = 0;
            int medianX = Mathf.RoundToInt((minX + maxX) * 0.5f);

            for (int x = 0; x < xHistogram.Length; x++)
            {
                cumulative += xHistogram[x];
                if (cumulative >= halfway)
                {
                    medianX = x;
                    break;
                }
            }

            centerX = medianX + 0.5f;
        }
        else
        {
            centerX = (minX + maxX + 1) * 0.5f;
        }

        // Largest component removes most detached ground effects.
        // Its minimum Y is therefore a good feet/ground anchor.
        float feetY = minY + 0.5f;

        float pivotX = centerX / Mathf.Max(1f, cell.width);
        float pivotY = feetY / Mathf.Max(1f, cell.height);

        return new Vector2(
            Mathf.Clamp01(pivotX),
            Mathf.Clamp01(pivotY)
        );
    }

    private List<int> FindLargestOpaqueComponent(
        Texture2D tex,
        Color32[] pixels,
        RectInt cell)
    {
        int w = cell.width;
        int h = cell.height;
        int size = w * h;

        byte[] state = new byte[size];
        // 0 = transparent/unvisited
        // 1 = opaque/unvisited
        // 2 = visited

        for (int y = 0; y < h; y++)
        {
            int ty = cell.y + y;
            if (ty < 0 || ty >= tex.height)
                continue;

            int rowStart = ty * tex.width;

            for (int x = 0; x < w; x++)
            {
                int tx = cell.x + x;
                if (tx < 0 || tx >= tex.width)
                    continue;

                if (pixels[rowStart + tx].a > alphaThreshold)
                    state[y * w + x] = 1;
            }
        }

        List<int> largest = new List<int>();
        Queue<int> queue = new Queue<int>();

        // 8-neighbour connectivity is friendlier to pixel-art diagonals.
        int[] nx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] ny = { -1, -1, -1, 0, 0, 1, 1, 1 };

        for (int start = 0; start < size; start++)
        {
            if (state[start] != 1)
                continue;

            List<int> current = new List<int>();
            queue.Clear();

            state[start] = 2;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                current.Add(idx);

                int x = idx % w;
                int y = idx / w;

                for (int k = 0; k < 8; k++)
                {
                    int xx = x + nx[k];
                    int yy = y + ny[k];

                    if (xx < 0 || xx >= w || yy < 0 || yy >= h)
                        continue;

                    int ni = yy * w + xx;

                    if (state[ni] != 1)
                        continue;

                    state[ni] = 2;
                    queue.Enqueue(ni);
                }
            }

            if (current.Count > largest.Count)
                largest = current;
        }

        return largest;
    }

    private AnimationClip CreateAnimationClip(
        string texturePath,
        Sprite[] sprites,
        List<int> baseOrder,
        List<float> baseDurations)
    {
        List<int> order = new List<int>(baseOrder);
        List<float> durations = new List<float>(baseDurations);

        if (pingPong && baseOrder.Count > 2)
        {
            for (int i = baseOrder.Count - 2; i >= 1; i--)
            {
                order.Add(baseOrder[i]);
                durations.Add(baseDurations[i]);
            }
        }

        string baseName = Path.GetFileNameWithoutExtension(texturePath);
        string safeAnimationName =
            string.IsNullOrWhiteSpace(animationName) ? "Animation" : animationName.Trim();

        string clipName = baseName + "_" + safeAnimationName;
        string clipPath =
            (outputFolder.TrimEnd('/') + "/" + clipName + ".anim").Replace("\\", "/");

        // 기존 클립은 지우지 않고 재사용한다. Delete 후 재생성하면
        // 씬/프리팹/애니메이터가 들고 있던 참조가 전부 끊긴다.
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNewClip = clip == null;

        if (isNewClip)
        {
            clip = new AnimationClip();
        }
        else
        {
            clip.ClearCurves();
            EditorCurveBinding[] oldBindings =
                AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (EditorCurveBinding oldBinding in oldBindings)
                AnimationUtility.SetObjectReferenceCurve(clip, oldBinding, null);
        }

        clip.name = clipName;
        clip.frameRate = 60f;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        List<ObjectReferenceKeyframe> keys = new List<ObjectReferenceKeyframe>();
        float time = 0f;

        for (int i = 0; i < order.Count; i++)
        {
            keys.Add(new ObjectReferenceKeyframe
            {
                time = time,
                value = sprites[order[i]]
            });

            time += durations[i];
        }

        // This duplicate key makes the final frame keep its requested duration.
        keys.Add(new ObjectReferenceKeyframe
        {
            time = time,
            value = sprites[order[order.Count - 1]]
        });

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

        if (isNewClip)
            AssetDatabase.CreateAsset(clip, clipPath);

        SetAnimationLoop(clip, loop);

        EditorUtility.SetDirty(clip);

        Debug.Log("[RPG Sprite Auto Builder] Created: " + clipPath);

        return clip;
    }

    private void SetAnimationLoop(AnimationClip clip, bool shouldLoop)
    {
        SerializedObject so = new SerializedObject(clip);
        SerializedProperty settings = so.FindProperty("m_AnimationClipSettings");

        if (settings != null)
        {
            SerializedProperty loopTime =
                settings.FindPropertyRelative("m_LoopTime");

            if (loopTime != null)
                loopTime.boolValue = shouldLoop;
        }

        so.ApplyModifiedProperties();
    }

    private void CreateOrUpdateAnimator(string texturePath, AnimationClip clip)
    {
        string baseName = GetCharacterBaseName(
            Path.GetFileNameWithoutExtension(texturePath)
        );

        string controllerPath =
            (outputFolder.TrimEnd('/') + "/" + baseName + ".controller")
            .Replace("\\", "/");

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
            controller =
                AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;

        string stateName =
            string.IsNullOrWhiteSpace(animationName) ? "Animation" : animationName.Trim();

        AnimatorState state = null;

        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state != null && child.state.name == stateName)
            {
                state = child.state;
                break;
            }
        }

        if (state == null)
            state = stateMachine.AddState(stateName);

        state.motion = clip;

        // First useful locomotion / idle state becomes default.
        if (stateMachine.defaultState == null ||
            stateName.Equals("Idle", StringComparison.OrdinalIgnoreCase))
        {
            stateMachine.defaultState = state;
        }

        EditorUtility.SetDirty(controller);

        Debug.Log("[RPG Sprite Auto Builder] Animator: " + controllerPath);
    }

    private string GetCharacterBaseName(string filename)
    {
        string[] suffixes =
        {
            "_idle", "-idle", " idle",
            "_run", "-run", " run",
            "_walk", "-walk", " walk",
            "_attack", "-attack", " attack",
            "_atk", "-atk", " atk",
            "_skill", "-skill", " skill",
            "_cast", "-cast", " cast",
            "_die", "-die", " die",
            "_death", "-death", " death"
        };

        // "tanker_idle_01" 같은 변형 시트 번호를 먼저 제거해야
        // 같은 캐릭터의 클립이 하나의 컨트롤러로 모인다.
        int end = filename.Length;
        while (end > 0 && char.IsDigit(filename[end - 1]))
            end--;

        if (end < filename.Length && end > 0 &&
            (filename[end - 1] == '_' ||
             filename[end - 1] == '-' ||
             filename[end - 1] == ' '))
        {
            filename = filename.Substring(0, end - 1);
        }

        string lower = filename.ToLowerInvariant();

        foreach (string suffix in suffixes)
        {
            if (lower.EndsWith(suffix))
            {
                return filename.Substring(0, filename.Length - suffix.Length)
                    .Trim('_', '-', ' ');
            }
        }

        return filename;
    }

    private float GuessDefaultFrameTime()
    {
        string lower = animationName.ToLowerInvariant();

        if (ContainsAny(lower, "idle", "stand", "wait"))
            return 0.12f;

        if (ContainsAny(lower, "run", "move"))
            return 0.07f;

        if (lower.Contains("walk"))
            return 0.10f;

        if (ContainsAny(lower, "attack", "atk", "slash", "hit"))
            return 0.08f;

        if (ContainsAny(lower, "skill", "cast", "spell"))
            return 0.09f;

        if (ContainsAny(lower, "die", "death", "dead"))
            return 0.12f;

        return 0.10f;
    }

    private List<int> ParseOrder(string input, int spriteCount)
    {
        if (spriteCount <= 0)
            throw new Exception("Sprite가 없습니다.");

        if (string.IsNullOrWhiteSpace(input))
            return Enumerable.Range(0, spriteCount).ToList();

        List<int> result = new List<int>();

        foreach (string token in input.Split(','))
        {
            string s = token.Trim();

            if (string.IsNullOrEmpty(s))
                continue;

            int value;
            if (!int.TryParse(s, out value))
                throw new Exception("Frame Order 숫자를 읽을 수 없습니다: " + s);

            if (value < 0 || value >= spriteCount)
                throw new Exception(
                    "Frame index가 범위를 벗어났습니다: " +
                    value + " / 0~" + (spriteCount - 1)
                );

            result.Add(value);
        }

        if (result.Count == 0)
            throw new Exception("Frame Order가 비어 있습니다.");

        return result;
    }

    private List<float> ParseDurations(string input, int count)
    {
        if (count <= 0)
            throw new Exception("재생할 프레임이 없습니다.");

        if (string.IsNullOrWhiteSpace(input))
            return Enumerable.Repeat(GuessDefaultFrameTime(), count).ToList();

        List<float> values = new List<float>();

        foreach (string token in input.Split(','))
        {
            string s = token.Trim();

            if (string.IsNullOrEmpty(s))
                continue;

            float value;

            if (!float.TryParse(
                s,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                // Korean Windows can use locale-dependent parsing in some projects.
                if (!float.TryParse(s, out value))
                    throw new Exception("Frame Time 숫자를 읽을 수 없습니다: " + s);
            }

            if (value <= 0f)
                throw new Exception("Frame Time은 0보다 커야 합니다.");

            values.Add(value);
        }

        if (values.Count == 1)
            return Enumerable.Repeat(values[0], count).ToList();

        if (values.Count != count)
        {
            throw new Exception(
                "Frame Time 개수(" + values.Count +
                ")와 Frame Order 개수(" + count + ")가 다릅니다."
            );
        }

        return values;
    }

    private bool IsOrderValidForCount(string input, int count)
    {
        try
        {
            List<int> order = ParseOrder(input, count);
            return order.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;

        int i = value.Length - 1;
        while (i >= 0 && char.IsDigit(value[i]))
            i--;

        string number = value.Substring(i + 1);

        int result;
        return int.TryParse(number, out result) ? result : 0;
    }

    private void EnsureFolder(string folderPath)
    {
        folderPath = (folderPath ?? "").Replace("\\", "/").TrimEnd('/');

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        if (string.IsNullOrEmpty(folderPath) ||
            !folderPath.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new Exception(
                "Output Folder는 Assets 아래여야 합니다. " +
                "예: Assets/GeneratedAnimations"
            );
        }

        string[] parts = folderPath.Split('/');
        string current = "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i]))
                continue;

            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
