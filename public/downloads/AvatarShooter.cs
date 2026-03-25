#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace AvatarFittingRoom
{
    /// <summary>
    /// アバター試着データベース 撮影ツール v5
    ///
    /// v4からの主な変更:
    ///   - 衣装ボーン同期を「デルタ伝搬」方式に変更
    ///     (ボーン参照を差し替えず、アニメーションの差分を衣装ボーンに適用)
    ///   - MA + キセテネ等の衣装調整が撮影結果に正しく反映されるように
    ///   - レンダリング時の一時コピー非表示化（ゴースト描画防止）
    ///   - シーン内他オブジェクトのレンダリング除外
    /// </summary>
    public class AvatarShooter : EditorWindow
    {
        // ===== 定数 =====
        const string DEFAULT_OUTPUT = "renders_output";
        const int IMG_WIDTH = 1024;
        const int IMG_HEIGHT = 1440;
        const float CAM_ORTHO_SIZE = 0.85f;
        const float CAM_DISTANCE = 3f;

        // ===== Supabase =====
        const string SUPABASE_URL = "https://ksotijmuhednzfiahhoo.supabase.co";
        const string SUPABASE_ANON_KEY = "sb_publishable_IPqD4wfiJzK1PBOZDhLKVA_iqzcfv6y";

        static readonly string[] GENRES = {
            "制服", "カジュアル", "メイド", "ゴスロリ", "ワンピース",
            "水着", "和服", "ドレス", "スポーティ", "パンク",
            "セクシー", "コスプレ", "その他"
        };

        static readonly (string id, string label, string clipFileName)[] CURATED_POSES = {
            // 確認済み
            ("shy",          "照れ",         "010_0070"),  // ヒップ値0.29
            ("hip-hand",     "腰に手",       "010_0090"),  // ヒップ値0.57
            ("cool-crossed", "クール腕組み", "020_0010"),
            ("relax",        "リラックス",   "020_0020"),
            ("point",        "指差し",       "020_0040"),
            // ヒップ値が低い順に追加（スカート貫通対策）
            ("pose-e",       "ポーズE",      "030_0060"),  // ヒップ値0.007 最安全
            ("pose-f",       "ポーズF",      "010_0010"),  // ヒップ値0.189
            ("pose-g",       "ポーズG",      "030_0061"),  // ヒップ値0.008
            ("pose-h",       "ポーズH",      "010_0100"),  // ヒップ値0.251
            ("pose-i",       "ポーズI",      "010_0020"),  // ヒップ値0.215
        };

        // ===== 内部クラス =====
        class DetectedAvatar { public GameObject go; public string name; public bool selected = true; }
        class PoseEntry { public AnimationClip clip; public string name; public string id; }

        // ===== フィールド =====
        List<DetectedAvatar> detected = new List<DetectedAvatar>();
        List<PoseEntry> poses = new List<PoseEntry>();
        string outputFolder = DEFAULT_OUTPUT;
        string avatarUrl = "";
        string outfitUrl = "";
        int selectedGenre = 0;
        string customGenre = "";
        string contributorName = "";
        bool enableSmile = true;
        float smileWeight = 80f;
        Vector2 scrollPos, previewScrollPos, uploadScrollPos;

        int currentTab = 0;
        static readonly string[] TAB_NAMES = { "撮影", "プレビュー", "アップロード", "設定" };

        List<PreviewEntry> previews = new List<PreviewEntry>();
        class PreviewEntry
        {
            public string path, avatarName, label, poseName;
            public Texture2D thumbnail;
        }

        Dictionary<string, string> uploadOutfitUrls = new Dictionary<string, string>();
        string uploadStatus = "";
        bool agreedToTerms = false;

        // ===== ウィンドウ初期化 =====
        [MenuItem("Tools/アバター試着室/撮影")]
        static void ShowWindow()
        {
            var w = GetWindow<AvatarShooter>("アバター試着DB 撮影 v5");
            w.minSize = new Vector2(420, 500);
            if (w.outputFolder == DEFAULT_OUTPUT)
                w.outputFolder = Path.Combine(Application.dataPath, "..", DEFAULT_OUTPUT).Replace("\\", "/");
            w.ScanHierarchy();
            w.ScanPoses();
        }

        void OnFocus() { ScanHierarchy(); ScanPoses(); }
        void OnHierarchyChange() => ScanHierarchy();

        // ========== スキャン ==========
        void ScanHierarchy()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var prevSelected = new HashSet<string>(detected.Where(d => d.selected).Select(d => d.name));
            detected.Clear();

            foreach (var root in roots)
            {
                if (!root.activeInHierarchy) continue;
                if (root.GetComponent<Camera>() != null || root.GetComponent<Light>() != null) continue;
                var animator = root.GetComponent<Animator>();
                if (animator == null) continue;

                bool isAvatar = (animator.isHuman && animator.GetBoneTransform(HumanBodyBones.Hips) != null)
                    || root.GetComponents<Component>().Any(c => c != null && c.GetType().Name.Contains("VRCAvatarDescriptor"));

                if (isAvatar)
                    detected.Add(new DetectedAvatar { go = root, name = root.name, selected = prevSelected.Count == 0 || prevSelected.Contains(root.name) });
            }
            Repaint();
        }

        void ScanPoses()
        {
            poses.Clear();
            var guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" });
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) clips[Path.GetFileNameWithoutExtension(path)] = clip;
            }
            foreach (var (id, label, clipFileName) in CURATED_POSES)
            {
                if (clips.TryGetValue(clipFileName, out var clip))
                    poses.Add(new PoseEntry { clip = clip, name = label, id = id });
            }
        }

        // ========== GUI ==========
        void OnGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("アバター試着DB 撮影ツール v5", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
            EditorGUILayout.Space(2);
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

        // ===== 撮影タブ =====
        void DrawShootTab()
        {
            if (detected.Count == 0)
            {
                EditorGUILayout.HelpBox("アバターが見つかりません。\nヒエラルキーにアバターを配置してください。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("アバター", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(80));
            foreach (var d in detected)
            {
                EditorGUILayout.BeginHorizontal();
                d.selected = EditorGUILayout.Toggle(d.selected, GUILayout.Width(20));
                if (GUILayout.Button(d.name, EditorStyles.label))
                    Selection.activeGameObject = d.go;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            avatarUrl = EditorGUILayout.TextField("アバターURL", avatarUrl);
            outfitUrl = EditorGUILayout.TextField("衣装URL", outfitUrl);
            if (string.IsNullOrWhiteSpace(outfitUrl))
                EditorGUILayout.HelpBox("空欄なら素体として撮影します", MessageType.None);

            EditorGUILayout.Space(3);
            contributorName = EditorGUILayout.TextField("提供者名", contributorName);
            if (string.IsNullOrWhiteSpace(contributorName))
                EditorGUILayout.HelpBox("空欄なら「匿名」として登録されます", MessageType.None);

            EditorGUILayout.Space(3);
            selectedGenre = EditorGUILayout.Popup("ジャンル", selectedGenre, GENRES);
            if (selectedGenre == GENRES.Length - 1)
                customGenre = EditorGUILayout.TextField(" ", customGenre);

            GUILayout.FlexibleSpace();

            int selectedCount = detected.Count(d => d.selected);
            string label = string.IsNullOrWhiteSpace(outfitUrl) ? "素体" : ExtractBoothId(outfitUrl);

            if (poses.Count == 0)
                EditorGUILayout.HelpBox("ポーズが見つかりません。Assets内にポーズ素材を配置してください。", MessageType.Warning);

            GUI.enabled = selectedCount > 0 && poses.Count > 0;
            GUI.backgroundColor = string.IsNullOrWhiteSpace(outfitUrl) ? new Color(0.3f, 0.75f, 0.95f) : new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button($"撮影  ({selectedCount}体 × {poses.Count}ポーズ = {selectedCount * poses.Count}枚)", GUILayout.Height(45)))
                ShootAll(label);
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.Space(5);
        }

        // ===== プレビュータブ =====
        void DrawPreviewTab()
        {
            if (previews.Count == 0) { EditorGUILayout.HelpBox("まだ撮影されていません。", MessageType.Info); return; }

            EditorGUILayout.LabelField($"撮影済み: {previews.Count}枚", EditorStyles.boldLabel);
            previewScrollPos = EditorGUILayout.BeginScrollView(previewScrollPos);
            int cols = 3;
            float thumbSize = (position.width - 30) / cols;
            int col = 0;

            EditorGUILayout.BeginHorizontal();
            foreach (var p in previews)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(thumbSize));
                if (p.thumbnail != null)
                    GUI.DrawTexture(GUILayoutUtility.GetRect(thumbSize - 5, thumbSize * 1.4f), p.thumbnail, ScaleMode.ScaleToFit);
                var s = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                EditorGUILayout.LabelField($"{p.avatarName} / {p.label}", s);
                if (!string.IsNullOrEmpty(p.poseName)) EditorGUILayout.LabelField(p.poseName, s);
                EditorGUILayout.EndVertical();
                if (++col >= cols) { col = 0; EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("プレビューをクリア"))
            {
                foreach (var p in previews) if (p.thumbnail != null) Object.DestroyImmediate(p.thumbnail);
                previews.Clear();
            }
        }

        // ===== アップロードタブ =====
        void DrawUploadTab()
        {
            if (previews.Count == 0) { EditorGUILayout.HelpBox("まだ撮影されていません。\n撮影タブで撮影してから登録できます。", MessageType.Info); return; }

            var avatarNames = previews.Select(p => p.avatarName).Distinct().ToList();

            EditorGUILayout.LabelField("アバター情報", EditorStyles.boldLabel);
            foreach (var avName in avatarNames)
            {
                EditorGUILayout.LabelField(avName, EditorStyles.boldLabel);
                if (!string.IsNullOrWhiteSpace(avatarUrl))
                    EditorGUILayout.LabelField($"  URL: {avatarUrl}", EditorStyles.miniLabel);
                else
                    EditorGUILayout.HelpBox("撮影タブでアバターURLを入力してください", MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("撮影済み衣装", EditorStyles.boldLabel);

            var outfitLabels = previews.Select(p => p.label).Distinct().Where(l => l != "素体").ToList();
            uploadScrollPos = EditorGUILayout.BeginScrollView(uploadScrollPos, GUILayout.MaxHeight(200));
            foreach (var label in outfitLabels)
            {
                if (!uploadOutfitUrls.ContainsKey(label))
                    uploadOutfitUrls[label] = !string.IsNullOrWhiteSpace(outfitUrl) ? outfitUrl : "";

                EditorGUILayout.BeginHorizontal();
                var count = previews.Count(p => p.label == label);
                EditorGUILayout.LabelField($"{label} ({count}枚)", EditorStyles.boldLabel, GUILayout.Width(150));
                uploadOutfitUrls[label] = EditorGUILayout.TextField(uploadOutfitUrls[label]);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(uploadStatus))
                EditorGUILayout.HelpBox(uploadStatus, uploadStatus.StartsWith("OK") ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "送信されたデータは「アバター試着データベース」に公開されます。\n" +
                "・撮影画像は誰でも閲覧できる状態で公開されます\n" +
                "・提供者名を入力した場合、サイト上に表示されます\n" +
                "・掲載に使用したアバター・衣装の権利はクリエイターに帰属します\n" +
                "・権利者からの申請により削除される場合があります",
                MessageType.Info);

            agreedToTerms = EditorGUILayout.ToggleLeft(
                "上記を理解し、撮影データの公開に同意します",
                agreedToTerms);

            EditorGUILayout.Space(4);
            GUI.enabled = agreedToTerms;
            GUI.backgroundColor = agreedToTerms ? new Color(0.3f, 0.85f, 0.5f) : Color.gray;
            if (GUILayout.Button("サイトへ送信", GUILayout.Height(45)))
            {
                if (ValidateBeforeUpload())
                    RegisterToCatalog(avatarNames);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        // ===== 設定タブ =====
        void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("出力設定", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField("出力先", outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var p = EditorUtility.OpenFolderPanel("出力先フォルダ", outputFolder, "");
                if (!string.IsNullOrEmpty(p)) outputFolder = p;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("表情", EditorStyles.boldLabel);
            enableSmile = EditorGUILayout.Toggle("笑顔にする", enableSmile);
            if (enableSmile)
                smileWeight = EditorGUILayout.Slider("笑顔の強さ", smileWeight, 0f, 100f);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"解像度: {IMG_WIDTH}×{IMG_HEIGHT} / カメラ: ortho {CAM_ORTHO_SIZE} / 背景: 透過PNG");

            EditorGUILayout.Space(10);
            if (GUILayout.Button("出力フォルダを開く"))
            {
                var winPath = outputFolder.Replace("/", "\\");
                if (Directory.Exists(winPath))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = "explorer.exe", Arguments = $"\"{winPath}\"", UseShellExecute = false });
            }
        }

        // ========== 撮影コア（v4 新方式） ==========

        void ShootAll(string label)
        {
            var targets = detected.Where(d => d.selected && d.go != null).ToList();
            if (targets.Count == 0 || poses.Count == 0) return;

            int total = targets.Count * poses.Count, done = 0;
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();

            try
            {
                foreach (var target in targets)
                {
                    foreach (var pose in poses)
                    {
                        EditorUtility.DisplayProgressBar("撮影中...",
                            $"{target.name} / {label} / {pose.name} ({done + 1}/{total})",
                            (float)done / total);

                        var path = ShootSinglePose(target.go, pose, target.name, label);
                        AddPreview(path, target.name, label, pose.name);
                        done++;
                    }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            EditorUtility.DisplayDialog("撮影完了", $"{done}枚 撮影しました！", "OK");
            currentTab = 1;
            Repaint();
        }

        /// <summary>
        /// v5の撮影コア:
        /// 1. アバターをInstantiate（一時コピー）
        /// 2. アバターボーンのレスト状態を記録
        /// 3. SampleAnimationでポーズ適用
        /// 4. アニメーションのデルタを衣装ボーンに伝搬（キセテネ等の調整を保持）
        /// 5. BakeMeshでメッシュ焼き付け
        /// 6. 一時コピーを非表示にし、シーンをクリーンにしてレンダリング
        /// 7. 一時コピー破棄
        /// </summary>
        string ShootSinglePose(GameObject sourceAvatar, PoseEntry pose, string avatarName, string label)
        {
            // Step 1: 一時コピー
            var temp = Object.Instantiate(sourceAvatar);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.position = sourceAvatar.transform.position;
            temp.transform.rotation = sourceAvatar.transform.rotation;

            // Step 2: アバターArmatureのレスト状態を記録（ポーズ適用前）
            Transform avatarArmature = FindArmature(temp.transform);
            var restSnapshots = new Dictionary<string, BoneSnapshot>();
            if (avatarArmature != null)
                RecordBoneSnapshots(avatarArmature, restSnapshots);

            // Step 3: Animator無効化してからポーズ適用
            var animator = temp.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            if (pose.clip != null) pose.clip.SampleAnimation(temp, 0f);

            // Step 3.5: 笑顔BlendShape適用
            if (enableSmile) ApplySmile(temp, smileWeight);

            // Step 4: アニメーションデルタを衣装ボーンに伝搬（v5の核心）
            // ボーン参照を差し替えず、衣装ボーンを直接アニメーションする。
            // これによりキセテネ等の位置/スケール調整が保持される。
            if (avatarArmature != null)
            {
                var avatarBoneMap = new Dictionary<string, Transform>();
                CollectBones(avatarArmature, avatarBoneMap);
                PropagateAnimationToClothing(temp, avatarArmature, restSnapshots, avatarBoneMap);
            }

            // Step 5: BakeMesh
            var bakedObjects = BakeAllMeshes(temp);

            // Step 6: 一時コピーを非表示（カメラに映らないようにする）
            temp.SetActive(false);

            // Step 7: カメラ位置計算
            var bounds = CalculateBounds(bakedObjects);
            var center = bounds.center;
            var camPos = center + Vector3.forward * CAM_DISTANCE;

            // Step 8: シーン内の他オブジェクトを一時非表示にしてレンダリング
            var avatarId = Sanitize(avatarName);
            var labelId = Sanitize(label);
            var poseId = Sanitize(pose.id);
            var fileName = $"{labelId}_{poseId}.png";
            var outputPath = Path.Combine(outputFolder, avatarId, fileName);

            var hiddenRenderers = HideSceneRenderers(bakedObjects);
            RenderToPNG(bakedObjects, camPos, center, outputPath);
            RestoreSceneRenderers(hiddenRenderers);

            // Step 9: メタデータ保存
            SaveMeta(avatarId, avatarName, label, labelId);

            // Step 10: クリーンアップ
            DestroyBaked(bakedObjects);
            Object.DestroyImmediate(temp);

            Debug.Log($"[撮影] {avatarName} / {label} [{pose.name}] → {outputPath}");
            return outputPath;
        }

        // ========== v5: アニメーションデルタ伝搬方式 ==========

        struct BoneSnapshot
        {
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        /// <summary>
        /// アバターArmature配下の全ボーンのローカル変換を記録する。
        /// SampleAnimation前に呼び出し、レスト状態を保存する。
        /// </summary>
        void RecordBoneSnapshots(Transform bone, Dictionary<string, BoneSnapshot> dict)
        {
            if (!dict.ContainsKey(bone.name))
            {
                dict[bone.name] = new BoneSnapshot
                {
                    localPosition = bone.localPosition,
                    localRotation = bone.localRotation,
                    localScale = bone.localScale
                };
            }
            foreach (Transform child in bone)
                RecordBoneSnapshots(child, dict);
        }

        /// <summary>
        /// アバターボーンのアニメーションデルタ（レスト→ポーズの差分）を
        /// 衣装ボーンに適用する。ボーン参照は差し替えないため、
        /// キセテネ等で調整した位置/スケールオフセットが保持される。
        ///
        /// MA(Modular Avatar)はエディタ上ではArmature Lockingで同期するのみで、
        /// 衣装ボーンはアバターと別階層に残っている。SampleAnimationは
        /// アバターのボーンしか動かさないため、衣装ボーンには手動で
        /// アニメーションデルタを伝搬する必要がある。
        /// </summary>
        void PropagateAnimationToClothing(GameObject avatarRoot, Transform avatarArmature,
            Dictionary<string, BoneSnapshot> restSnapshots,
            Dictionary<string, Transform> avatarBoneMap)
        {
            var processed = new HashSet<Transform>();

            foreach (Transform childRoot in avatarRoot.transform)
            {
                if (childRoot == avatarArmature) continue;
                PropagateRecursive(childRoot, restSnapshots, avatarBoneMap, processed);
            }
        }

        void PropagateRecursive(Transform bone,
            Dictionary<string, BoneSnapshot> restSnapshots,
            Dictionary<string, Transform> avatarBoneMap,
            HashSet<Transform> processed)
        {
            if (processed.Contains(bone)) return;
            processed.Add(bone);

            // ボーン名でアバター側のマッチを探す
            string matchName = FindMatchingBoneName(bone.name, restSnapshots);
            if (matchName != null && avatarBoneMap.TryGetValue(matchName, out var avatarBone))
            {
                var rest = restSnapshots[matchName];

                // 回転デルタ: アバターボーンがレストからどれだけ回転したか
                Quaternion deltaRot = avatarBone.localRotation * Quaternion.Inverse(rest.localRotation);
                // 位置デルタ: アバターボーンがレストからどれだけ移動したか（主にHips）
                Vector3 deltaPos = avatarBone.localPosition - rest.localPosition;

                // 衣装ボーンにデルタを適用（既存のローカル変換を保持）
                bone.localRotation = deltaRot * bone.localRotation;
                bone.localPosition += deltaPos;
                // スケールは通常アニメーションされないが、念のため
                if (rest.localScale.x > 0.0001f && rest.localScale.y > 0.0001f && rest.localScale.z > 0.0001f)
                {
                    Vector3 scaleRatio = new Vector3(
                        avatarBone.localScale.x / rest.localScale.x,
                        avatarBone.localScale.y / rest.localScale.y,
                        avatarBone.localScale.z / rest.localScale.z
                    );
                    bone.localScale = Vector3.Scale(bone.localScale, scaleRatio);
                }
            }

            foreach (Transform child in bone)
                PropagateRecursive(child, restSnapshots, avatarBoneMap, processed);
        }

        /// <summary>
        /// ボーン名をアバターのスナップショット辞書から検索する。
        /// 完全一致 → サフィックス除去(.001, _1等)で再検索。
        /// </summary>
        string FindMatchingBoneName(string boneName, Dictionary<string, BoneSnapshot> snapshots)
        {
            if (snapshots.ContainsKey(boneName)) return boneName;
            string stripped = StripSuffix(boneName);
            if (stripped != boneName && snapshots.ContainsKey(stripped)) return stripped;
            return null;
        }

        /// <summary>
        /// レンダリング前にシーン内の不要なレンダラーを非表示にする。
        /// ライトはそのまま残し、baked objects以外のMeshRenderer/SkinnedMeshRendererを無効化する。
        /// </summary>
        List<Renderer> HideSceneRenderers(List<GameObject> except)
        {
            var exceptSet = new HashSet<GameObject>(except);
            var hidden = new List<Renderer>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (!root.activeInHierarchy) continue;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
                {
                    if (exceptSet.Contains(renderer.gameObject)) continue;
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        hidden.Add(renderer);
                    }
                }
            }
            return hidden;
        }

        void RestoreSceneRenderers(List<Renderer> hidden)
        {
            foreach (var r in hidden)
                if (r != null) r.enabled = true;
        }

        Transform FindArmature(Transform root)
        {
            foreach (Transform child in root)
            {
                if (child.name.ToLowerInvariant().Contains("armature"))
                    return child;
            }
            // Armatureという名前でない場合、Animatorのボーン階層から探す
            var animator = root.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null) return hips.parent;
            }
            return null;
        }

        void CollectBones(Transform bone, Dictionary<string, Transform> dict)
        {
            if (!dict.ContainsKey(bone.name))
                dict[bone.name] = bone;
            foreach (Transform child in bone)
                CollectBones(child, dict);
        }

        static string StripSuffix(string name)
        {
            // ".001" → 除去
            int dot = name.LastIndexOf('.');
            if (dot > 0 && int.TryParse(name.Substring(dot + 1), out _))
                return name.Substring(0, dot);
            // "_1" → 除去
            int us = name.LastIndexOf('_');
            if (us > 0 && int.TryParse(name.Substring(us + 1), out _))
                return name.Substring(0, us);
            return name;
        }

        // ========== BakeMesh ==========

        List<GameObject> BakeAllMeshes(GameObject avatar)
        {
            var result = new List<GameObject>();
            foreach (var smr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null || !smr.enabled || !smr.gameObject.activeInHierarchy) continue;
                smr.updateWhenOffscreen = true;

                var mesh = new Mesh();
                smr.BakeMesh(mesh);

                var go = new GameObject("__Baked_" + smr.name);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;

                // BakeMeshはローカル空間メッシュを返すので、SMRのワールド変換を適用
                go.transform.position = smr.transform.position;
                go.transform.rotation = smr.transform.rotation;
                go.transform.localScale = smr.transform.lossyScale;

                result.Add(go);
            }
            return result;
        }

        Bounds CalculateBounds(List<GameObject> objects)
        {
            bool init = false;
            var bounds = new Bounds();
            foreach (var go in objects)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                if (!init) { bounds = mr.bounds; init = true; }
                else bounds.Encapsulate(mr.bounds);
            }
            return init ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }

        void DestroyBaked(List<GameObject> objects)
        {
            foreach (var go in objects)
            {
                if (go == null) continue;
                var mf = go.GetComponent<MeshFilter>();
                if (mf?.sharedMesh != null) Object.DestroyImmediate(mf.sharedMesh);
                Object.DestroyImmediate(go);
            }
        }

        // ========== レンダリング ==========

        void RenderToPNG(List<GameObject> bakedObjects, Vector3 camPos, Vector3 lookAt, string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var rt = new RenderTexture(IMG_WIDTH, IMG_HEIGHT, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            rt.Create();

            var camGO = new GameObject("__ShooterCam");
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

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(IMG_WIDTH, IMG_HEIGHT, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, IMG_WIDTH, IMG_HEIGHT), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(outputPath, tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGO);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // ========== メタデータ・カタログ ==========

        [System.Serializable] class MetaJson
        {
            public string avatarName = "";
            public string avatarBoothUrl = "";
            public string contributor = "匿名";
            public List<MetaShot> shots = new List<MetaShot>();
        }

        [System.Serializable] class MetaShot
        {
            public string id = "";
            public string boothUrl = "";
            public string genre = "";
        }

        void SaveMeta(string avatarId, string avatarName, string label, string labelId)
        {
            var metaPath = Path.Combine(outputFolder, avatarId, "meta.json");

            // 既存メタ読み込み
            MetaJson meta;
            if (File.Exists(metaPath))
            {
                try { meta = JsonUtility.FromJson<MetaJson>(File.ReadAllText(metaPath)); }
                catch { meta = new MetaJson(); }
            }
            else
            {
                meta = new MetaJson();
            }

            meta.avatarName = avatarName;
            meta.avatarBoothUrl = !string.IsNullOrWhiteSpace(avatarUrl) ? avatarUrl.Trim() : meta.avatarBoothUrl;
            meta.contributor = !string.IsNullOrWhiteSpace(contributorName) ? contributorName.Trim() : "匿名";

            string genre = selectedGenre < GENRES.Length - 1 ? GENRES[selectedGenre]
                : (!string.IsNullOrWhiteSpace(customGenre) ? customGenre.Trim() : "その他");
            string oUrl = label != "素体" && !string.IsNullOrWhiteSpace(outfitUrl) ? outfitUrl.Trim() : "";

            // 既存ショット更新 or 追加
            var existing = meta.shots.FirstOrDefault(s => s.id == labelId);
            if (existing != null)
            {
                existing.boothUrl = oUrl;
                existing.genre = genre;
            }
            else
            {
                meta.shots.Add(new MetaShot { id = labelId, boothUrl = oUrl, genre = genre });
            }

            var dir = Path.GetDirectoryName(metaPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
        }

        // ===== カタログ登録 =====

        [System.Serializable] class CatalogRoot { public List<CatalogAvatar> avatars = new List<CatalogAvatar>(); }
        [System.Serializable] class CatalogAvatar
        {
            public string id = "", name = "", boothUrl = "", thumbnailUrl = "";
            public List<CatalogOutfit> outfits = new List<CatalogOutfit>();
        }
        [System.Serializable] class CatalogOutfit
        {
            public string id = "", name = "", boothUrl = "", genre = "", thumbnailUrl = "";
            public List<string> contributors = new List<string>();
        }

        void RegisterToCatalog(List<string> avatarNames)
        {
            bool supabaseOk = true;
            int uploadedImages = 0;
            int totalImages = previews.Count;
            string contributor = SanitizeInput(
                !string.IsNullOrWhiteSpace(contributorName) ? contributorName : "匿名", 50);
            const int MAX_UPLOAD_IMAGES = 200;
            if (totalImages > MAX_UPLOAD_IMAGES)
            {
                EditorUtility.DisplayDialog("エラー", $"一度にアップロードできる画像は{MAX_UPLOAD_IMAGES}枚までです。", "OK");
                return;
            }

            try
            {
                // ===== Phase 1: Upload render images to Supabase Storage =====
                for (int i = 0; i < previews.Count; i++)
                {
                    var p = previews[i];
                    if (!File.Exists(p.path)) continue;

                    var avatarId = Sanitize(p.avatarName);
                    var fileName = Path.GetFileNameWithoutExtension(p.path);
                    var storagePath = $"renders/{avatarId}/{fileName}";

                    EditorUtility.DisplayProgressBar("Supabaseへアップロード中...",
                        $"画像 {i + 1}/{totalImages}: {avatarId}/{fileName}.png",
                        (float)i / totalImages);

                    var imageBytes = File.ReadAllBytes(p.path);
                    var err = UploadToStorage(storagePath, imageBytes);
                    if (err != null)
                    {
                        Debug.LogWarning($"[Supabase] 画像アップロード失敗: {storagePath} - {err}");
                        supabaseOk = false;
                    }
                    else
                    {
                        uploadedImages++;
                    }
                }

                // ===== Phase 2: Upsert avatar data =====
                foreach (var avName in avatarNames)
                {
                    var avatarId = Sanitize(avName);

                    EditorUtility.DisplayProgressBar("Supabaseへアップロード中...",
                        $"アバター登録: {avName}", 0.8f);

                    // 既存アバター名があればそちらを使う（ヒエラルキー名で上書きしない）
                    var existingAvatarName = FetchExistingAvatarName(avatarId);
                    var displayName = existingAvatarName ?? SanitizeInput(avName, 100);

                    var thumbnailUrl = $"/thumbnails/avatars/{ExtractBoothId(avatarUrl)}.jpg";
                    var avatarJson = JsonUtility.ToJson(new SupabaseAvatar
                    {
                        id = avatarId,
                        name = displayName,
                        booth_url = avatarUrl?.Trim() ?? "",
                        thumbnail_url = thumbnailUrl
                    });

                    var err = UpsertRow("fitting_avatars", avatarJson);
                    if (err != null)
                    {
                        Debug.LogWarning($"[Supabase] アバター登録失敗: {avatarId} - {err}");
                        supabaseOk = false;
                    }

                    // ===== Phase 3: Upsert outfit data =====
                    var outfitLabels = previews.Where(p => p.avatarName == avName && p.label != "素体")
                        .Select(p => p.label).Distinct();

                    foreach (var label in outfitLabels)
                    {
                        var outfitId = Sanitize(label);

                        EditorUtility.DisplayProgressBar("Supabaseへアップロード中...",
                            $"衣装登録: {label}", 0.9f);

                        string genre = "";
                        var metaPath = Path.Combine(outputFolder, avatarId, "meta.json");
                        if (File.Exists(metaPath))
                        {
                            try
                            {
                                var meta = JsonUtility.FromJson<MetaJson>(File.ReadAllText(metaPath));
                                var shot = meta.shots.FirstOrDefault(s => s.id == outfitId);
                                if (shot != null) genre = shot.genre;
                            }
                            catch { }
                        }

                        string oUrl = (uploadOutfitUrls.ContainsKey(label) ? uploadOutfitUrls[label] : "").Trim();

                        // Fetch existing record to merge contributors
                        string existingOutfitName;
                        var existingContributors = FetchExistingContributors(avatarId, outfitId, out existingOutfitName);
                        string outfitName = existingOutfitName ?? SanitizeInput(label, 100);
                        if (!existingContributors.Contains(contributor))
                            existingContributors.Add(contributor);

                        // PostgREST TEXT[] requires Postgres array format: {"a","b"}
                        var pgContributors = "{" + string.Join(",", existingContributors.Select(c => "\"" + EscapeJson(c) + "\"")) + "}";

                        var outfitJson = "{" +
                            $"\"avatar_id\":\"{EscapeJson(avatarId)}\"," +
                            $"\"outfit_id\":\"{EscapeJson(outfitId)}\"," +
                            $"\"name\":\"{EscapeJson(outfitName)}\"," +
                            $"\"booth_url\":\"{EscapeJson(oUrl)}\"," +
                            $"\"genre\":\"{EscapeJson(genre)}\"," +
                            $"\"contributors\":\"{EscapeJson(pgContributors)}\"" +
                        "}";

                        var outfitErr = UpsertRow("fitting_outfits", outfitJson);
                        if (outfitErr != null)
                        {
                            Debug.LogWarning($"[Supabase] 衣装登録失敗: {outfitId} - {outfitErr}");
                            supabaseOk = false;
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (supabaseOk)
            {
                Debug.Log($"[Supabase] アップロード完了: 画像{uploadedImages}枚");
            }

            // ===== Fallback: ローカル catalog.json にも書き込む =====
            WriteCatalogJsonFallback(avatarNames, contributor);

            var msg = supabaseOk
                ? $"Supabaseへ送信完了！\n画像: {uploadedImages}/{totalImages}枚\nアバター: {avatarNames.Count}体"
                : $"Supabase送信で一部エラーあり（ローカル保存済み）\n画像: {uploadedImages}/{totalImages}枚";
            uploadStatus = supabaseOk ? "OK: " + msg : "WARN: " + msg;
            Debug.Log($"[撮影ツール] {msg}");
            EditorUtility.DisplayDialog("送信完了", msg, "OK");
        }

        // ===== Supabase シリアライズ用クラス =====

        [System.Serializable] class SupabaseAvatar
        {
            public string id = "", name = "", booth_url = "", thumbnail_url = "";
        }

        [System.Serializable] class SupabaseOutfit
        {
            public string avatar_id = "", outfit_id = "", name = "", booth_url = "", genre = "";
            public List<string> contributors = new List<string>();
        }

        [System.Serializable] class SupabaseOutfitResponse
        {
            public string avatar_id = "", outfit_id = "", name = "";
            public List<string> contributors = new List<string>();
        }

        [System.Serializable] class SupabaseOutfitResponseArray
        {
            public List<SupabaseOutfitResponse> items = new List<SupabaseOutfitResponse>();
        }

        // ===== Supabase Storage アップロード =====

        /// <summary>
        /// PNGバイト列をSupabase Storageにアップロード（upsert）する。
        /// 成功時null、失敗時エラーメッセージを返す。
        /// </summary>
        string UploadToStorage(string storagePath, byte[] imageBytes)
        {
            // 10MB制限
            if (imageBytes.Length > 10 * 1024 * 1024)
                return "ファイルサイズが10MBを超えています";

            // パストラバーサル防止
            if (storagePath.Contains("..") || storagePath.Contains("\\"))
                return "不正なファイルパスです";

            var url = $"{SUPABASE_URL}/storage/v1/object/fitting-room/{storagePath}";
            var req = UnityWebRequest.Put(url, imageBytes);
            req.SetRequestHeader("apikey", SUPABASE_ANON_KEY);
            req.SetRequestHeader("Authorization", $"Bearer {SUPABASE_ANON_KEY}");
            req.SetRequestHeader("Content-Type", "image/png");
            req.SetRequestHeader("x-upsert", "true");

            req.SendWebRequest();
            while (!req.isDone) { } // Editor context, blocking is OK

            if (req.result != UnityWebRequest.Result.Success)
                return $"{req.responseCode} {req.error} - {req.downloadHandler?.text}";

            req.Dispose();
            return null;
        }

        // ===== Supabase REST: Upsert行 =====

        /// <summary>
        /// Supabase REST APIで行をupsertする。
        /// 成功時null、失敗時エラーメッセージを返す。
        /// </summary>
        static readonly HashSet<string> ALLOWED_TABLES = new HashSet<string> { "fitting_avatars", "fitting_outfits" };

        string UpsertRow(string table, string jsonBody)
        {
            if (!ALLOWED_TABLES.Contains(table))
                return $"不正なテーブル名: {table}";

            var url = $"{SUPABASE_URL}/rest/v1/{table}";
            var req = new UnityWebRequest(url, "POST");
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("apikey", SUPABASE_ANON_KEY);
            req.SetRequestHeader("Authorization", $"Bearer {SUPABASE_ANON_KEY}");
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Prefer", "resolution=merge-duplicates");

            req.SendWebRequest();
            while (!req.isDone) { } // Editor context, blocking is OK

            if (req.result != UnityWebRequest.Result.Success)
                return $"{req.responseCode} {req.error} - {req.downloadHandler?.text}";

            req.Dispose();
            return null;
        }

        // ===== Supabase REST: 既存contributors取得 =====

        /// <summary>
        /// 既存の衣装レコードからcontributorsリストを取得する。
        /// レコードが無い場合は空リストを返す。
        /// </summary>
        List<string> FetchExistingContributors(string avatarId, string outfitId, out string existingName)
        {
            existingName = null;
            var url = $"{SUPABASE_URL}/rest/v1/fitting_outfits?avatar_id=eq.{UnityWebRequest.EscapeURL(avatarId)}&outfit_id=eq.{UnityWebRequest.EscapeURL(outfitId)}&select=contributors,name";
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("apikey", SUPABASE_ANON_KEY);
            req.SetRequestHeader("Authorization", $"Bearer {SUPABASE_ANON_KEY}");

            req.SendWebRequest();
            while (!req.isDone) { } // Editor context, blocking is OK

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Supabase] contributors取得失敗: {req.error}");
                req.Dispose();
                return new List<string>();
            }

            var responseText = req.downloadHandler.text;
            req.Dispose();

            // Response is a JSON array like [{"contributors":["Alice","Bob"]}]
            // JsonUtility doesn't support top-level arrays, so we wrap it
            try
            {
                var wrapped = "{\"items\":" + responseText + "}";
                var parsed = JsonUtility.FromJson<SupabaseOutfitResponseArray>(wrapped);
                if (parsed.items != null && parsed.items.Count > 0)
                {
                    if (!string.IsNullOrEmpty(parsed.items[0].name))
                        existingName = parsed.items[0].name;
                    if (parsed.items[0].contributors != null)
                        return new List<string>(parsed.items[0].contributors);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Supabase] contributors解析失敗: {e.Message}");
            }

            return new List<string>();
        }

        // ===== ローカル catalog.json 書き込み（フォールバック） =====

        void WriteCatalogJsonFallback(List<string> avatarNames, string contributor)
        {
            var catalogPath = Path.Combine(Path.GetDirectoryName(outputFolder), "data", "catalog.json");
            var catalogDir = Path.GetDirectoryName(catalogPath);
            if (!Directory.Exists(catalogDir)) Directory.CreateDirectory(catalogDir);

            CatalogRoot catalog;
            if (File.Exists(catalogPath))
            {
                try { catalog = JsonUtility.FromJson<CatalogRoot>(File.ReadAllText(catalogPath)); }
                catch { catalog = new CatalogRoot(); }
            }
            else
            {
                catalog = new CatalogRoot();
            }

            foreach (var avName in avatarNames)
            {
                var avatarId = Sanitize(avName);
                var av = catalog.avatars.FirstOrDefault(a => a.id == avatarId);
                if (av == null) { av = new CatalogAvatar { id = avatarId }; catalog.avatars.Add(av); }

                av.name = avName;
                av.boothUrl = avatarUrl ?? "";
                av.thumbnailUrl = $"/thumbnails/avatars/{ExtractBoothId(avatarUrl)}.jpg";

                var outfitLabels = previews.Where(p => p.avatarName == avName && p.label != "素体")
                    .Select(p => p.label).Distinct();

                foreach (var label in outfitLabels)
                {
                    var outfitId = Sanitize(label);
                    var outfit = av.outfits.FirstOrDefault(o => o.id == outfitId);
                    if (outfit == null) { outfit = new CatalogOutfit { id = outfitId }; av.outfits.Add(outfit); }

                    outfit.name = label;
                    outfit.boothUrl = uploadOutfitUrls.ContainsKey(label) ? uploadOutfitUrls[label] : "";
                    outfit.thumbnailUrl = $"/thumbnails/outfits/{outfitId}.jpg";
                    if (!outfit.contributors.Contains(contributor))
                        outfit.contributors.Add(contributor);

                    var metaPath = Path.Combine(outputFolder, avatarId, "meta.json");
                    if (File.Exists(metaPath))
                    {
                        try
                        {
                            var meta = JsonUtility.FromJson<MetaJson>(File.ReadAllText(metaPath));
                            var shot = meta.shots.FirstOrDefault(s => s.id == outfitId);
                            if (shot != null) outfit.genre = shot.genre;
                        }
                        catch { }
                    }
                }
            }

            File.WriteAllText(catalogPath, JsonUtility.ToJson(catalog, true));
        }

        // ========== ユーティリティ ==========

        void AddPreview(string path, string avatarName, string label, string poseName)
        {
            var entry = new PreviewEntry { path = path, avatarName = avatarName, label = label, poseName = poseName };
            if (File.Exists(path))
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(path));
                entry.thumbnail = tex;
            }
            previews.Add(entry);
        }

        string ExtractBoothId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "素体";
            var match = System.Text.RegularExpressions.Regex.Match(url, @"items/(\d+)");
            return match.Success ? match.Groups[1].Value : url.TrimEnd('/').Split('/').Last();
        }

        string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var s = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            s = s.Replace("..", "");
            return string.IsNullOrWhiteSpace(s) ? "unnamed" : s;
        }

        // ===== 表情制御 =====

        /// <summary>
        /// アバターの顔メッシュを探し、笑顔系BlendShapeを適用する。
        /// VRChatアバターで一般的なシェイプキー名に幅広く対応。
        /// </summary>
        void ApplySmile(GameObject avatarRoot, float weight)
        {
            // 笑顔系BlendShapeの候補名（優先度順）
            string[] smileKeys = {
                // 日本語系
                "笑い", "笑顔", "にっこり", "にこ", "スマイル", "微笑み", "嬉しい",
                // 英語系
                "smile", "happy", "joy", "grin",
                // VRChat / VRM 系
                "vrc.v_aa", "Fcl_ALL_Joy", "Fcl_MTH_Joy", "Fcl_ALL_Fun",
                "face_smile", "mouth_smile",
                // MMD系
                "にやり", "ω", "∀",
            };

            bool anyApplied = false;
            var allShapeNames = new List<string>();

            var smrs = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                if (smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0) continue;

                // 全BlendShape名を収集（デバッグ用）
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                    allShapeNames.Add($"{smr.gameObject.name}/{smr.sharedMesh.GetBlendShapeName(i)}");

                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    string shapeName = smr.sharedMesh.GetBlendShapeName(i);
                    foreach (var key in smileKeys)
                    {
                        if (shapeName.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            smr.SetBlendShapeWeight(i, weight);
                            Debug.Log($"[表情] 適用: {smr.gameObject.name}/{shapeName} = {weight}%");
                            anyApplied = true;
                            goto nextSmr; // この SMR は処理済み、次へ
                        }
                    }
                }
                nextSmr:;
            }

            if (!anyApplied)
            {
                Debug.LogWarning("[表情] 笑顔BlendShapeが見つかりませんでした。以下が利用可能なBlendShapeです:");
                foreach (var name in allShapeNames)
                    Debug.Log($"  - {name}");
            }
        }

        // ===== Supabase REST: 既存アバター名取得 =====

        /// <summary>既存のアバターレコードからnameを取得する。無ければnull。</summary>
        string FetchExistingAvatarName(string avatarId)
        {
            var url = $"{SUPABASE_URL}/rest/v1/fitting_avatars?id=eq.{UnityWebRequest.EscapeURL(avatarId)}&select=name";
            var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("apikey", SUPABASE_ANON_KEY);
            req.SetRequestHeader("Authorization", $"Bearer {SUPABASE_ANON_KEY}");
            req.SendWebRequest();
            while (!req.isDone) { }

            if (req.result != UnityWebRequest.Result.Success)
            {
                req.Dispose();
                return null;
            }

            var text = req.downloadHandler.text;
            req.Dispose();

            // Response: [{"name":"新入り"}] or []
            try
            {
                var wrapped = "{\"items\":" + text + "}";
                var parsed = JsonUtility.FromJson<SupabaseAvatarResponseArray>(wrapped);
                if (parsed.items != null && parsed.items.Count > 0 && !string.IsNullOrEmpty(parsed.items[0].name))
                    return parsed.items[0].name;
            }
            catch { }

            return null;
        }

        [System.Serializable] class SupabaseAvatarResponse { public string name = ""; }
        [System.Serializable] class SupabaseAvatarResponseArray { public List<SupabaseAvatarResponse> items = new List<SupabaseAvatarResponse>(); }

        // ===== JSONエスケープ =====

        /// <summary>JSON文字列内の特殊文字をエスケープする。</summary>
        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        // ===== 入力バリデーション =====

        /// <summary>BOOTH URLの形式チェック。空欄はOK、入力ありなら booth.pm ドメイン必須。</summary>
        bool IsValidBoothUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return true;
            url = url.Trim();
            if (!url.StartsWith("https://")) return false;
            try
            {
                var uri = new System.Uri(url);
                return uri.Host.EndsWith("booth.pm");
            }
            catch { return false; }
        }

        /// <summary>HTMLタグを除去する。</summary>
        string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return System.Text.RegularExpressions.Regex.Replace(input, "<[^>]*>", "");
        }

        /// <summary>文字列を最大長で切る。</summary>
        string Truncate(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Length <= maxLength ? input : input.Substring(0, maxLength);
        }

        /// <summary>アップロード前のバリデーション。失敗時はダイアログ表示してfalseを返す。</summary>
        bool ValidateBeforeUpload()
        {
            if (!string.IsNullOrWhiteSpace(avatarUrl) && !IsValidBoothUrl(avatarUrl))
            {
                EditorUtility.DisplayDialog("入力エラー",
                    "アバターURLが不正です。\nhttps://xxx.booth.pm/items/... の形式で入力してください。", "OK");
                return false;
            }

            foreach (var kv in uploadOutfitUrls)
            {
                if (!IsValidBoothUrl(kv.Value))
                {
                    EditorUtility.DisplayDialog("入力エラー",
                        $"衣装「{kv.Key}」のURLが不正です。\nhttps://xxx.booth.pm/items/... の形式で入力してください。", "OK");
                    return false;
                }
            }

            return true;
        }

        /// <summary>入力値をサニタイズしてからアップロードに使う。</summary>
        string SanitizeInput(string input, int maxLength = 100)
        {
            return Truncate(StripHtmlTags(input?.Trim() ?? ""), maxLength);
        }
    }
}
#endif
