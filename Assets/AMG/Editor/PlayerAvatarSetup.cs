using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AMG.EditorTools
{
    /// プレイヤーを、IKで動く骨とAI生成モデル（特殊部隊員）のアバターにする。
    /// メニュー「A・M・G > プレイヤーをリグ付きアバターにする」
    public static class PlayerAvatarSetup
    {
        const string ModelDir = "Assets/AMG/Models/Player";
        const string MatDir = "Assets/AMG/Materials";

        /// 骨の名前 → モデル（Tools/blender/process_part.py の p_ パーツ）
        static readonly Dictionary<string, string> Models = new Dictionary<string, string>
        {
            { "Hips", "p_pelvis" }, { "Spine", "p_torso" }, { "Head", "p_head" },
            { "ArmL_Upper", "p_upper_arm" }, { "ArmR_Upper", "p_upper_arm" },
            { "ArmL_Lower", "p_forearm" }, { "ArmR_Lower", "p_forearm" },
            { "ArmL_End", "p_hand_rigged" }, { "ArmR_End", "p_hand_rigged" },
            { "LegL_Upper", "p_thigh" }, { "LegR_Upper", "p_thigh" },
            { "LegL_Lower", "p_shin" }, { "LegR_Lower", "p_shin" },
            { "LegL_End", "p_boot" }, { "LegR_End", "p_boot" },
            // 銃は武器ごとに PlayerRig が出す（「A・M・G > 銃のモデルと握り方を設定」）
        };

        /// 骨ごとの位置の初期値（胴体はアンテナの分だけ低く整えられるので、頭を少し下げてすき間をなくす）
        static readonly Dictionary<string, Vector3> PositionDefaults = new Dictionary<string, Vector3>
        {
            { "Head", new Vector3(0f, -0.07f, 0f) },
        };

        /// コマンドライン（-executeMethod）から α版シーンに対して実行する
        public static void RunOnAlphaScene()
        {
            EditorSceneManager.OpenScene("Assets/AMG/Scenes/Alpha_Boss1.unity");
            Setup();
        }

        [MenuItem("A・M・G/プレイヤーをリグ付きアバターにする")]
        static void Setup()
        {
            var health = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            if (health == null || health.visual == null)
            {
                EditorUtility.DisplayDialog("A・M・G", "開いているシーンにプレイヤー（PlayerHealth と Visual）が見つかりません。", "OK");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(health.gameObject, "プレイヤーをリグ付きアバターにする");
            var visual = health.visual;

            var rig = visual.GetComponent<PlayerRig>();
            if (rig == null) rig = BuildRig(visual, health.GetComponent<WeaponSystem>());

            BossModelSetup.Setup(visual);   // 骨ごとの差し替え口
            int materials = AssetDatabase.IsValidFolder(ModelDir) ? BossModelSetup.CreateMaterials(ModelDir) : 0;
            int assigned = AssignModels(visual);

            rig.ApplyRestPose();
            foreach (var slot in visual.GetComponentsInChildren<VisualSlot>(true))
            {
                slot.enabled = false;
                slot.enabled = true;
            }

            EditorSceneManager.MarkSceneDirty(health.gameObject.scene);
            EditorSceneManager.SaveScene(health.gameObject.scene);
            EditorUtility.DisplayDialog("A・M・G", $"プレイヤーをリグ付きにしました。マテリアル {materials} 個、モデル {assigned} か所を割り当てて、シーンを保存しました。", "OK");
        }

        /// 旧来の1枚の仮モデルを消して、骨（空のTransform）と仮の形状を組み立てる
        static PlayerRig BuildRig(Transform visual, WeaponSystem weapons)
        {
            var body = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Player.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/PlayerDark.mat");
            var skin = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/PlayerSkin.mat");

            // 旧来の仮モデル（形状だけのもの）を削除。銃口（Muzzle）は残して小銃に付け替える
            var old = new List<GameObject>();
            foreach (Transform child in visual)
                if (child.GetComponent<MeshRenderer>() != null) old.Add(child.gameObject);
            foreach (var go in old) Undo.DestroyObjectImmediate(go);

            var hips = Prim.Empty("Hips", visual, new Vector3(0f, 0.95f, 0f));
            Prim.Create(PrimitiveType.Cube, "Pelvis", hips, new Vector3(0f, -0.05f, 0f), new Vector3(0.36f, 0.22f, 0.24f), dark);

            var spine = Prim.Empty("Spine", hips, new Vector3(0f, 0.05f, 0f));
            Prim.Create(PrimitiveType.Cube, "Torso", spine, new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.5f, 0.26f), dark);

            var chest = Prim.Empty("Chest", spine, new Vector3(0f, 0.5f, 0f));
            var head = Prim.Empty("Head", chest, new Vector3(0f, 0.12f, 0f));
            Prim.Create(PrimitiveType.Sphere, "Face", head, new Vector3(0f, 0.14f, 0f), Vector3.one * 0.24f, skin);
            Prim.Create(PrimitiveType.Sphere, "Helmet", head, new Vector3(0f, 0.2f, -0.01f), new Vector3(0.27f, 0.18f, 0.29f), dark);

            var armL = AlphaSceneBuilder.Limb("ArmL", chest, new Vector3(-0.21f, 0f, 0f), 0.30f, 0.28f, 0.09f, 0.08f, body);
            var armR = AlphaSceneBuilder.Limb("ArmR", chest, new Vector3(0.21f, 0f, 0f), 0.30f, 0.28f, 0.09f, 0.08f, body);
            Prim.Create(PrimitiveType.Sphere, "HandL_Mesh", armL.end, Vector3.zero, Vector3.one * 0.09f, dark);
            Prim.Create(PrimitiveType.Sphere, "HandR_Mesh", armR.end, Vector3.zero, Vector3.one * 0.09f, dark);

            var legL = AlphaSceneBuilder.Limb("LegL", hips, new Vector3(-0.11f, -0.05f, 0f), 0.45f, 0.45f, 0.13f, 0.11f, body);
            var legR = AlphaSceneBuilder.Limb("LegR", hips, new Vector3(0.11f, -0.05f, 0f), 0.45f, 0.45f, 0.13f, 0.11f, body);
            Prim.Create(PrimitiveType.Cube, "BootL_Mesh", legL.end, new Vector3(0f, -0.04f, 0.06f), new Vector3(0.11f, 0.1f, 0.26f), dark);
            Prim.Create(PrimitiveType.Cube, "BootR_Mesh", legR.end, new Vector3(0f, -0.04f, 0.06f), new Vector3(0.11f, 0.1f, 0.26f), dark);

            // 小銃：原点が床尾、+Z が銃口方向
            var rifle = Prim.Empty("Rifle", visual, new Vector3(0.2f, 1.4f, 0.3f));
            Prim.Create(PrimitiveType.Cube, "Rifle_Mesh", rifle, new Vector3(0f, 0f, 0.45f), new Vector3(0.06f, 0.1f, 0.9f), dark);
            if (weapons != null && weapons.muzzle != null)
            {
                Undo.SetTransformParent(weapons.muzzle, rifle, "銃口を小銃に付け替え");
                weapons.muzzle.localPosition = new Vector3(0f, 0.03f, 0.9f);
                weapons.muzzle.localRotation = Quaternion.identity;
            }

            var rig = Undo.AddComponent<PlayerRig>(visual.gameObject);
            rig.hips = hips;
            rig.spine = spine;
            rig.chest = chest;
            rig.head = head;
            rig.armLUpper = armL.upper; rig.armLLower = armL.lower; rig.handL = armL.end;
            rig.armRUpper = armR.upper; rig.armRLower = armR.lower; rig.handR = armR.end;
            rig.legLUpper = legL.upper; rig.legLLower = legL.lower; rig.footL = legL.end;
            rig.legRUpper = legR.upper; rig.legRLower = legR.lower; rig.footR = legR.end;
            rig.rifle = rifle;
            return rig;
        }

        static int AssignModels(Transform visual)
        {
            int assigned = 0;
            foreach (var slot in visual.GetComponentsInChildren<VisualSlot>(true))
            {
                if (!Models.TryGetValue(slot.name, out var file)) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{file}.fbx");
                if (model == null || slot.replacement == model) continue;
                Undo.RecordObject(slot, "プレイヤーのモデルを割り当て");
                slot.replacement = model;
                slot.positionOffset = PositionDefaults.TryGetValue(slot.name, out var offset) ? offset : Vector3.zero;
                slot.rotationOffset = Vector3.zero;
                // 手は左手用のグローブなので、右手は左右反転する
                slot.scale = slot.name == "ArmR_End" ? new Vector3(-1f, 1f, 1f) : Vector3.one;
                EditorUtility.SetDirty(slot);
                assigned++;
            }
            return assigned;
        }
    }
}
