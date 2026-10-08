using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace AMG.EditorTools
{
    /// メニュー「A・M・G > ステージ7（ラスボス）シーンを生成」で、原初の魔法少女とのボス戦のシーンを組み立てる。
    /// ・舞台：巨大都市の中心、隕石落下後のクレーター（中に障害物はない）。CG_Scene の街とクレーターを背景に置く
    /// ・プレイヤー・カメラ・GameManager（HUD）は α版（ステージ1）のシーンから複製する（握り方やモデルの設定ごと引き継ぐ）
    /// ・ボスは Avatar（フェーズ1・2）と Phase3（フェーズ3・4）のモデルを使う
    public static class Stage7SceneBuilder
    {
        const string ScenePath = "Assets/AMG/Scenes/Stage7_FinalBoss.unity";
        const string AlphaScenePath = "Assets/AMG/Scenes/Alpha_Boss1.unity";
        const string ModelDir = "Assets/AMG/Models/Stage7Boss";
        const string AvatarPath = ModelDir + "/Models/Avatar.fbx";
        const string Phase3Path = ModelDir + "/Phase3.fbx";
        const string CgScenePath = ModelDir + "/Models/CG_Scene.fbx";
        const string StageMatDir = ModelDir + "/Materials";

        const float ArenaRadius = 38f;
        const float BossHeight = 1.6f;   // 5〜6等身の少女
        static readonly Vector3 PlayerStart = new Vector3(0f, 0f, -24f);
        static readonly Vector3 BossStart = new Vector3(0f, 0f, 8f);

        class Mats
        {
            public Material telegraph, comet, burst, circle, meteor, meteorHot, star, weak, ground, fissure, rim;
        }

        [MenuItem("A・M・G/ステージ7（ラスボス）シーンを生成")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(AlphaScenePath))
            {
                EditorUtility.DisplayDialog("A・M・G", "プレイヤーを複製するため、先に「α版シーンを生成」で Alpha_Boss1 を作ってください。", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(Phase3Path) == null)
            {
                EditorUtility.DisplayDialog("A・M・G", $"ボスのモデルが見つかりません。\n{AvatarPath}\n{Phase3Path}", "OK");
                return;
            }
            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("ステージ7シーンを生成", "既存の Stage7_FinalBoss シーンを作り直します。よろしいですか？", "作り直す", "キャンセル"))
                return;

            // モデルに付いてきたマテリアルを URP に変換しておく（別プロジェクトのシェーダーのままだとピンクになる）
            Stage7MaterialFixer.FixAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var m = CreateMaterials();

            BuildEnvironment(m);
            BuildBackdrop();
            CopyPlayerFromAlpha(scene);
            BuildBoss(m);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Debug.Log("[A・M・G] ステージ7（ラスボス）のシーンを生成しました: " + ScenePath);
        }

        // ---------- マテリアル ----------

        static Mats CreateMaterials()
        {
            return new Mats
            {
                telegraph = AlphaSceneBuilder.Mat("Stage7_Telegraph", new Color(1f, 0.35f, 0.72f, 0.38f), unlit: true, transparent: true),
                comet = AlphaSceneBuilder.Mat("Stage7_Comet", new Color(1f, 0.6f, 0.88f), unlit: true),
                burst = AlphaSceneBuilder.Mat("Stage7_Burst", new Color(1f, 0.5f, 0.82f, 0.55f), unlit: true, transparent: true),
                circle = AlphaSceneBuilder.Mat("Stage7_MagicCircle", new Color(0.95f, 0.5f, 1f, 0.32f), unlit: true, transparent: true),
                meteor = StageMat("ck_meteor_obsidian") ??
                         AlphaSceneBuilder.Mat("Stage7_Meteor", new Color(0.18f, 0.12f, 0.2f), emission: new Color(0.6f, 0.15f, 0.4f)),
                meteorHot = AlphaSceneBuilder.Mat("Stage7_MeteorHot", new Color(0.9f, 0.18f, 0.1f), emission: new Color(1.6f, 0.25f, 0.1f)),
                star = AlphaSceneBuilder.Mat("Stage7_Star", new Color(1f, 0.93f, 0.8f), unlit: true),
                weak = AlphaSceneBuilder.Mat("Stage7_WeakGlow", new Color(1f, 0.35f, 0.55f, 0.75f), unlit: true, transparent: true),
                ground = StageMat("ck_ash_fractured_basalt") ?? AlphaSceneBuilder.Mat("Stage7_Ground", new Color(0.22f, 0.2f, 0.22f)),
                fissure = StageMat("ck_molten_red_fissures") ??
                          AlphaSceneBuilder.Mat("Stage7_Fissure", new Color(0.8f, 0.15f, 0.1f), emission: new Color(1.4f, 0.2f, 0.08f)),
                rim = StageMat("ck_meteor_obsidian") ?? AlphaSceneBuilder.Mat("Stage7_Rim", new Color(0.15f, 0.13f, 0.15f)),
            };
        }

        /// CG_Scene と一緒に入っているマテリアル（あれば使う）
        static Material StageMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{StageMatDir}/{name}.mat");

        // ---------- クレーター（戦闘エリア） ----------

        static void BuildEnvironment(Mats m)
        {
            // 血のように赤い、沈みかけの太陽
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(18f, -35f, 0f);
                light.intensity = 1.1f;
                light.color = new Color(1f, 0.55f, 0.45f);
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.009f;
            RenderSettings.fogColor = new Color(0.33f, 0.2f, 0.3f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.24f, 0.32f);
            var sky = StageMat("GradientSky");
            if (sky != null && sky.shader != null && sky.shader.name.ToLower().Contains("skybox")) RenderSettings.skybox = sky;

            var env = new GameObject("Crater").transform;
            // クレーターの底（平らで障害物なし）
            Prim.Create(PrimitiveType.Plane, "Ground", env, Vector3.zero, new Vector3(12f, 1f, 12f), m.ground, true);

            // 見えない外壁（円形）
            const int walls = 28;
            float segment = 2f * Mathf.PI * (ArenaRadius + 1f) / walls + 0.5f;
            for (int i = 0; i < walls; i++)
            {
                float a = i * 360f / walls;
                var rot = Quaternion.Euler(0f, a, 0f);
                var wall = Prim.Create(PrimitiveType.Cube, "Boundary", env, rot * Vector3.forward * (ArenaRadius + 1f) + Vector3.up * 6f,
                    new Vector3(segment, 12f, 1f), null, true, rot);
                Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());
            }

            // クレーターの縁（隕石の破片が積み重なった土手）
            var rng = new System.Random(77);
            for (int i = 0; i < 70; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = ArenaRadius + 3f + (float)rng.NextDouble() * 10f;
                float size = 2f + (float)rng.NextDouble() * 5f;
                var rot = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f);
                Prim.Create(PrimitiveType.Sphere, "RimRock", env, new Vector3(Mathf.Cos(a) * r, size * 0.15f, Mathf.Sin(a) * r),
                    new Vector3(size, size * 0.55f, size * 0.8f), m.rim, false, rot);
            }

            // 中心から放射状に走る、赤熱したひび（演出のみ）
            for (int i = 0; i < 14; i++)
            {
                float a = i * 360f / 14f + (float)rng.NextDouble() * 12f;
                float len = 18f + (float)rng.NextDouble() * 16f;
                var rot = Quaternion.Euler(0f, a, 0f);
                Prim.Create(PrimitiveType.Cube, "MoltenFissure", env, rot * Vector3.forward * (3f + len * 0.5f) + Vector3.up * 0.02f,
                    new Vector3(0.25f + (float)rng.NextDouble() * 0.3f, 0.02f, len), m.fissure, false, rot);
            }

            // 原初の魔法少女が乗ってきた隕石（エリアの外、正面奥に半分埋まっている）
            Prim.Create(PrimitiveType.Sphere, "ImpactMeteor", env, new Vector3(0f, 2f, ArenaRadius + 14f), new Vector3(16f, 12f, 14f), m.meteor);
        }

        /// CG_Scene の街とクレーターを背景として置く。クレーターの大きさを戦闘エリアに合わせ、
        /// 中にいる一枚絵用の少女・ライト・カメラは使わないので隠す
        static void BuildBackdrop()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CgScenePath);
            if (prefab == null) return;
            var cg = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            cg.name = "Backdrop_CG_Scene";
            Stage7MaterialFixer.FixRenderers(cg);

            foreach (var t in cg.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("CG_Body") || t.name.StartsWith("CG_Head") || t.name.StartsWith("CG_Hair"))
                    t.gameObject.SetActive(false);
            foreach (var l in cg.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var c in cg.GetComponentsInChildren<Camera>(true)) c.enabled = false;
            foreach (var c in cg.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // クレーターの半径を、戦闘エリアの少し外側に合わせる
            Renderer crater = null;
            foreach (var r in cg.GetComponentsInChildren<Renderer>(true))
                if (r.name.Contains("impact crater")) crater = r;
            Bounds b = crater != null ? crater.bounds : Combined(cg);
            float extent = Mathf.Max(b.extents.x, b.extents.z);
            if (extent > 0.01f)
            {
                float target = crater != null ? ArenaRadius + 14f : ArenaRadius * 4f;
                cg.transform.localScale *= target / extent;
                b = crater != null ? crater.bounds : Combined(cg);
                // クレーターの中心を原点に、いちばん低いところを地面の少し下にそろえる
                cg.transform.position += new Vector3(-b.center.x, -b.min.y - 0.3f, -b.center.z);
                // すり鉢の斜面がエリアの中に盛り上がってこないよう、エリアの縁で斜面が地面と同じ高さになるまで沈める
                if (crater != null) SinkCraterToArenaEdge(cg, crater);
            }
            Prim.SetLayerRecursive(cg, 0);
        }

        static void SinkCraterToArenaEdge(GameObject cg, Renderer crater)
        {
            var filter = crater.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            var col = crater.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = filter.sharedMesh;
            Physics.SyncTransforms();
            float highest = float.NegativeInfinity;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var ray = new Ray(new Vector3(Mathf.Cos(a) * ArenaRadius, 1000f, Mathf.Sin(a) * ArenaRadius), Vector3.down);
                if (col.Raycast(ray, out var hit, 2000f)) highest = Mathf.Max(highest, hit.point.y);
            }
            Object.DestroyImmediate(col);
            if (!float.IsNegativeInfinity(highest) && highest > 0f) cg.transform.position += Vector3.down * (highest - 0.05f);
        }

        static Bounds Combined(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        // ---------- プレイヤー・カメラ・GameManager（α版から複製） ----------

        static void CopyPlayerFromAlpha(Scene scene)
        {
            // 新しいシーンの既定のカメラは、複製するカメラと入れ替える
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Camera>() != null) Object.DestroyImmediate(root);

            var alpha = EditorSceneManager.OpenScene(AlphaScenePath, OpenSceneMode.Additive);
            GameObject playerSrc = null, cameraSrc = null, managerSrc = null;
            foreach (var root in alpha.GetRootGameObjects())
            {
                if (root.GetComponent<PlayerHealth>() != null) playerSrc = root;
                else if (root.GetComponent<TPSCamera>() != null) cameraSrc = root;
                else if (root.GetComponent<GameManager>() != null) managerSrc = root;
            }

            GameObject player = Copy(playerSrc, scene);
            GameObject cam = Copy(cameraSrc, scene);
            Copy(managerSrc, scene);
            EditorSceneManager.CloseScene(alpha, true);
            SceneManager.SetActiveScene(scene);

            if (player != null) player.transform.SetPositionAndRotation(PlayerStart, Quaternion.identity);
            if (cam != null)
            {
                var tps = cam.GetComponent<TPSCamera>();
                tps.target = player != null ? player.transform : null;
                tps.followBone = null;   // 複製したプレイヤーの頭を自動で使う
                cam.transform.position = PlayerStart + new Vector3(0.8f, 2.1f, -3.6f);
            }
        }

        static GameObject Copy(GameObject src, Scene scene)
        {
            if (src == null) return null;
            var copy = Object.Instantiate(src);
            copy.name = src.name;
            SceneManager.MoveGameObjectToScene(copy, scene);

            // 編集中のプレビュー（保存されない隠しオブジェクト）は複製されると重複するので、消して作り直させる
            var hidden = new List<GameObject>();
            foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                if ((t.gameObject.hideFlags & HideFlags.DontSave) != 0 && t.gameObject != copy) hidden.Add(t.gameObject);
            foreach (var go in hidden)
                if (go != null) Object.DestroyImmediate(go);
            foreach (var slot in copy.GetComponentsInChildren<VisualSlot>(true))
            {
                if (!slot.enabled) continue;
                slot.enabled = false;
                slot.enabled = true;
            }
            return copy;
        }

        // ---------- ボス ----------

        static void BuildBoss(Mats m)
        {
            var boss = new GameObject("Primordial_Boss");
            var form1 = BuildForm("Form1_Avatar", AvatarPath, boss.transform);
            var form3 = BuildForm("Form3_Phase3", Phase3Path, boss.transform);

            var controller = boss.AddComponent<PrimordialBoss>();
            controller.bossName = "原初の魔法少女";
            controller.stageTitle = "α版　ステージ7：隕石落下後のクレーター（ラスボス）";
            controller.loadoutHint = "魔法少女が力尽きたときだけ、胸の弱点が露出する。\n腕を失えばその付け根が、最後は降ってくる隕石を壊したときだけ本体が無防備になる。\nソロ出撃：HPが0になると即死。";
            controller.maxHp = 9000f;
            controller.form1 = form1;
            controller.form3 = form3;
            controller.arenaRadius = ArenaRadius - 4f;
            controller.floatPoint = new Vector3(0f, 0f, 6f);
            controller.telegraphMaterial = m.telegraph;
            controller.cometMaterial = m.comet;
            controller.burstMaterial = m.burst;
            controller.circleMaterial = m.circle;
            controller.meteorMaterial = m.meteor;
            controller.meteorHotMaterial = m.meteorHot;
            controller.familiarMaterial = m.star;
            controller.weakMaterial = m.weak;
            boss.AddComponent<PrimordialAttacks>();
            var voice = boss.AddComponent<BossVoice>();
            voice.calmLines = new string[0];
            voice.brokenLines = new string[0];

            Prim.SetLayerRecursive(boss, Layers.Enemy);
            boss.transform.SetPositionAndRotation(BossStart, Quaternion.Euler(0f, 180f, 0f));
            form3.SetActive(false);
        }

        /// モデルを入れる器（PrimordialRig を付け、+Z を正面とする）を作り、モデルの向き・大きさ・足元をそろえる
        static GameObject BuildForm(string name, string modelPath, Transform parent)
        {
            var form = new GameObject(name);
            form.transform.SetParent(parent, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            model.transform.SetParent(form.transform, false);
            Stage7MaterialFixer.FixRenderers(model);

            // 左右の腕の位置から、モデルが +Z と -Z のどちらを向いているかを判定する（左腕が -X 側なら +Z 向き）
            var l = FindBone(model, HumanBodyBones.LeftUpperArm, "upper_arm.L", "eye.L");
            var r = FindBone(model, HumanBodyBones.RightUpperArm, "upper_arm.R", "eye.R");
            if (l != null && r != null && form.transform.InverseTransformPoint(l.position).x > form.transform.InverseTransformPoint(r.position).x)
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // 背の高さを 5〜6等身の少女らしくそろえる（極端に大きい・小さいときだけ）
            Bounds b = Combined(model);
            if (b.size.y > 0.01f && (b.size.y < 1.2f || b.size.y > 2.4f))
            {
                model.transform.localScale *= BossHeight / b.size.y;
                b = Combined(model);
            }
            // 足元を地面にそろえ、体の中心を器の原点に
            Vector3 local = form.transform.InverseTransformPoint(b.center);
            model.transform.localPosition += new Vector3(-local.x, -form.transform.InverseTransformPoint(b.min).y, -local.z);
            float height = b.size.y;

            // 当たり判定はモデルと一緒に浮くように、モデルの子に置く（器と同じ座標になるよう打ち消す）
            var hitbox = new GameObject("Hitbox");
            hitbox.transform.SetParent(model.transform, false);
            hitbox.transform.position = form.transform.position;
            hitbox.transform.rotation = form.transform.rotation;
            Vector3 s = model.transform.lossyScale;
            hitbox.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
            var cap = hitbox.AddComponent<CapsuleCollider>();
            cap.height = height;
            cap.center = new Vector3(0f, height * 0.5f, 0f);
            cap.radius = 0.28f * height / BossHeight;

            foreach (var anim in model.GetComponentsInChildren<Animator>(true)) anim.applyRootMotion = false;
            form.AddComponent<PrimordialRig>();
            return form;
        }

        static Transform FindBone(GameObject model, HumanBodyBones humanBone, params string[] names)
        {
            var anim = model.GetComponentInChildren<Animator>(true);
            if (anim != null && anim.isHuman)
            {
                var t = anim.GetBoneTransform(humanBone);
                if (t != null) return t;
            }
            foreach (var n in names)
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    if (t.name == n) return t;
            return null;
        }

        static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
