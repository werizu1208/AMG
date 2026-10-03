using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AMG.EditorTools
{
    /// ボスを本番モデルに差し替えられる状態にする。
    /// ・仮の形状を持つ骨すべてに VisualSlot を付ける（AI生成モデルなどを入れるだけで差し替わる）
    /// ・根の腕・地面の根（FabrikChain）に TendrilTube を付ける（つなぎ目のない樹皮の筒にする）
    public static class BossModelSetup
    {
        [MenuItem("A・M・G/ボスを本番モデルの差し替えに対応させる")]
        static void SetupOpenScene()
        {
            var boss = Object.FindFirstObjectByType<BossController>(FindObjectsInactive.Include);
            if (boss == null)
            {
                EditorUtility.DisplayDialog("A・M・G", "開いているシーンにボス（BossController）が見つかりません。", "OK");
                return;
            }
            var (slots, tubes) = Setup(boss.transform);
            EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
            EditorSceneManager.SaveScene(boss.gameObject.scene);
            EditorUtility.DisplayDialog("A・M・G", $"差し替え口（VisualSlot）を {slots} 個、筒状の根（TendrilTube）を {tubes} 本追加して、シーンを保存しました。", "OK");
        }

        const string Stage1ModelDir = "Assets/AMG/Models/Stage1Boss";

        struct SlotDefault
        {
            public string model;
            public Vector3 position, rotation, scale;

            public SlotDefault(string model, Vector3 position, Vector3 rotation, Vector3 scale)
            {
                this.model = model;
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
            }
        }

        static SlotDefault Slot(string model, float px = 0, float py = 0, float pz = 0, float rx = 0, float ry = 0, float rz = 0, float s = 1) =>
            new SlotDefault(model, new Vector3(px, py, pz), new Vector3(rx, ry, rz), Vector3.one * s);

        /// 骨の名前 → AI生成モデルと、その位置・向き・大きさの既定値
        /// （2026-10-02 に再生中のインスペクターで調整した値。左右で同じモデルを使うため、右側は反転している）
        static readonly Dictionary<string, SlotDefault> Stage1Models = new Dictionary<string, SlotDefault>
        {
            { "Head", Slot("head", 0.01f, -0.03f, -0.03f, s: 0.7f) },
            { "Spine", Slot("torso", 0f, -0.02f, 0f, s: 0.91f) },
            { "Hips", Slot("skirt", s: 1.7f) },
            { "ArmL_Upper", Slot("upper_arm", -0.03f, 0f, 0.03f, s: 0.88f) },
            { "ArmR_Upper", Slot("upper_arm", 0.03f, 0f, 0.03f, 0f, 180f, -90f, -0.88f) },
            { "ArmL_Lower", Slot("forearm", -0.03f, 0f, -0.03f, s: 0.78f) },
            { "ArmR_Lower", Slot("forearm", 0.03f, 0f, -0.03f, s: 1.24f) },
            { "ArmL_End", Slot("hand", -0.019f, 0f, -0.13f) },
            { "ArmR_End", Slot("hand", 0.02f, 0f, 0.06f, -180f, 0f, 0f, -1.3f) },
            { "LegL_Upper", Slot("thigh") },
            { "LegR_Upper", Slot("thigh") },
            { "LegL_Lower", Slot("shin", -0.01f) },
            { "LegR_Lower", Slot("shin", -0.01f) },
            { "LegL_End", Slot("foot", 0.01f, 0.09f, 0.03f, 0f, -32.33f, 0f) },
            { "LegR_End", Slot("foot", 0.015f, 0.09f, 0.06f, 0f, -90f, 180f, -1f) },
            { "Log", Slot("log") },
            // フェーズ2：巨木の怪物（胴体は根元が原点。空洞が顔の高さ6.5mに来るよう整えてある）
            { "TreeForm", Slot("tree_body") },
            { "Face", Slot("tree_face") },
        };

        [MenuItem("A・M・G/ステージ1ボスにAI生成モデルを割り当て")]
        static void AssignStage1Models()
        {
            var boss = Object.FindFirstObjectByType<BossController>(FindObjectsInactive.Include);
            if (boss == null)
            {
                EditorUtility.DisplayDialog("A・M・G", "開いているシーンにボス（BossController）が見つかりません。", "OK");
                return;
            }
            Setup(boss.transform);
            int materials = CreateStage1Materials();
            ApplyEditorPose(boss);

            int assigned = 0;
            var missing = new List<string>();
            AssignRootBark(boss);
            foreach (var slot in boss.GetComponentsInChildren<VisualSlot>(true))
            {
                if (!Stage1Models.TryGetValue(slot.name, out var def)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Stage1ModelDir}/{def.model}.fbx");
                if (model == null)
                {
                    missing.Add(def.model);
                    continue;
                }
                // すでに同じモデルが入っている骨は、調整済みの位置・向き・大きさをそのまま残す
                if (slot.replacement == model) continue;
                Undo.RecordObject(slot, "AI生成モデルを割り当て");
                slot.replacement = model;
                slot.positionOffset = def.position;
                slot.rotationOffset = def.rotation;
                slot.scale = def.scale;
                EditorUtility.SetDirty(slot);
                assigned++;
            }
            RefreshPreviews(boss);
            EditorSceneManager.MarkSceneDirty(boss.gameObject.scene);
            EditorSceneManager.SaveScene(boss.gameObject.scene);

            string msg = $"マテリアルを {materials} 個作成し、{assigned} か所に新しくモデルを割り当てて、シーンを保存しました。";
            if (missing.Count > 0) msg += "\n見つからなかったモデル：" + string.Join(", ", new HashSet<string>(missing));
            EditorUtility.DisplayDialog("A・M・G", msg, "OK");
        }

        [MenuItem("A・M・G/ステージ1ボスのマテリアルを作成")]
        static void CreateStage1MaterialsMenu()
        {
            int n = CreateStage1Materials();
            EditorUtility.DisplayDialog("A・M・G", $"URPのマテリアルを {n} 個作成し、モデルに割り当てました。", "OK");
        }

        /// Textures/ の <パーツ名>_basecolor.jpg・_normal.jpg から URP Lit のマテリアルを作り、各FBXのマテリアルを置き換える
        static int CreateStage1Materials() => CreateMaterials(Stage1ModelDir);

        /// modelDir の各FBXに、Textures/ のテクスチャから作った URP Lit のマテリアルを割り当てる
        internal static int CreateMaterials(string modelDir)
        {
            AssetDatabase.Refresh();
            string matDir = modelDir + "/Materials";
            if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(modelDir, "Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            int count = 0;
            foreach (var file in System.IO.Directory.GetFiles(modelDir, "*.fbx"))
            {
                string path = file.Replace('\\', '/');
                string part = System.IO.Path.GetFileNameWithoutExtension(path);
                var baseMap = LoadTexture($"{modelDir}/Textures/{part}_basecolor.jpg", false);
                var normalMap = LoadTexture($"{modelDir}/Textures/{part}_normal.jpg", true);

                string matPath = $"{matDir}/{part}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetColor("_BaseColor", Color.white);
                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(mat);

                // FBXの中のマテリアルを、作ったマテリアルに置き換える
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (sub is Material m)
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat);
                importer.SaveAndReimport();
                count++;
            }
            AssetDatabase.SaveAssets();
            return count;
        }

        /// フェーズ2の根の腕・地面の根（TendrilTube）に、継ぎ目のない樹皮テクスチャのマテリアルを使う
        static void AssignRootBark(BossController boss)
        {
            var tex = LoadTexture($"{Stage1ModelDir}/Textures/root_bark_basecolor.jpg", false);
            if (tex == null) return;
            string matPath = $"{Stage1ModelDir}/Materials/root_bark.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);

            foreach (var tube in boss.GetComponentsInChildren<TendrilTube>(true))
            {
                Undo.RecordObject(tube, "根に樹皮マテリアルを設定");
                tube.material = mat;
                EditorUtility.SetDirty(tube);
            }

            // 攻撃で出てくる根（地を這う根・突き出す根・潜行の跡）も同じ樹皮にする
            var lib = Object.FindFirstObjectByType<VfxLibrary>(FindObjectsInactive.Include);
            if (lib != null)
            {
                Undo.RecordObject(lib, "攻撃の根に樹皮マテリアルを設定");
                lib.rootBark = mat;
                EditorUtility.SetDirty(lib);
            }
        }

        internal static Texture2D LoadTexture(string path, bool normalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            if (normalMap && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// 再生していないときのシーンで、少女が立ち姿に見えるようにする
        static void ApplyEditorPose(BossController boss)
        {
            var rig = boss.girlVisual.GetComponent<GirlRig>();
            if (rig == null) return;
            Undo.RegisterFullObjectHierarchyUndo(rig.gameObject, "立ち姿にする");
            Physics.SyncTransforms();
            rig.ApplyEditorPose();
            // 瘤を壊したときに巻きつく根は、ゲーム開始時と同じく隠しておく
            foreach (var wrap in boss.rootWraps)
                if (wrap != null) wrap.SetActive(false);
        }

        /// モデルの割り当てやマテリアルの変更を、編集中のプレビューに反映し直す
        static void RefreshPreviews(BossController boss)
        {
            foreach (var slot in boss.GetComponentsInChildren<VisualSlot>(true))
            {
                if (!slot.isActiveAndEnabled) continue;
                slot.enabled = false;
                slot.enabled = true;
            }
        }

        /// 追加した VisualSlot と TendrilTube の数を返す（すでに付いているものは数えない）
        public static (int slots, int tubes) Setup(Transform bossRoot)
        {
            int slots = 0, tubes = 0;
            var chains = bossRoot.GetComponentsInChildren<FabrikChain>(true);
            var chainRoots = new HashSet<Transform>();
            foreach (var chain in chains)
            {
                chainRoots.Add(chain.transform);
                if (chain.GetComponent<TendrilTube>() == null)
                {
                    Undo.AddComponent<TendrilTube>(chain.gameObject);
                    tubes++;
                }
            }

            foreach (var t in bossRoot.GetComponentsInChildren<Transform>(true))
            {
                if (IsUnderChain(t, chainRoots) || t.GetComponent<VisualSlot>() != null) continue;
                var placeholders = new List<GameObject>();
                foreach (Transform child in t)
                    if (IsPlaceholder(child)) placeholders.Add(child.gameObject);
                if (placeholders.Count == 0) continue;

                var slot = Undo.AddComponent<VisualSlot>(t.gameObject);
                slot.placeholders = placeholders.ToArray();
                slots++;
            }
            return (slots, tubes);
        }

        /// 仮の形状 = 子を持たない、メッシュだけのオブジェクト（骨・当たり判定・瘤で巻きつく根は除く）
        static bool IsPlaceholder(Transform t)
        {
            if (t.childCount > 0) return false;
            if (t.GetComponent<MeshRenderer>() == null || t.GetComponent<MeshFilter>() == null) return false;
            if (t.name.StartsWith("RootWrap")) return false;
            return t.GetComponent<Collider>() == null;
        }

        static bool IsUnderChain(Transform t, HashSet<Transform> chainRoots)
        {
            for (var p = t; p != null; p = p.parent)
                if (chainRoots.Contains(p)) return true;
            return false;
        }
    }
}
