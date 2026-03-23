#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AvatarFittingRoom
{
    /// <summary>
    /// アバター試着室 撮影ツール v3
    ///
    /// 使い方:
    ///   1. ヒエラルキーにアバターを置く
    ///   2. Tools/アバター試着室/撮影 を開く
    ///   3. アバターURL・衣装URL・ジャンルを入力
    ///   4.「撮影」ボタンで厳選10ポーズを一括撮影
    /// </summary>
    public class AvatarShooter : EditorWindow
    {
        // ===== 定数 =====
        static readonly string DEFAULT_OUTPUT = "renders_output";
        const int IMG_WIDTH = 1024;
        const int IMG_HEIGHT = 1440;
        const float CAM_ORTHO_SIZE = 0.85f;
        const float CAM_DISTANCE = 3f;

        // ===== ジャンル定義 =====
        static readonly string[] GENRES = {
            "制服", "カジュアル", "メイド", "ゴスロリ", "ワンピース",
            "水着", "和服", "ドレス", "スポーティ", "パンク",
            "セクシー", "コスプレ", "その他"
        };

        // ===== 内部クラス =====
        class DetectedAvatar
        {
            public GameObject go;
            public string name;
            public bool selected = true;
        }

        class PoseEntry
        {
            public AnimationClip clip;
            public string name;
        }

        // ===== フィールド =====
        List<DetectedAvatar> detected = new List<DetectedAvatar>();
        List<PoseEntry> poses = new List<PoseEntry>();
        string outputFolder = DEFAULT_OUTPUT;
        string avatarUrl = "";
        string outfitUrl = "";
        int selectedGenre = 0;
        string customGenre = "";
        string contributorName = ""; // データ提供者名（空 = 匿名）
        Vector2 scrollPos;
        Vector2 previewScrollPos;

        // タブ
        int currentTab = 0;
        static readonly string[] TAB_NAMES = { "撮影", "プレビュー", "アップロード", "設定" };

        // プレビュー用
        List<PreviewEntry> previews = new List<PreviewEntry>();
        class PreviewEntry
        {
            public string path;
            public string avatarName;
            public string label;
            public string poseName;
            public Texture2D thumbnail;
        }

        // アップロード用
        Vector2 uploadScrollPos;
        Dictionary<string, string> uploadOutfitUrls = new Dictionary<string, string>();
        string uploadStatus = "";

        [MenuItem("Tools/アバター試着室/撮影")]
        static void ShowWindow()
        {
            var w = GetWindow<AvatarShooter>("アバター試着室 撮影");
            w.minSize = new Vector2(420, 500);
            if (w.outputFolder == DEFAULT_OUTPUT)
                w.outputFolder = Path.Combine(Application.dataPath, "..", DEFAULT_OUTPUT).Replace("\\", "/");
            w.ScanHierarchy();
            w.ScanPoses();
        }

        void OnFocus()
        {
            ScanHierarchy();
            ScanPoses();
        }

        void OnHierarchyChange() => ScanHierarchy();

        // ========== ヒエラルキースキャン ==========
        void ScanHierarchy()
        {
            var roots = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene()
                .GetRootGameObjects();

            var prevSelected = new HashSet<string>(
                detected.Where(d => d.selected).Select(d => d.name)
            );

            detected.Clear();

            foreach (var root in roots)
            {
                if (root.GetComponent<Camera>() != null) continue;
                if (root.GetComponent<Light>() != null) continue;
                if (root.GetComponent<Canvas>() != null) continue;
                if (!root.activeInHierarchy) continue;

                var animator = root.GetComponent<Animator>();
                if (animator == null) continue;

                bool isHumanoid = animator.isHuman
                    && animator.GetBoneTransform(HumanBodyBones.Hips) != null;

                bool hasVrcDescriptor = root.GetComponents<Component>()
                    .Any(c => c != null && c.GetType().Name.Contains("VRCAvatarDescriptor"));

                if (isHumanoid || hasVrcDescriptor)
                {
                    detected.Add(new DetectedAvatar
                    {
                        go = root,
                        name = root.name,
                        selected = prevSelected.Count == 0 || prevSelected.Contains(root.name)
                    });
                }
            }

            Repaint();
        }

        // ========== ポーズ定義（厳選10ポーズ） ==========
        // ねじれが少ないポーズを厳選済み（Day3で選定）
        static readonly (string id, string label, string clipFileName)[] CURATED_POSES = {
            ("arms-crossed", "腕組み",       "010_0030"),
            ("peace",        "ピース",       "010_0040"),
            ("guts",         "ガッツ",       "010_0060"),
            ("shy",          "照れ",         "010_0070"),
            ("hip-hand",     "腰に手",       "010_0090"),
            ("cool-crossed", "クール腕組み", "020_0010"),
            ("relax",        "リラックス",   "020_0020"),
            ("point",        "指差し",       "020_0040"),
            ("seiza",        "正座",         "030_0010"),
            ("finger-up",    "指立て",       "030_0060"),
        };

        // ========== ポーズスキャン ==========
        void ScanPoses()
        {
            poses.Clear();

            // Assets内から厳選10ポーズのみ検索
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
            var clipsByFileName = new Dictionary<string, (AnimationClip clip, string path)>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null)
                    clipsByFileName[fileName] = (clip, path);
            }

            foreach (var (id, label, clipFileName) in CURATED_POSES)
            {
                if (!clipsByFileName.TryGetValue(clipFileName, out var found))
                    continue;

                poses.Add(new PoseEntry
                {
                    clip = found.clip,
                    name = label,
                });
            }
        }

        // ========== GUI ==========
        void OnGUI()
        {
            EditorGUILayout.Space(5);
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            EditorGUILayout.LabelField("アバター試着室 撮影ツール v2", headerStyle);
            EditorGUILayout.Space(2);

            // タブ切替
            currentTab = GUILayout.Toolbar(currentTab, TAB_NAMES);
            EditorGUILayout.Space(5);

            switch (currentTab)
            {
                case 0: DrawShootTab(); break;
                case 1: DrawPreviewTab(); break;
                case 2: DrawUploadTab(); break;
                case 3: DrawSettingsTab(); break;
            }
        }

        // ========== 撮影タブ ==========
        void DrawShootTab()
        {
            // ===== 検出アバター（コンパクト） =====
            if (detected.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "アバターが見つかりません。\nヒエラルキーにアバターを配置してください。",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("アバター", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(80));
            foreach (var d in detected)
            {
                EditorGUILayout.BeginHorizontal();
                d.selected = EditorGUILayout.Toggle(d.selected, GUILayout.Width(20));
                if (GUILayout.Button(d.name, EditorStyles.label))
                {
                    Selection.activeGameObject = d.go;
                    EditorGUIUtility.PingObject(d.go);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);

            // ===== アバターURL =====
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("アバターURL", GUILayout.Width(80));
            avatarUrl = EditorGUILayout.TextField(avatarUrl);
            EditorGUILayout.EndHorizontal();

            // ===== 衣装URL =====
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("衣装URL", GUILayout.Width(80));
            outfitUrl = EditorGUILayout.TextField(outfitUrl);
            EditorGUILayout.EndHorizontal();
            if (string.IsNullOrWhiteSpace(outfitUrl))
                EditorGUILayout.HelpBox("空欄なら素体として撮影します", MessageType.None);

            // ===== 提供者名 =====
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("提供者名", GUILayout.Width(80));
            contributorName = EditorGUILayout.TextField(contributorName);
            EditorGUILayout.EndHorizontal();
            if (string.IsNullOrWhiteSpace(contributorName))
                EditorGUILayout.HelpBox("空欄なら「匿名」として登録されます", MessageType.None);

            // ===== ジャンル =====
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ジャンル", GUILayout.Width(80));
            selectedGenre = EditorGUILayout.Popup(selectedGenre, GENRES);
            EditorGUILayout.EndHorizontal();
            if (selectedGenre == GENRES.Length - 1) // "その他"
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(84);
                customGenre = EditorGUILayout.TextField(customGenre);
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.FlexibleSpace();

            // ===== 撮影ボタン =====
            int selectedCount = detected.Count(d => d.selected);
            bool isBase = string.IsNullOrWhiteSpace(outfitUrl);
            string label = isBase ? "素体" : ExtractLabelFromUrl(outfitUrl);

            EditorGUILayout.Space(5);
            if (poses.Count == 0)
            {
                EditorGUILayout.HelpBox("ポーズが見つかりません。Assets内にポーズ素材を配置してください。", MessageType.Warning);
            }

            GUI.enabled = selectedCount > 0 && poses.Count > 0;
            GUI.backgroundColor = isBase
                ? new Color(0.3f, 0.75f, 0.95f)
                : new Color(0.4f, 0.9f, 0.4f);

            var btnText = $"撮影  ({selectedCount}体 × {poses.Count}ポーズ = {selectedCount * poses.Count}枚)";
            if (GUILayout.Button(btnText, GUILayout.Height(45)))
            {
                ShootAll(label);
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.Space(5);
        }

        /// <summary>衣装URLからラベル（ファイル名用）を抽出</summary>
        string ExtractLabelFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "素体";
            // booth.pm/items/12345 → "12345"
            var match = System.Text.RegularExpressions.Regex.Match(url, @"items/(\d+)");
            if (match.Success) return match.Groups[1].Value;
            // それ以外はURL末尾のパス
            var uri = url.TrimEnd('/');
            int lastSlash = uri.LastIndexOf('/');
            return lastSlash >= 0 ? uri.Substring(lastSlash + 1) : uri;
        }

        // ========== プレビュータブ ==========
        void DrawPreviewTab()
        {
            if (previews.Count == 0)
            {
                EditorGUILayout.HelpBox("まだ撮影されていません。\n撮影タブで撮影すると、ここにプレビューが表示されます。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"撮影済み: {previews.Count}枚", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            previewScrollPos = EditorGUILayout.BeginScrollView(previewScrollPos);

            // グリッド表示（3列）
            int cols = 3;
            float thumbSize = (position.width - 30) / cols;
            int col = 0;

            EditorGUILayout.BeginHorizontal();
            foreach (var p in previews)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(thumbSize));

                // サムネイル
                if (p.thumbnail != null)
                {
                    var rect = GUILayoutUtility.GetRect(thumbSize - 5, thumbSize * 1.4f);
                    GUI.DrawTexture(rect, p.thumbnail, ScaleMode.ScaleToFit);
                }

                // ラベル
                var labelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                EditorGUILayout.LabelField($"{p.avatarName}", labelStyle);
                EditorGUILayout.LabelField($"{p.label}" + (string.IsNullOrEmpty(p.poseName) ? "" : $" / {p.poseName}"), labelStyle);

                // 再撮影ボタン
                if (GUILayout.Button("再撮影", EditorStyles.miniButton))
                {
                    var avatar = detected.FirstOrDefault(d => d.name == p.avatarName);
                    if (avatar != null && avatar.go != null)
                    {
                        currentTab = 0;
                        Repaint();
                    }
                }

                EditorGUILayout.EndVertical();

                col++;
                if (col >= cols)
                {
                    col = 0;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);
            if (GUILayout.Button("プレビューをクリア"))
            {
                foreach (var p in previews)
                    if (p.thumbnail != null) Object.DestroyImmediate(p.thumbnail);
                previews.Clear();
            }
        }

        // ========== アップロードタブ ==========
        void DrawUploadTab()
        {
            // 撮影済みデータがあるか確認
            if (previews.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "まだ撮影されていません。\n" +
                    "撮影タブで撮影してから、ここでサイトへアップロードできます。",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "撮影した画像をサイトに登録します。\n" +
                "① URL を確認 → ②「送信」でカタログに登録",
                MessageType.Info);
            EditorGUILayout.Space(5);

            // --- アバター情報 ---
            // 撮影済みのアバター名を取得
            var avatarNames = previews.Select(p => p.avatarName).Distinct().ToList();
            EditorGUILayout.LabelField("アバター情報", EditorStyles.boldLabel);

            foreach (var avName in avatarNames)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(avName, EditorStyles.boldLabel, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();

                // 撮影タブで入力済みのアバターURLを表示（編集不可）
                if (!string.IsNullOrWhiteSpace(avatarUrl))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("アバターURL", GUILayout.Width(80));
                    EditorGUILayout.SelectableLabel(avatarUrl, GUILayout.Height(18));
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("撮影タブでアバターURLを入力してください", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(8);

            // --- 衣装一覧 ---
            EditorGUILayout.LabelField("撮影済み衣装", EditorStyles.boldLabel);

            // 衣装ラベル別にグループ化（素体を除く）
            var outfitLabels = previews
                .Select(p => p.label)
                .Distinct()
                .Where(l => l != "素体")
                .ToList();

            uploadScrollPos = EditorGUILayout.BeginScrollView(uploadScrollPos, GUILayout.MaxHeight(200));

            // 素体を先頭に表示
            var hasBase = previews.Any(p => p.label == "素体");
            if (hasBase)
            {
                EditorGUILayout.BeginHorizontal();
                var baseThumb = previews.FirstOrDefault(p => p.label == "素体");
                if (baseThumb?.thumbnail != null)
                {
                    var rect = GUILayoutUtility.GetRect(40, 56);
                    GUI.DrawTexture(rect, baseThumb.thumbnail, ScaleMode.ScaleToFit);
                }
                EditorGUILayout.LabelField("素体（アバター本体）", EditorStyles.boldLabel);
                var baseCount = previews.Count(p => p.label == "素体");
                EditorGUILayout.LabelField($"{baseCount}枚", EditorStyles.miniLabel, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(3);
            }

            foreach (var label in outfitLabels)
            {
                if (!uploadOutfitUrls.ContainsKey(label))
                {
                    // meta.jsonから既存URLを読み込み
                    var labelId = SanitizeFileName(label);
                    foreach (var avName in avatarNames)
                    {
                        var metaPath = Path.Combine(outputFolder, SanitizeFileName(avName), "meta.json");
                        if (File.Exists(metaPath))
                        {
                            var json = File.ReadAllText(metaPath);
                            var pattern = $"\"{System.Text.RegularExpressions.Regex.Escape(labelId)}\"\\s*:\\s*\\{{[^}}]*\"boothUrl\"\\s*:\\s*\"([^\"]*)\"";
                            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                            if (match.Success)
                            {
                                uploadOutfitUrls[label] = match.Groups[1].Value;
                                break;
                            }
                        }
                    }
                    if (!uploadOutfitUrls.ContainsKey(label))
                        uploadOutfitUrls[label] = "";
                }

                EditorGUILayout.BeginHorizontal();

                // サムネイル
                var thumb = previews.FirstOrDefault(p => p.label == label);
                if (thumb?.thumbnail != null)
                {
                    var rect = GUILayoutUtility.GetRect(40, 56);
                    GUI.DrawTexture(rect, thumb.thumbnail, ScaleMode.ScaleToFit);
                }

                EditorGUILayout.BeginVertical();
                var count = previews.Count(p => p.label == label);
                EditorGUILayout.LabelField($"{label}  ({count}枚)", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("衣装URL", GUILayout.Width(55));
                uploadOutfitUrls[label] = EditorGUILayout.TextField(uploadOutfitUrls[label]);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            // --- 統計 ---
            EditorGUILayout.Space(5);
            var totalImages = previews.Count;
            var totalOutfits = outfitLabels.Count + (hasBase ? 1 : 0);
            EditorGUILayout.LabelField(
                $"合計: {avatarNames.Count}体 × {totalOutfits}衣装 = {totalImages}枚",
                EditorStyles.miniLabel);

            // --- ステータス ---
            if (!string.IsNullOrEmpty(uploadStatus))
            {
                var statusType = uploadStatus.StartsWith("OK")
                    ? MessageType.Info : MessageType.Warning;
                EditorGUILayout.HelpBox(uploadStatus, statusType);
            }

            GUILayout.FlexibleSpace();

            // --- 送信ボタン ---
            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button("サイトへ送信", GUILayout.Height(45)))
            {
                RegisterToCatalog(avatarNames);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(5);
        }

        /// <summary>
        /// 撮影データをcatalog.jsonに登録し、サイトから読み込める形にする
        /// </summary>
        void RegisterToCatalog(List<string> avatarNames)
        {
            var catalogPath = Path.Combine(
                Path.GetDirectoryName(outputFolder), "data", "catalog.json");
            var catalogDir = Path.GetDirectoryName(catalogPath);
            if (!Directory.Exists(catalogDir))
                Directory.CreateDirectory(catalogDir);

            // 既存カタログ読み込み
            var existingAvatars = new List<CatalogAvatar>();
            if (File.Exists(catalogPath))
            {
                try
                {
                    var json = File.ReadAllText(catalogPath);
                    existingAvatars = ParseCatalogAvatars(json);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[アバター試着室] catalog.json読み込みエラー: {e.Message}");
                }
            }

            foreach (var avName in avatarNames)
            {
                var avatarId = SanitizeFileName(avName);

                // 既存エントリを検索
                var existing = existingAvatars.FirstOrDefault(a => a.id == avatarId);
                if (existing == null)
                {
                    existing = new CatalogAvatar { id = avatarId, name = avName };
                    existingAvatars.Add(existing);
                }

                existing.name = avName;
                existing.boothUrl = avatarUrl ?? "";
                existing.thumbnailUrl = $"/renders/{avatarId}/素体.png";

                // 衣装を追加/更新
                var outfitLabels = previews
                    .Where(p => p.avatarName == avName && p.label != "素体")
                    .Select(p => p.label)
                    .Distinct();

                foreach (var label in outfitLabels)
                {
                    var outfitId = SanitizeFileName(label);
                    var existingOutfit = existing.outfits.FirstOrDefault(o => o.id == outfitId);
                    if (existingOutfit == null)
                    {
                        existingOutfit = new CatalogOutfit { id = outfitId };
                        existing.outfits.Add(existingOutfit);
                    }

                    existingOutfit.name = label;
                    existingOutfit.boothUrl = uploadOutfitUrls.ContainsKey(label)
                        ? uploadOutfitUrls[label] : "";
                    existingOutfit.thumbnailUrl = $"/renders/{avatarId}/{outfitId}.png";

                    // ジャンルをmeta.jsonから取得
                    var metaPath = Path.Combine(outputFolder, avatarId, "meta.json");
                    if (File.Exists(metaPath))
                    {
                        var metaJson = File.ReadAllText(metaPath);
                        var genrePattern = $"\"{System.Text.RegularExpressions.Regex.Escape(outfitId)}\"[^}}]*\"genre\"\\s*:\\s*\"([^\"]*)\"";
                        var genreMatch = System.Text.RegularExpressions.Regex.Match(metaJson, genrePattern);
                        if (genreMatch.Success)
                            existingOutfit.genre = genreMatch.Groups[1].Value;
                    }
                }
            }

            // catalog.json書き出し
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"avatars\": [");
            for (int i = 0; i < existingAvatars.Count; i++)
            {
                var av = existingAvatars[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"id\": \"{EscapeJson(av.id)}\",");
                sb.AppendLine($"      \"name\": \"{EscapeJson(av.name)}\",");
                sb.AppendLine($"      \"boothUrl\": \"{EscapeJson(av.boothUrl)}\",");
                sb.AppendLine($"      \"thumbnailUrl\": \"{EscapeJson(av.thumbnailUrl)}\",");
                sb.AppendLine("      \"outfits\": [");
                for (int j = 0; j < av.outfits.Count; j++)
                {
                    var o = av.outfits[j];
                    sb.AppendLine("        {");
                    sb.AppendLine($"          \"id\": \"{EscapeJson(o.id)}\",");
                    sb.AppendLine($"          \"name\": \"{EscapeJson(o.name)}\",");
                    sb.AppendLine($"          \"boothUrl\": \"{EscapeJson(o.boothUrl)}\",");
                    sb.AppendLine($"          \"genre\": \"{EscapeJson(o.genre)}\",");
                    sb.AppendLine($"          \"thumbnailUrl\": \"{EscapeJson(o.thumbnailUrl)}\"");
                    sb.Append("        }");
                    sb.AppendLine(j < av.outfits.Count - 1 ? "," : "");
                }
                sb.AppendLine("      ]");
                sb.Append("    }");
                sb.AppendLine(i < existingAvatars.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            File.WriteAllText(catalogPath, sb.ToString());

            var msg = $"カタログ登録完了！\n" +
                      $"アバター: {avatarNames.Count}体\n" +
                      $"衣装: {existingAvatars.Sum(a => a.outfits.Count)}着\n" +
                      $"保存先: {catalogPath}";
            uploadStatus = "OK: " + msg;
            Debug.Log($"[アバター試着室] {msg}");

            EditorUtility.DisplayDialog("送信完了", msg, "OK");
        }

        // --- カタログ用データクラス ---
        class CatalogAvatar
        {
            public string id = "";
            public string name = "";
            public string boothUrl = "";
            public string thumbnailUrl = "";
            public List<CatalogOutfit> outfits = new List<CatalogOutfit>();
        }

        class CatalogOutfit
        {
            public string id = "";
            public string name = "";
            public string boothUrl = "";
            public string genre = "";
            public string thumbnailUrl = "";
        }

        /// <summary>
        /// catalog.jsonからアバターリストを簡易パース
        /// </summary>
        List<CatalogAvatar> ParseCatalogAvatars(string json)
        {
            var result = new List<CatalogAvatar>();

            // "avatars" 配列内の各オブジェクトを簡易的にパース
            var avatarsMatch = System.Text.RegularExpressions.Regex.Match(
                json, "\"avatars\"\\s*:\\s*\\[");
            if (!avatarsMatch.Success) return result;

            int pos = avatarsMatch.Index + avatarsMatch.Length;
            while (pos < json.Length)
            {
                int objStart = json.IndexOf('{', pos);
                if (objStart < 0) break;

                // ネストされたオブジェクトを考慮して対応する } を見つける
                int depth = 0;
                int objEnd = objStart;
                for (int i = objStart; i < json.Length; i++)
                {
                    if (json[i] == '{') depth++;
                    else if (json[i] == '}') { depth--; if (depth == 0) { objEnd = i; break; } }
                }

                if (objEnd <= objStart) break;
                var block = json.Substring(objStart, objEnd - objStart + 1);

                var av = new CatalogAvatar();
                av.id = ExtractJsonString(block, "id");
                av.name = ExtractJsonString(block, "name");
                av.boothUrl = ExtractJsonString(block, "boothUrl");
                av.thumbnailUrl = ExtractJsonString(block, "thumbnailUrl");

                // outfitsを解析
                var outfitsMatch = System.Text.RegularExpressions.Regex.Match(
                    block, "\"outfits\"\\s*:\\s*\\[");
                if (outfitsMatch.Success)
                {
                    int oPos = outfitsMatch.Index + outfitsMatch.Length;
                    while (oPos < block.Length)
                    {
                        int oStart = block.IndexOf('{', oPos);
                        if (oStart < 0) break;
                        int oEnd = block.IndexOf('}', oStart);
                        if (oEnd < 0) break;
                        var oBlock = block.Substring(oStart, oEnd - oStart + 1);

                        var outfit = new CatalogOutfit();
                        outfit.id = ExtractJsonString(oBlock, "id");
                        outfit.name = ExtractJsonString(oBlock, "name");
                        outfit.boothUrl = ExtractJsonString(oBlock, "boothUrl");
                        outfit.genre = ExtractJsonString(oBlock, "genre");
                        outfit.thumbnailUrl = ExtractJsonString(oBlock, "thumbnailUrl");
                        av.outfits.Add(outfit);

                        oPos = oEnd + 1;
                    }
                }

                result.Add(av);
                pos = objEnd + 1;
            }

            return result;
        }

        string ExtractJsonString(string json, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                json, $"\"{key}\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        // ========== 設定タブ ==========
        void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("出力設定", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("出力先", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var path = EditorUtility.OpenFolderPanel("出力先フォルダ", outputFolder, "");
                if (!string.IsNullOrEmpty(path))
                    outputFolder = path;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("カメラ設定", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"解像度: {IMG_WIDTH} × {IMG_HEIGHT}");
            EditorGUILayout.LabelField($"カメラサイズ: {CAM_ORTHO_SIZE}");
            EditorGUILayout.LabelField($"カメラ距離: {CAM_DISTANCE}");
            EditorGUILayout.LabelField("背景: 透過PNG");
            EditorGUILayout.LabelField("アンチエイリアス: 4x");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("ポーズフォルダについて", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Assets内に「Poses」フォルダを作り、.animファイルを入れてください。\n" +
                "パスまたはファイル名に「pose」を含むAnimationClipを自動検出します。\n\n" +
                "おすすめ無料素材:\n" +
                "・ねここや PoseCollection（BOOTH）\n" +
                "・よーぽのお店 16ポーズ（BOOTH）\n" +
                "・きつねぱんち メンズポーズ集（BOOTH）",
                MessageType.Info
            );

            EditorGUILayout.Space(10);
            if (GUILayout.Button("出力フォルダを開く"))
            {
                var winPath = outputFolder.Replace("/", "\\");
                if (Directory.Exists(winPath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{winPath}\"",
                        UseShellExecute = false
                    });
            }
        }

        // ========== ポーズ適用 ==========

        /// <summary>
        /// Sceneプレビュー用（AnimationMode使用）
        /// </summary>
        void ApplyPosePreview(GameObject avatar, AnimationClip clip)
        {
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(avatar, clip, 0f);
            AnimationMode.EndSampling();

            SceneView.RepaintAll();
        }

        /// <summary>
        /// AKA方式: 一時コピーにポーズ適用 → BakeMesh → レンダリング → 一時コピー破棄
        /// 元のアバターには一切触らないのでねじれ・復元問題が発生しない
        /// </summary>

        /// <summary>
        /// 一時コピーを作成し、ポーズ適用＋衣装同期＋BakeMeshまで行う。
        /// 戻り値のbakedObjectsをレンダリングに使い、使用後にDestroyBakedObjectsで破棄する。
        /// tempInstanceも呼び出し側で破棄すること。
        /// </summary>
        (GameObject tempInstance, List<GameObject> bakedObjects) PreparePosedBake(
            GameObject sourceAvatar, AnimationClip clip)
        {
            // Step 1: 一時コピー作成
            var tempInstance = Object.Instantiate(sourceAvatar);
            tempInstance.hideFlags = HideFlags.HideAndDontSave;
            tempInstance.transform.position = sourceAvatar.transform.position;
            tempInstance.transform.rotation = sourceAvatar.transform.rotation;

            // Step 2: Animator無効化（SampleAnimationとの干渉防止）
            var animator = tempInstance.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = false;

            // Step 3: ポーズ適用（clipがあれば）
            if (clip != null)
                clip.SampleAnimation(tempInstance, 0f);

            // Step 4: 衣装ボーン同期
            SyncClothingBonesToAvatar(tempInstance);

            // Step 5: BakeMesh
            var bakedObjects = BakeMeshFromAvatar(tempInstance);

            return (tempInstance, bakedObjects);
        }

        // ========== 衣装ボーン同期（AKA PreviewRenderer方式） ==========

        /// <summary>
        /// アバター直下の衣装Armatureのボーンを、アバターのArmatureのボーンに同期する。
        /// MA配置済み衣装はアニメーションで動かないため、名前マッチでTransformをコピーする。
        /// </summary>
        static void SyncClothingBonesToAvatar(GameObject avatarRoot)
        {
            Transform avatarArmature = null;
            foreach (Transform child in avatarRoot.transform)
            {
                if (child.name.ToLowerInvariant().Contains("armature"))
                {
                    avatarArmature = child;
                    break;
                }
            }
            if (avatarArmature == null) return;

            var avatarBones = new Dictionary<string, Transform>();
            BuildBoneDict(avatarArmature, avatarBones);

            foreach (Transform clothRoot in avatarRoot.transform)
            {
                if (clothRoot == avatarArmature) continue;

                foreach (Transform clothChild in clothRoot)
                {
                    if (!clothChild.name.ToLowerInvariant().Contains("armature")) continue;
                    SyncBoneRecursive(clothChild, avatarBones);
                }
            }
        }

        static void BuildBoneDict(Transform bone, Dictionary<string, Transform> dict)
        {
            dict[bone.name] = bone;
            foreach (Transform child in bone)
                BuildBoneDict(child, dict);
        }

        static void SyncBoneRecursive(Transform clothBone, Dictionary<string, Transform> avatarBones)
        {
            string boneName = clothBone.name;

            if (avatarBones.TryGetValue(boneName, out var avatarBone))
            {
                clothBone.localPosition = avatarBone.localPosition;
                clothBone.localRotation = avatarBone.localRotation;
                clothBone.localScale = avatarBone.localScale;
            }
            else
            {
                string stripped = StripBoneSuffix(boneName);
                if (stripped != boneName && avatarBones.TryGetValue(stripped, out avatarBone))
                {
                    clothBone.localPosition = avatarBone.localPosition;
                    clothBone.localRotation = avatarBone.localRotation;
                    clothBone.localScale = avatarBone.localScale;
                }
            }

            foreach (Transform child in clothBone)
                SyncBoneRecursive(child, avatarBones);
        }

        /// <summary>ボーン名の末尾の .001 や _1 等を除去</summary>
        static string StripBoneSuffix(string name)
        {
            int dotIdx = name.LastIndexOf('.');
            if (dotIdx > 0 && dotIdx < name.Length - 1)
            {
                string after = name.Substring(dotIdx + 1);
                if (int.TryParse(after, out _))
                    return name.Substring(0, dotIdx);
            }
            int usIdx = name.LastIndexOf('_');
            if (usIdx > 0 && usIdx < name.Length - 1)
            {
                string after = name.Substring(usIdx + 1);
                if (int.TryParse(after, out _))
                    return name.Substring(0, usIdx);
            }
            return name;
        }

        // ========== BakeMesh ヘルパー ==========

        /// <summary>
        /// アバターの全SMRをBakeMeshして一時的なMeshFilter+MeshRendererオブジェクトを生成。
        /// 返すリストの各要素は撮影後にDestroyImmediateすること。
        /// </summary>
        static List<GameObject> BakeMeshFromAvatar(GameObject avatar)
        {
            var bakedObjects = new List<GameObject>();
            var smrs = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (var smr in smrs)
            {
                if (smr.sharedMesh == null) continue;
                if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;

                smr.updateWhenOffscreen = true;

                var bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);

                var bakedGO = new GameObject("__BakedMesh_" + smr.name);
                bakedGO.hideFlags = HideFlags.HideAndDontSave;

                var mf = bakedGO.AddComponent<MeshFilter>();
                mf.sharedMesh = bakedMesh;

                var mr = bakedGO.AddComponent<MeshRenderer>();
                mr.sharedMaterials = smr.sharedMaterials;

                // SMRのワールド変換を適用
                bakedGO.transform.position = smr.transform.position;
                bakedGO.transform.rotation = smr.transform.rotation;
                bakedGO.transform.localScale = smr.transform.lossyScale;

                bakedObjects.Add(bakedGO);
            }

            return bakedObjects;
        }

        /// <summary>
        /// BakeMeshオブジェクトを破棄（メッシュも含む）
        /// </summary>
        static void DestroyBakedObjects(List<GameObject> bakedObjects)
        {
            if (bakedObjects == null) return;
            foreach (var go in bakedObjects)
            {
                if (go == null) continue;
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    Object.DestroyImmediate(mf.sharedMesh);
                Object.DestroyImmediate(go);
            }
        }

        // ========== 撮影実行（全ポーズ一括） ==========
        void ShootAll(string label)
        {
            var targets = detected.Where(d => d.selected && d.go != null).ToList();
            if (targets.Count == 0 || poses.Count == 0) return;

            int total = targets.Count * poses.Count;
            int done = 0;

            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            try
            {
                foreach (var target in targets)
                {
                    foreach (var pose in poses)
                    {
                        EditorUtility.DisplayProgressBar(
                            "撮影中...",
                            $"{target.name} / {label} / {pose.name} ({done + 1}/{total})",
                            (float)done / total
                        );

                        var (tempInstance, bakedObjects) = PreparePosedBake(target.go, pose.clip);
                        var path = ShootAvatarWithBakedMeshes(
                            target.go, bakedObjects, target.name, label, pose.name);
                        AddPreview(path, target.name, label, pose.name);

                        DestroyBakedObjects(bakedObjects);
                        Object.DestroyImmediate(tempInstance);
                        done++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog(
                "撮影完了",
                $"{targets.Count}体 × {poses.Count}ポーズ = {done}枚 撮影しました！",
                "OK"
            );

            currentTab = 1;
            Repaint();
        }

        // ========== 撮影実行（BakeMesh版） ==========
        string ShootAvatarWithBakedMeshes(
            GameObject originalAvatar, List<GameObject> bakedObjects,
            string avatarName, string label, string poseName)
        {
            var avatarId = SanitizeFileName(avatarName);
            var labelId = SanitizeFileName(label);

            // カメラ位置計算（BakeMeshオブジェクトのバウンドから）
            var bounds = CalculateBoundsFromBaked(bakedObjects, originalAvatar);
            var center = bounds.center;
            var camPos = center + Vector3.forward * CAM_DISTANCE;

            string fileName;
            if (!string.IsNullOrEmpty(poseName))
                fileName = $"{labelId}_{SanitizeFileName(poseName)}.png";
            else
                fileName = $"{labelId}.png";

            var path = Path.Combine(outputFolder, avatarId, fileName);
            CaptureFromBakedMeshes(originalAvatar, bakedObjects, camPos, center, path);

            // メタデータ更新
            SaveMeta(avatarId, avatarName, label, labelId);

            var poseInfo = string.IsNullOrEmpty(poseName) ? "" : $" [{poseName}]";
            Debug.Log($"[アバター試着室] {avatarName} / {label}{poseInfo} → {path}");

            return path;
        }

        // ========== プレビュー追加 ==========
        void AddPreview(string path, string avatarName, string label, string poseName)
        {
            var entry = new PreviewEntry
            {
                path = path,
                avatarName = avatarName,
                label = label,
                poseName = poseName
            };

            // サムネイル読み込み
            if (File.Exists(path))
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                entry.thumbnail = tex;
            }

            previews.Add(entry);
        }

        // ========== メタデータ保存 ==========
        void SaveMeta(string avatarId, string avatarName, string label, string labelId)
        {
            var metaPath = Path.Combine(outputFolder, avatarId, "meta.json");

            string existingJson = "{}";
            if (File.Exists(metaPath))
                existingJson = File.ReadAllText(metaPath);

            var existingShots = new Dictionary<string, string>();
            if (existingJson.Contains("\"shots\""))
            {
                var shotsStart = existingJson.IndexOf("\"shots\"");
                if (shotsStart >= 0)
                {
                    var content = existingJson.Substring(shotsStart);
                    int searchFrom = 0;
                    while (true)
                    {
                        var keyStart = content.IndexOf("\"", searchFrom);
                        if (keyStart < 0) break;
                        var keyEnd = content.IndexOf("\"", keyStart + 1);
                        if (keyEnd < 0) break;
                        var key = content.Substring(keyStart + 1, keyEnd - keyStart - 1);

                        if (key == "shots" || key == "boothUrl" || key == "label" || key == "genre")
                        {
                            searchFrom = keyEnd + 1;
                            continue;
                        }

                        var blockStart = content.IndexOf("{", keyEnd);
                        if (blockStart < 0) break;
                        var blockEnd = content.IndexOf("}", blockStart);
                        if (blockEnd < 0) break;
                        var block = content.Substring(blockStart, blockEnd - blockStart + 1);

                        var urlMatch = System.Text.RegularExpressions.Regex.Match(
                            block, "\"boothUrl\"\\s*:\\s*\"([^\"]*)\""
                        );
                        existingShots[key] = urlMatch.Success ? urlMatch.Groups[1].Value : "";

                        searchFrom = blockEnd + 1;
                    }
                }
            }

            var oUrl = !string.IsNullOrWhiteSpace(outfitUrl) ? outfitUrl.Trim() : "";
            existingShots[labelId] = label != "素体" ? oUrl : "";

            // アバターURLは常にフィールドから取得（既存値にフォールバック）
            string avatarBoothUrl = !string.IsNullOrWhiteSpace(avatarUrl)
                ? avatarUrl.Trim() : "";
            if (string.IsNullOrEmpty(avatarBoothUrl))
            {
                var avatarUrlMatch = System.Text.RegularExpressions.Regex.Match(
                    existingJson, "\"avatarBoothUrl\"\\s*:\\s*\"([^\"]*)\""
                );
                if (avatarUrlMatch.Success)
                    avatarBoothUrl = avatarUrlMatch.Groups[1].Value;
            }

            // ジャンル
            string genre = selectedGenre < GENRES.Length - 1
                ? GENRES[selectedGenre]
                : (!string.IsNullOrWhiteSpace(customGenre) ? customGenre.Trim() : "その他");

            // 提供者名
            string contributor = !string.IsNullOrWhiteSpace(contributorName)
                ? contributorName.Trim() : "匿名";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"avatarName\": \"{EscapeJson(avatarName)}\",");
            sb.AppendLine($"  \"avatarBoothUrl\": \"{EscapeJson(avatarBoothUrl)}\",");
            sb.AppendLine($"  \"contributor\": \"{EscapeJson(contributor)}\",");
            sb.AppendLine("  \"shots\": {");
            int idx = 0;
            foreach (var kv in existingShots)
            {
                var comma = idx < existingShots.Count - 1 ? "," : "";
                if (string.IsNullOrEmpty(kv.Value))
                    sb.AppendLine($"    \"{EscapeJson(kv.Key)}\": {{ \"genre\": \"{EscapeJson(genre)}\" }}{comma}");
                else
                    sb.AppendLine($"    \"{EscapeJson(kv.Key)}\": {{ \"boothUrl\": \"{EscapeJson(kv.Value)}\", \"genre\": \"{EscapeJson(genre)}\" }}{comma}");
                idx++;
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");

            File.WriteAllText(metaPath, sb.ToString());
        }

        string EscapeJson(string s)
        {
            return s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f");
        }

        /// <summary>
        /// BakeMesh方式の透過PNG撮影。
        /// BakeMesh済みオブジェクトだけをレンダリングしてPNG保存（シンプル版）
        /// </summary>
        void CaptureFromBakedMeshes(GameObject originalAvatar, List<GameObject> bakedObjects, Vector3 camPos, Vector3 lookAt, string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // 元アバターを一時的に非表示（BakeMeshと重ならないように）
            bool wasActive = originalAvatar.activeSelf;
            originalAvatar.SetActive(false);

            var rt = new RenderTexture(IMG_WIDTH, IMG_HEIGHT, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();

            var camGO = new GameObject("__AvatarShooterCam");
            var cam = camGO.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = CAM_ORTHO_SIZE;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.transform.position = camPos;
            cam.transform.LookAt(lookAt);

            cam.Render();

            var prevRT = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(IMG_WIDTH, IMG_HEIGHT, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, IMG_WIDTH, IMG_HEIGHT), 0, 0);
            tex.Apply();
            RenderTexture.active = prevRT;

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            rt.Release();
            Object.DestroyImmediate(rt);

            // 元アバターを復元
            originalAvatar.SetActive(wasActive);
        }

        Bounds CalculateBoundsFromBaked(List<GameObject> bakedObjects, GameObject fallback)
        {
            bool init = false;
            var bounds = new Bounds();
            foreach (var go in bakedObjects)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                if (!init) { bounds = mr.bounds; init = true; }
                else bounds.Encapsulate(mr.bounds);
            }
            if (!init) return CalculateBounds(fallback);
            return bounds;
        }

        /// 旧方式（互換用に残す）
        /// </summary>
        void CaptureTransparentPNG(GameObject avatarGO, Vector3 camPos, Vector3 lookAt, string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Step 1: BakeMeshで変形済み静的メッシュを生成
            var bakedObjects = BakeMeshFromAvatar(avatarGO);

            // Step 2: 元のSMRを一時的に非表示
            var smrs = avatarGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var smrWasEnabled = new Dictionary<SkinnedMeshRenderer, bool>();
            foreach (var smr in smrs)
            {
                smrWasEnabled[smr] = smr.enabled;
                smr.enabled = false;
            }

            // Step 3: カメラ作成・レンダリング
            var rt = new RenderTexture(IMG_WIDTH, IMG_HEIGHT, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();

            var camGO = new GameObject("__AvatarShooterCam");
            var cam = camGO.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = CAM_ORTHO_SIZE;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.transform.position = camPos;
            cam.transform.LookAt(lookAt);

            cam.Render();

            // Step 4: テクスチャ読み取り・保存
            var prevRT = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(IMG_WIDTH, IMG_HEIGHT, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, IMG_WIDTH, IMG_HEIGHT), 0, 0);
            tex.Apply();
            RenderTexture.active = prevRT;

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());

            // Step 5: クリーンアップ
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            rt.Release();
            Object.DestroyImmediate(rt);

            // BakeMeshオブジェクトを破棄
            DestroyBakedObjects(bakedObjects);

            // 元のSMRを復元
            foreach (var kv in smrWasEnabled)
            {
                if (kv.Key != null)
                    kv.Key.enabled = kv.Value;
            }
        }

        Bounds CalculateBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .ToArray();

            if (renderers.Length == 0)
                return new Bounds(obj.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            sanitized = sanitized.Replace("..", "");
            if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "unnamed";
            return sanitized;
        }
    }
}
#endif
