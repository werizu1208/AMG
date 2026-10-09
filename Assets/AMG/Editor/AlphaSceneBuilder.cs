using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AMG.EditorTools
{
    /// メニュー「A・M・G > α版シーンを生成」で、ステージ1ボス戦のシーンを丸ごと組み立てる。
    /// 見た目はすべて基本形状。ボスは骨（空のTransform）＋形状の組み合わせで、実行時にIKで動く
    public static class AlphaSceneBuilder
    {
        const string Root = "Assets/AMG";
        const string MatDir = Root + "/Materials";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/Alpha_Boss1.unity";

        class Mats
        {
            public Material ground, grassTuft, house, roof, treeDark, canopy, deadCanopy, knot, wither;
            public Material wood, dryGrass, root, dark, log;
            public Material player, playerDark, playerSkin;
            public Material telegraph, spike, tracer, leaf, explosion, rootLine, airLayer, overdrive, scarecrow, scarecrowHead, grenade;
        }

        [MenuItem("A・M・G/α版シーンを生成")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (System.IO.File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("α版シーンを生成", "既存の Alpha_Boss1 シーンを作り直します。よろしいですか？", "作り直す", "キャンセル"))
                return;

            EnsureFolder(Root);
            EnsureFolder(MatDir);
            EnsureFolder(SceneDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var m = CreateMaterials();

            BuildEnvironment(m);
            BuildKnotTrees(m);
            var player = BuildPlayer(m);
            BuildBoss(m);
            SetupCamera(player);
            BuildManagers(m);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Debug.Log("[A・M・G] α版シーンを生成しました: " + ScenePath);
        }

        // ---------- マテリアル ----------

        static Mats CreateMaterials()
        {
            return new Mats
            {
                ground = Mat("Ground", new Color(0.30f, 0.33f, 0.20f)),
                grassTuft = Mat("GrassTuft", new Color(0.36f, 0.42f, 0.22f)),
                house = Mat("House", new Color(0.42f, 0.38f, 0.33f)),
                roof = Mat("Roof", new Color(0.30f, 0.22f, 0.18f)),
                treeDark = Mat("TreeDark", new Color(0.25f, 0.19f, 0.13f)),
                canopy = Mat("Canopy", new Color(0.28f, 0.36f, 0.20f)),
                deadCanopy = Mat("DeadCanopy", new Color(0.22f, 0.24f, 0.16f)),
                knot = Mat("Knot", new Color(0.55f, 0.20f, 0.12f), emission: new Color(0.45f, 0.08f, 0.02f)),
                wither = Mat("Wither", new Color(0.45f, 0.43f, 0.40f)),
                wood = Mat("PaleWood", new Color(0.82f, 0.76f, 0.62f)),
                dryGrass = Mat("DryGrass", new Color(0.55f, 0.50f, 0.30f)),
                root = Mat("Root", new Color(0.28f, 0.20f, 0.13f)),
                dark = Mat("Dark", new Color(0.06f, 0.05f, 0.04f)),
                log = Mat("Log", new Color(0.35f, 0.27f, 0.18f)),
                player = Mat("Player", new Color(0.25f, 0.30f, 0.20f)),
                playerDark = Mat("PlayerDark", new Color(0.12f, 0.12f, 0.12f)),
                playerSkin = Mat("PlayerSkin", new Color(0.75f, 0.60f, 0.50f)),
                telegraph = Mat("Telegraph", new Color(1f, 0.15f, 0.10f, 0.4f), unlit: true, transparent: true),
                spike = Mat("Spike", new Color(0.30f, 0.22f, 0.12f)),
                tracer = Mat("Tracer", new Color(1f, 0.9f, 0.5f), unlit: true),
                leaf = Mat("Leaf", new Color(0.35f, 0.50f, 0.20f)),
                explosion = Mat("Explosion", new Color(1f, 0.6f, 0.2f, 0.6f), unlit: true, transparent: true),
                rootLine = Mat("RootLine", new Color(0.30f, 0.20f, 0.12f)),
                airLayer = Mat("AirLayer", new Color(0.6f, 0.9f, 1f, 0.18f), unlit: true, transparent: true),
                overdrive = Mat("HumanWisdom", new Color(1f, 0.4f, 0.2f, 0.18f), unlit: true, transparent: true),
                scarecrow = Mat("Scarecrow", new Color(0.70f, 0.60f, 0.35f)),
                scarecrowHead = Mat("ScarecrowHead", new Color(0.85f, 0.80f, 0.70f)),
                grenade = Mat("Grenade", new Color(0.15f, 0.18f, 0.12f)),
            };
        }

        /// 両方のステージで使う演出用のマテリアル（Materials/Common に置く）
        static readonly HashSet<string> CommonMats = new HashSet<string> { "Telegraph", "Tracer", "Explosion", "AirLayer", "HumanWisdom", "Grenade" };

        /// マテリアルの置き場所：Common（共通の演出）/ Player / Stage1 / Stage7（名前が Stage7_ で始まるもの）
        internal static string MatFolder(string name) =>
            name.StartsWith("Stage7_") ? MatDir + "/Stage7"
            : name.StartsWith("Player") ? MatDir + "/Player"
            : CommonMats.Contains(name) ? MatDir + "/Common"
            : MatDir + "/Stage1";

        internal static Material Mat(string name, Color color, bool unlit = false, bool transparent = false, Color? emission = null)
        {
            string folder = MatFolder(name);
            EnsureFolder(folder);
            string path = $"{folder}/{name}.mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;

            mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (!unlit) mat.SetFloat("_Smoothness", 0.12f);
            if (transparent) MakeTransparent(mat);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        internal static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ---------- 村（戦闘エリア） ----------

        static void BuildEnvironment(Mats m)
        {
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                light.intensity = 0.9f;
                light.color = new Color(1f, 0.92f, 0.8f);
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = new Color(0.50f, 0.55f, 0.47f);

            var env = new GameObject("Village").transform;
            Prim.Create(PrimitiveType.Plane, "Ground", env, Vector3.zero, new Vector3(10f, 1f, 10f), m.ground, true);

            // 見えない外壁
            const float edge = 45f;
            for (int i = 0; i < 4; i++)
            {
                bool x = i < 2;
                float sign = i % 2 == 0 ? 1f : -1f;
                var wall = Prim.Create(PrimitiveType.Cube, "Boundary", env,
                    x ? new Vector3(edge * sign, 5f, 0f) : new Vector3(0f, 5f, edge * sign),
                    x ? new Vector3(1f, 10f, edge * 2f) : new Vector3(edge * 2f, 10f, 1f), null, true);
                Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());
            }

            // 村の中央の枯れかけた巨木（フェーズ2で少女と一体化する）
            var bigTree = Prim.Empty("GreatTree", env, new Vector3(0f, 0f, 14f));
            Prim.Create(PrimitiveType.Cylinder, "Trunk", bigTree, new Vector3(0f, 9f, 0f), new Vector3(4f, 9f, 4f), m.treeDark, true);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + 20f;
                var rot = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(55f, 0f, 0f);
                Prim.Create(PrimitiveType.Cylinder, "Branch", bigTree, new Vector3(0f, 15f, 0f) + rot * Vector3.up * 3f, new Vector3(0.9f, 3.5f, 0.9f), m.treeDark, false, rot);
                var rootRot = Quaternion.Euler(0f, a + 36f, 0f) * Quaternion.Euler(80f, 0f, 0f);
                Prim.Create(PrimitiveType.Cylinder, "Root", bigTree, new Vector3(0f, 0.4f, 0f) + rootRot * Vector3.up * 3f, new Vector3(1.1f, 3f, 1.1f), m.treeDark, false, rootRot);
            }
            Prim.Create(PrimitiveType.Sphere, "DeadCrown", bigTree, new Vector3(0f, 19f, 0f), new Vector3(12f, 6f, 12f), m.deadCanopy);

            // 廃屋
            var houses = new (float x, float z, float rot)[] { (-20, -16, 15), (18, -15, -20), (-30, 8, 80), (30, 6, -75), (-8, 31, 0), (12, 31, 5) };
            foreach (var h in houses)
            {
                var house = Prim.Empty("House", env, new Vector3(h.x, 0f, h.z));
                house.localRotation = Quaternion.Euler(0f, h.rot, 0f);
                Prim.Create(PrimitiveType.Cube, "Walls", house, new Vector3(0f, 1.75f, 0f), new Vector3(6f, 3.5f, 5f), m.house, true);
                Prim.Create(PrimitiveType.Cube, "Roof", house, new Vector3(0f, 3.5f, 0f), new Vector3(4.3f, 4.3f, 5.4f), m.roof, false, Quaternion.Euler(0f, 0f, 45f));
            }

            // 崩れた塀（遮蔽物）
            var walls = new (float x, float z, float rot)[] { (-7, -6, 20), (7, -5, -15), (-11, 5, 70), (11, 6, -60), (0, -11, 0), (-5, -24, 10), (6, -25, -10) };
            foreach (var w in walls)
                Prim.Create(PrimitiveType.Cube, "BrokenWall", env, new Vector3(w.x, 0.7f, w.z), new Vector3(3.5f, 1.4f, 0.5f), m.house, true, Quaternion.Euler(0f, w.rot, 0f));

            // 倒木
            Prim.Create(PrimitiveType.Cylinder, "FallenLog", env, new Vector3(-16f, 0.6f, -2f), new Vector3(1.2f, 4f, 1.2f), m.log, true, Quaternion.Euler(0f, 30f, 90f));
            Prim.Create(PrimitiveType.Cylinder, "FallenLog", env, new Vector3(17f, 0.6f, 12f), new Vector3(1.2f, 3.5f, 1.2f), m.log, true, Quaternion.Euler(0f, -50f, 90f));

            // 草むら
            var rng = new System.Random(7);
            for (int i = 0; i < 60; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = 6f + (float)rng.NextDouble() * 36f;
                float size = 0.6f + (float)rng.NextDouble() * 1.2f;
                Prim.Create(PrimitiveType.Cylinder, "Grass", env, new Vector3(Mathf.Cos(a) * r, size * 0.3f, Mathf.Sin(a) * r),
                    new Vector3(size, size * 0.3f, size), m.grassTuft);
            }
        }

        // ---------- 瘤の生った木 ----------

        static void BuildKnotTrees(Mats m)
        {
            var parent = new GameObject("KnotTrees").transform;
            var positions = new[] { new Vector3(-22f, 0f, -4f), new Vector3(-13f, 0f, 17f), new Vector3(13f, 0f, 18f), new Vector3(22f, 0f, -3f), new Vector3(0f, 0f, -18f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var tree = Prim.Empty($"KnotTree_{i + 1}", parent, positions[i]);
                var trunk = Prim.Create(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 2.5f, 0f), new Vector3(0.9f, 2.5f, 0.9f), m.treeDark, true);
                var crown = Prim.Create(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, 6f, 0f), new Vector3(4f, 3.5f, 4f), m.canopy);

                Vector3 toCenter = (-positions[i]).normalized;
                var knotGo = Prim.Create(PrimitiveType.Sphere, "Knot", tree, toCenter * 0.55f + Vector3.up * 2.2f, Vector3.one * 1.3f, m.knot, true);
                knotGo.layer = Layers.Enemy;
                var knot = knotGo.AddComponent<Knot>();
                knot.witherTargets = new[] { trunk.GetComponent<Renderer>(), crown.GetComponent<Renderer>() };
            }
        }

        // ---------- プレイヤー ----------

        static GameObject BuildPlayer(Mats m)
        {
            var p = new GameObject("Player");
            p.transform.position = new Vector3(0f, 0f, -34f);
            var cc = p.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.35f;

            var visual = Prim.Empty("Visual", p.transform, Vector3.zero);
            Prim.Create(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.95f, 0f), new Vector3(0.6f, 0.85f, 0.45f), m.player);
            Prim.Create(PrimitiveType.Cube, "Vest", visual, new Vector3(0f, 1.15f, 0f), new Vector3(0.62f, 0.5f, 0.48f), m.playerDark);
            Prim.Create(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 1.62f, 0f), Vector3.one * 0.32f, m.playerSkin);
            Prim.Create(PrimitiveType.Sphere, "Helmet", visual, new Vector3(0f, 1.72f, -0.02f), new Vector3(0.36f, 0.22f, 0.38f), m.playerDark);
            Prim.Create(PrimitiveType.Cube, "Gun", visual, new Vector3(0.28f, 1.25f, 0.35f), new Vector3(0.1f, 0.14f, 0.75f), m.playerDark);
            var muzzle = Prim.Empty("Muzzle", visual, new Vector3(0.28f, 1.27f, 0.75f));

            var health = p.AddComponent<PlayerHealth>();
            health.visual = visual;
            p.AddComponent<PlayerController>();
            p.AddComponent<WeaponSystem>().muzzle = muzzle;
            p.AddComponent<GadgetSystem>();
            p.AddComponent<UltSystem>();

            Prim.SetLayerRecursive(p, Layers.Player);
            return p;
        }

        // ---------- ボス ----------

        static void BuildBoss(Mats m)
        {
            var phase2Point = Prim.Empty("Phase2Point", null, new Vector3(0f, 0f, 9f));
            phase2Point.rotation = Quaternion.Euler(0f, 180f, 0f);

            var boss = new GameObject("MagicalGirl_Boss");
            boss.layer = Layers.Enemy;
            boss.transform.SetPositionAndRotation(new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f));

            var girl = BuildGirl(boss.transform, m, out var rootWraps);
            var tree = BuildTreeForm(boss.transform, m);

            var controller = boss.AddComponent<BossController>();
            controller.girlVisual = girl;
            controller.phase2Visual = tree;
            controller.rootWraps = rootWraps;
            controller.phase2Point = phase2Point;
            boss.AddComponent<BossAttacks>();
            boss.AddComponent<BossVoice>();

            Prim.SetLayerRecursive(boss, Layers.Enemy);
            BossModelSetup.Setup(boss.transform);   // 本番モデルの差し替え口と筒状の根
            tree.SetActive(false);
        }

        /// フェーズ1：木彫りの人形のような少女。骨はすべて空のTransformで、形状は骨の子
        static GameObject BuildGirl(Transform boss, Mats m, out GameObject[] rootWraps)
        {
            var girl = Prim.Empty("Girl", boss, Vector3.zero);
            var col = girl.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.75f, 0f); // 頭は弱点（WeakPoint）として別判定にするため胴体までにする
            col.height = 1.5f;
            col.radius = 0.4f;

            var hips = Prim.Empty("Hips", girl, new Vector3(0f, 0.85f, 0f));
            Prim.Create(PrimitiveType.Sphere, "Pelvis", hips, Vector3.zero, new Vector3(0.36f, 0.24f, 0.28f), m.dryGrass);
            Prim.Create(PrimitiveType.Cylinder, "Skirt", hips, new Vector3(0f, -0.15f, 0f), new Vector3(0.7f, 0.25f, 0.6f), m.dryGrass);

            var spine = Prim.Empty("Spine", hips, new Vector3(0f, 0.05f, 0f));
            Prim.Create(PrimitiveType.Capsule, "Torso", spine, new Vector3(0f, 0.28f, 0f), new Vector3(0.34f, 0.32f, 0.24f), m.wood);
            Prim.Create(PrimitiveType.Cube, "Dress", spine, new Vector3(0f, 0.22f, 0f), new Vector3(0.36f, 0.32f, 0.26f), m.dryGrass);

            // 瘤を壊すたびに巻きついていく根
            var rng = new System.Random(3);
            rootWraps = new GameObject[5];
            for (int i = 0; i < rootWraps.Length; i++)
            {
                var rot = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 28f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 28f);
                rootWraps[i] = Prim.Create(PrimitiveType.Cylinder, $"RootWrap_{i + 1}", spine, new Vector3(0f, 0.02f + i * 0.11f, 0f),
                    new Vector3(0.46f, 0.025f, 0.38f), m.root, false, rot);
            }

            var chest = Prim.Empty("Chest", spine, new Vector3(0f, 0.5f, 0f));
            var head = Prim.Empty("Head", chest, new Vector3(0f, 0.12f, 0f));
            Prim.Create(PrimitiveType.Cylinder, "Neck", head, new Vector3(0f, 0.02f, 0f), new Vector3(0.07f, 0.08f, 0.07f), m.wood);
            Prim.Create(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.17f, 0f), new Vector3(0.3f, 0.34f, 0.3f), m.wood);
            Prim.Create(PrimitiveType.Cube, "Hair", head, new Vector3(0f, 0.12f, -0.13f), new Vector3(0.34f, 0.5f, 0.1f), m.dryGrass);
            Prim.Create(PrimitiveType.Cube, "Bangs", head, new Vector3(0f, 0.29f, 0.1f), new Vector3(0.32f, 0.07f, 0.08f), m.dryGrass);
            Prim.Create(PrimitiveType.Sphere, "EyeL", head, new Vector3(-0.065f, 0.19f, 0.135f), Vector3.one * 0.045f, m.dark);
            Prim.Create(PrimitiveType.Sphere, "EyeR", head, new Vector3(0.065f, 0.19f, 0.135f), Vector3.one * 0.045f, m.dark);

            // 腕（長め：人形の不気味さ）
            var armL = Limb("ArmL", chest, new Vector3(-0.19f, -0.02f, 0f), 0.4f, 0.4f, 0.06f, 0.05f, m.wood);
            var armR = Limb("ArmR", chest, new Vector3(0.19f, -0.02f, 0f), 0.4f, 0.4f, 0.06f, 0.05f, m.wood);
            Prim.Create(PrimitiveType.Sphere, "HandL_Mesh", armL.end, Vector3.zero, Vector3.one * 0.08f, m.wood);
            Prim.Create(PrimitiveType.Sphere, "HandR_Mesh", armR.end, Vector3.zero, Vector3.one * 0.08f, m.wood);

            // 脚
            var legL = Limb("LegL", hips, new Vector3(-0.1f, -0.05f, 0f), 0.44f, 0.44f, 0.09f, 0.07f, m.wood);
            var legR = Limb("LegR", hips, new Vector3(0.1f, -0.05f, 0f), 0.44f, 0.44f, 0.09f, 0.07f, m.wood);
            Prim.Create(PrimitiveType.Cube, "FootL_Mesh", legL.end, new Vector3(0f, -0.02f, 0.05f), new Vector3(0.09f, 0.05f, 0.2f), m.wood);
            Prim.Create(PrimitiveType.Cube, "FootR_Mesh", legR.end, new Vector3(0f, -0.02f, 0.05f), new Vector3(0.09f, 0.05f, 0.2f), m.wood);

            // 引きずる倒木（+Z方向に伸びる）
            const float logLength = 6f;
            var log = Prim.Empty("Log", girl, new Vector3(0.4f, 0.85f, 0f));
            Prim.Create(PrimitiveType.Cylinder, "Log_Mesh", log, new Vector3(0f, 0f, logLength * 0.5f), new Vector3(0.32f, logLength * 0.5f, 0.32f), m.log, false, Quaternion.Euler(90f, 0f, 0f));
            Prim.Create(PrimitiveType.Cylinder, "Stub", log, new Vector3(0.25f, 0f, 2.2f), new Vector3(0.12f, 0.4f, 0.12f), m.log, false, Quaternion.Euler(0f, 0f, -60f));
            Prim.Create(PrimitiveType.Cylinder, "Stub", log, new Vector3(-0.2f, 0.1f, 4f), new Vector3(0.1f, 0.35f, 0.1f), m.log, false, Quaternion.Euler(30f, 0f, 50f));

            var rig = girl.gameObject.AddComponent<GirlRig>();
            rig.hips = hips;
            rig.spine = spine;
            rig.head = head;
            rig.armLUpper = armL.upper; rig.armLLower = armL.lower; rig.handL = armL.end;
            rig.armRUpper = armR.upper; rig.armRLower = armR.lower; rig.handR = armR.end;
            rig.legLUpper = legL.upper; rig.legLLower = legL.lower; rig.footL = legL.end;
            rig.legRUpper = legR.upper; rig.legRLower = legR.lower; rig.footR = legR.end;
            rig.log = log;
            rig.logLength = logLength;
            rig.hipHeight = 0.85f;
            return girl.gameObject;
        }

        /// 二関節の手足。骨はローカル+Z方向に伸びる（TwoBoneIKの規約）
        internal static (Transform upper, Transform lower, Transform end) Limb(string name, Transform parent, Vector3 localPos,
            float upperLen, float lowerLen, float upperThick, float lowerThick, Material mat)
        {
            var upper = Prim.Empty(name + "_Upper", parent, localPos);
            Prim.Bone(upper, upperLen, upperThick, mat);
            Prim.Create(PrimitiveType.Sphere, name + "_Joint", upper, Vector3.zero, Vector3.one * upperThick * 1.2f, mat);
            var lower = Prim.Empty(name + "_Lower", upper, new Vector3(0f, 0f, upperLen));
            Prim.Bone(lower, lowerLen, lowerThick, mat);
            Prim.Create(PrimitiveType.Sphere, name + "_Joint", lower, Vector3.zero, Vector3.one * upperThick * 1.1f, mat);
            var end = Prim.Empty(name + "_End", lower, new Vector3(0f, 0f, lowerLen));
            return (upper, lower, end);
        }

        /// フェーズ2：巨木と一体化した樹木の怪物。根の腕はFABRIKで動く
        static GameObject BuildTreeForm(Transform boss, Mats m)
        {
            var tree = Prim.Empty("TreeForm", boss, Vector3.zero);
            var col = tree.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 6f, -2f); // 正面の顔（弱点）が突き出るように
            col.radius = 2.5f;
            col.height = 12f;

            Prim.Create(PrimitiveType.Cylinder, "Mass", tree, new Vector3(0f, 5f, -2f), new Vector3(5.5f, 5f, 5.5f), m.treeDark);
            Prim.Create(PrimitiveType.Sphere, "Crown", tree, new Vector3(0f, 11f, -2f), new Vector3(8f, 5f, 8f), m.deadCanopy);

            // 樹皮に残った少女の顔
            var face = Prim.Empty("Face", tree, new Vector3(0f, 6.5f, 0.9f));
            Prim.Create(PrimitiveType.Sphere, "FaceMesh", face, Vector3.zero, new Vector3(1.6f, 2f, 0.9f), m.wood);
            Prim.Create(PrimitiveType.Sphere, "EyeL", face, new Vector3(-0.35f, 0.25f, 0.38f), Vector3.one * 0.22f, m.dark);
            Prim.Create(PrimitiveType.Sphere, "EyeR", face, new Vector3(0.35f, 0.25f, 0.38f), Vector3.one * 0.22f, m.dark);
            Prim.Create(PrimitiveType.Cube, "Mouth", face, new Vector3(0f, -0.45f, 0.4f), new Vector3(0.45f, 0.07f, 0.1f), m.dark);
            Prim.Create(PrimitiveType.Cube, "Hair", face, new Vector3(0f, 0.5f, -0.35f), new Vector3(2f, 2.6f, 0.3f), m.dryGrass);

            var tendrilL = Chain("TendrilL", tree, new Vector3(-2.4f, 4.5f, 0.2f), new Vector3(-0.6f, 0.3f, 0.75f), 9, 2.3f, 0.9f, 0.25f, m.root);
            var tendrilR = Chain("TendrilR", tree, new Vector3(2.4f, 4.5f, 0.2f), new Vector3(0.6f, 0.3f, 0.75f), 9, 2.3f, 0.9f, 0.25f, m.root);

            var groundRoots = new List<FabrikChain>();
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.Lerp(-100f, 100f, i / 5f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), -0.05f, Mathf.Cos(a));
                groundRoots.Add(Chain($"GroundRoot_{i + 1}", tree, new Vector3(0f, 0.3f, -1.5f) + dir * 2.6f, dir, 5, 1.3f, 0.5f, 0.15f, m.root));
            }

            var rig = tree.gameObject.AddComponent<TreeRig>();
            rig.face = face;
            rig.tendrilL = tendrilL;
            rig.tendrilR = tendrilR;
            rig.groundRoots = groundRoots.ToArray();
            return tree.gameObject;
        }

        static FabrikChain Chain(string name, Transform parent, Vector3 localBase, Vector3 localDir, int segments,
            float segmentLength, float thickBase, float thickTip, Material mat)
        {
            var container = Prim.Empty(name, parent, localBase);
            var chain = container.gameObject.AddComponent<FabrikChain>();
            Vector3 dir = localDir.normalized;
            var joints = new Transform[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float thick = Mathf.Lerp(thickBase, thickTip, i / (float)segments);
                joints[i] = Prim.Empty($"{name}_J{i}", container, dir * segmentLength * i);
                joints[i].localRotation = Quaternion.LookRotation(dir);
                Prim.Create(PrimitiveType.Sphere, "Knuckle", joints[i], Vector3.zero, Vector3.one * thick * 1.1f, mat);
                if (i < segments) Prim.Bone(joints[i], segmentLength, thick, mat);
            }
            chain.joints = joints;
            return chain;
        }

        // ---------- カメラ・管理 ----------

        static void SetupCamera(GameObject player)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.nearClipPlane = 0.05f;
            cam.transform.SetPositionAndRotation(player.transform.position + new Vector3(0.75f, 1.8f, -4.2f), Quaternion.identity);
            var tps = cam.gameObject.AddComponent<TPSCamera>();
            tps.target = player.transform;
        }

        static void BuildManagers(Mats m)
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            var lib = go.AddComponent<VfxLibrary>();
            lib.telegraph = m.telegraph;
            lib.spike = m.spike;
            lib.tracer = m.tracer;
            lib.leaf = m.leaf;
            lib.explosion = m.explosion;
            lib.rootLine = m.rootLine;
            lib.airLayer = m.airLayer;
            lib.overdrive = m.overdrive;
            lib.wither = m.wither;
            lib.scarecrow = m.scarecrow;
            lib.scarecrowHead = m.scarecrowHead;
            lib.grenade = m.grenade;
            go.AddComponent<HUD>();
        }

        // ---------- ユーティリティ ----------

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
