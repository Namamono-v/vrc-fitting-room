#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AvatarFittingRoom
{
    /// <summary>
    /// アバター試着データベース 撮影ツール v4
    ///
    /// v3からの主な変更:
    ///   - 衣装ボーン同期を「SMR.bonesの差し替え」方式に変更（ねじれ修正）
    ///   - BakeMeshの座標処理を修正
    ///   - 旧方式を完全削除・コード整理
    ///   - JSON出力にJsonUtilityを使用
    /// </summary>
    public class AvatarShooter : EditorWindow
    {
        // ===== 定数 =====
        const string DEFAULT_OUTPUT = "renders_output";
        const int IMG_WIDTH = 1024;
        const int IMG_HEIGHT = 1440;
        const float CAM_ORTHO_SIZE = 0.85f;
        const float CAM_DISTANCE = 3f;

        static readonly string[] GENRES = {
            "制服", "カジュアル", "メイド", "ゴスロリ", "ワンピース",
            "水着", "和服", "ドレス", "スポーティ", "パンク",
            "セクシー", "コスプレ", "その他"
        };

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

        // ===== ウィンドウ初期化 =====
        [MenuItem("Tools/アバター試着室/撮影")]
        static void ShowWindow()
        {
            var w = GetWindow<AvatarShooter>("アバター試着DB 撮影 v4");
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
            EditorGUILayout.LabelField("アバター試着DB 撮影ツール v4", new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 });
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

            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button("サイトへ送信", GUILayout.Height(45)))
                RegisterToCatalog(avatarNames);
            GUI.backgroundColor = Color.white;
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
        /// v4の撮影コア:
        /// 1. アバターをInstantiate（一時コピー）
        /// 2. SampleAnimationでポーズ適用
        /// 3. 衣装SMRのbonesをアバターArmatureのTransformに差し替え
        /// 4. BakeMeshでメッシュ焼き付け
        /// 5. カメラレンダリング → PNG保存
        /// 6. 一時コピー破棄
        /// </summary>
        string ShootSinglePose(GameObject sourceAvatar, PoseEntry pose, string avatarName, string label)
        {
            // Step 1: 一時コピー
            var temp = Object.Instantiate(sourceAvatar);
            temp.hideFlags = HideFlags.HideAndDontSave;
            temp.transform.position = sourceAvatar.transform.position;
            temp.transform.rotation = sourceAvatar.transform.rotation;

            // Step 2: Animator無効化してからポーズ適用
            var animator = temp.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            if (pose.clip != null) pose.clip.SampleAnimation(temp, 0f);

            // Step 3: 衣装SMRのbones差し替え（v4の核心）
            RemapClothingBones(temp);

            // Step 4: BakeMesh
            var bakedObjects = BakeAllMeshes(temp);

            // Step 5: カメラ位置計算
            var bounds = CalculateBounds(bakedObjects);
            var center = bounds.center;
            var camPos = center + Vector3.forward * CAM_DISTANCE;

            // Step 6: レンダリング
            var avatarId = Sanitize(avatarName);
            var labelId = Sanitize(label);
            var poseId = Sanitize(pose.id);
            var fileName = $"{labelId}_{poseId}.png";
            var outputPath = Path.Combine(outputFolder, avatarId, fileName);

            bool wasActive = sourceAvatar.activeSelf;
            sourceAvatar.SetActive(false);
            RenderToPNG(bakedObjects, camPos, center, outputPath);
            sourceAvatar.SetActive(wasActive);

            // Step 7: メタデータ保存
            SaveMeta(avatarId, avatarName, label, labelId);

            // Step 8: クリーンアップ
            DestroyBaked(bakedObjects);
            Object.DestroyImmediate(temp);

            Debug.Log($"[撮影] {avatarName} / {label} [{pose.name}] → {outputPath}");
            return outputPath;
        }

        // ========== v4: ボーン差し替え方式 ==========

        /// <summary>
        /// 衣装のSkinnedMeshRendererが参照しているボーンを、
        /// アバター本体のArmature内のボーンに直接差し替える。
        /// これにより、SampleAnimationで動いたアバターボーンに
        /// 衣装メッシュが正しく追従する。
        /// </summary>
        void RemapClothingBones(GameObject avatarRoot)
        {
            // アバター本体のArmatureを特定
            Transform avatarArmature = FindArmature(avatarRoot.transform);
            if (avatarArmature == null) return;

            // アバターの全ボーンを名前→Transform辞書に
            var avatarBoneMap = new Dictionary<string, Transform>();
            CollectBones(avatarArmature, avatarBoneMap);

            // アバター直下の衣装オブジェクトを走査
            foreach (Transform clothRoot in avatarRoot.transform)
            {
                if (clothRoot == avatarArmature) continue;

                var smrs = clothRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrs)
                {
                    if (smr.bones == null || smr.bones.Length == 0) continue;

                    var newBones = new Transform[smr.bones.Length];
                    bool anyRemapped = false;

                    for (int i = 0; i < smr.bones.Length; i++)
                    {
                        var originalBone = smr.bones[i];
                        if (originalBone == null) { newBones[i] = null; continue; }

                        string boneName = originalBone.name;

                        // 完全一致で検索
                        if (avatarBoneMap.TryGetValue(boneName, out var matched))
                        {
                            newBones[i] = matched;
                            anyRemapped = true;
                        }
                        // サフィックス除去して再検索 (.001, _1 等)
                        else
                        {
                            string stripped = StripSuffix(boneName);
                            if (stripped != boneName && avatarBoneMap.TryGetValue(stripped, out matched))
                            {
                                newBones[i] = matched;
                                anyRemapped = true;
                            }
                            else
                            {
                                // マッチしないボーンは元のまま（衣装固有ボーン）
                                newBones[i] = originalBone;
                            }
                        }
                    }

                    if (anyRemapped)
                    {
                        smr.bones = newBones;
                        // rootBoneもアバター側に差し替え
                        if (smr.rootBone != null && avatarBoneMap.TryGetValue(smr.rootBone.name, out var newRoot))
                            smr.rootBone = newRoot;
                    }
                }
            }
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
            public string id = "", name = "", boothUrl = "", genre = "", thumbnailUrl = "", contributor = "";
        }

        void RegisterToCatalog(List<string> avatarNames)
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

            string contributor = !string.IsNullOrWhiteSpace(contributorName) ? contributorName.Trim() : "匿名";

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
                    outfit.contributor = contributor;

                    // メタからジャンル取得
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

            var msg = $"カタログ登録完了！\nアバター: {avatarNames.Count}体\n衣装: {catalog.avatars.Sum(a => a.outfits.Count)}着";
            uploadStatus = "OK: " + msg;
            Debug.Log($"[撮影ツール] {msg}");
            EditorUtility.DisplayDialog("送信完了", msg, "OK");
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
    }
}
#endif
