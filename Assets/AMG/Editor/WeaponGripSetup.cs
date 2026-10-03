using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AMG.EditorTools
{
    /// 銃ごとのモデルと握り方を、プレイヤーに設定する。メニュー「A・M・G > 銃のモデルと握り方を設定」
    /// ・手のモデルを、指のボーン付きのグローブ（p_hand_rigged）にする。右手は左手用のグローブを反転して使う
    /// ・PlayerRig の Weapon Visuals に、アサルトライフル・ハンドガン・デバッグ武器を登録する
    /// ・握り方アセット（GripPose）がなければ初期値で作る（すでにあるものは上書きしない）
    /// 握り方の調整は「A・M・G > 握り方エディタ」で行う
    public static class WeaponGripSetup
    {
        const string ModelDir = "Assets/AMG/Models/Player";
        const string GripDir = "Assets/AMG/Settings/Grips";

        /// コマンドライン（-executeMethod）から α版シーンに対して実行する
        public static void RunOnAlphaScene()
        {
            EditorSceneManager.OpenScene("Assets/AMG/Scenes/Alpha_Boss1.unity");
            Run(false);
        }

        // 銃のローカル座標の目安（Blender で計測）
        //   アサルトライフル(p_rifle)：全長0.9。グリップ z≈0.15〜0.3、弾倉 z≈0.3〜0.4、縦グリップ z≈0.45〜0.6、銃身 y≈0
        //   ハンドガン(p_pistol)：全長0.24。グリップ z≈0〜0.07・y≈-0.07〜0、スライド上面 y≈0.08

        [MenuItem("A・M・G/銃のモデルと握り方を設定")]
        static void Menu() => Run(true);

        static void Run(bool dialog)
        {
            var rig = Object.FindFirstObjectByType<PlayerRig>(FindObjectsInactive.Include);
            if (rig == null)
            {
                if (dialog) EditorUtility.DisplayDialog("A・M・G", "開いているシーンにプレイヤー（PlayerRig）が見つかりません。先に「プレイヤーをリグ付きアバターにする」を実行してください。", "OK");
                return;
            }

            SetupRiggedHandImport();
            BossModelSetup.CreateMaterials(ModelDir);

            Undo.RegisterFullObjectHierarchyUndo(rig.gameObject, "銃のモデルと握り方を設定");
            AssignHands(rig);
            RemoveRifleSlot(rig);

            var ar = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/p_rifle.fbx");
            var pistol = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/p_pistol.fbx");
            var arGrip = LoadOrCreateGrip("Grip_AssaultRifle", RifleDefaults);
            var pistolGrip = LoadOrCreateGrip("Grip_Handgun", PistolDefaults);

            Undo.RecordObject(rig, "銃のモデルと握り方を設定");
            rig.weaponVisuals = new[]
            {
                Visual("アサルトライフル", ar, arGrip, new Vector3(0.14f, 0.04f, 0.06f), new Vector3(0f, -0.01f, 0.9f), rig, 0),
                Visual("ハンドガン", pistol, pistolGrip, new Vector3(0.02f, 0.06f, 0.38f), new Vector3(0f, 0.055f, 0.24f), rig, 1),
                Visual("デバッグ武器", ar, arGrip, new Vector3(0.14f, 0.04f, 0.06f), new Vector3(0f, -0.01f, 0.9f), rig, 2),
            };
            EditorUtility.SetDirty(rig);
            rig.ApplyRestPose();

            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            EditorSceneManager.SaveScene(rig.gameObject.scene);
            AssetDatabase.SaveAssets();
            string missing = pistol == null ? "\n※ p_pistol.fbx が見つからないため、ハンドガンは見た目なしです。" : "";
            if (dialog) EditorUtility.DisplayDialog("A・M・G", "銃のモデルと握り方を設定して、シーンを保存しました。\n握り方は「A・M・G > 握り方エディタ」で調整できます。" + missing, "OK");
        }

        /// すでに登録済みの値（手で調整した構える位置など）は残す
        static PlayerRig.WeaponVisual Visual(string label, GameObject model, GripPose grip, Vector3 hold, Vector3 muzzle, PlayerRig rig, int index)
        {
            var old = rig.weaponVisuals != null && index < rig.weaponVisuals.Length ? rig.weaponVisuals[index] : null;
            if (old != null && old.model == model)
            {
                old.label = label;
                if (old.grip == null) old.grip = grip;
                return old;
            }
            return new PlayerRig.WeaponVisual { label = label, model = model, grip = grip, holdOffset = hold, muzzle = muzzle };
        }

        // ---------- 手 ----------

        /// ボーン付きグローブは、Blender の軸をUnityの軸に焼き込んで読み込む（ボーンの向きがそのまま使える）
        static void SetupRiggedHandImport()
        {
            var importer = AssetImporter.GetAtPath($"{ModelDir}/p_hand_rigged.fbx") as ModelImporter;
            if (importer == null || importer.bakeAxisConversion) return;
            importer.bakeAxisConversion = true;
            importer.SaveAndReimport();
        }

        static void AssignHands(PlayerRig rig)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/p_hand_rigged.fbx");
            if (model == null) return;
            AssignHand(rig.handL, model, false);
            AssignHand(rig.handR, model, true);
        }

        static void AssignHand(Transform hand, GameObject model, bool mirror)
        {
            var slot = hand != null ? hand.GetComponent<VisualSlot>() : null;
            if (slot == null) return;
            Undo.RecordObject(slot, "手のモデルを割り当て");
            slot.replacement = model;
            var s = slot.scale;
            slot.scale = new Vector3(Mathf.Abs(s.x) * (mirror ? -1f : 1f), Mathf.Abs(s.y), Mathf.Abs(s.z));
            EditorUtility.SetDirty(slot);
            // 作り直して反映
            slot.enabled = false;
            slot.enabled = true;
        }

        /// 小銃の骨の固定の見た目（VisualSlot と仮の形状）を外す。銃の見た目は PlayerRig が武器ごとに出す
        static void RemoveRifleSlot(PlayerRig rig)
        {
            if (rig.rifle == null) return;
            var slot = rig.rifle.GetComponent<VisualSlot>();
            if (slot != null)
            {
                foreach (var p in slot.placeholders)
                    if (p != null) Undo.DestroyObjectImmediate(p);
                Undo.DestroyObjectImmediate(slot);
            }
            var mesh = rig.rifle.Find("Rifle_Mesh");
            if (mesh != null) Undo.DestroyObjectImmediate(mesh.gameObject);
        }

        // ---------- 握り方の初期値 ----------
        // 銃のローカル座標：原点＝後端、+Z が銃口、+Y が上。
        // 手の向き：指が +Z、手の甲が +Y のときが (0,0,0)

        static void RifleDefaults(GripPose g)
        {
            // 右手：グリップを右側から握る（手の甲が右、親指が上）。人差し指は引き金に沿わせる
            g.right.position = new Vector3(0.035f, -0.08f, 0.13f);
            g.right.rotation = new Vector3(0f, 0f, -90f);
            g.right.SetCurl(30f, 20f, 85f, 85f, 85f);
            // 左手：ハンドガード下の縦グリップを左側から握る（手の甲が左、親指が上）
            g.left.position = new Vector3(-0.035f, -0.09f, 0.43f);
            g.left.rotation = new Vector3(0f, 0f, 90f);
            g.left.SetCurl(30f, 80f, 85f, 85f, 85f);
        }

        static void PistolDefaults(GripPose g)
        {
            // 右手：グリップを握る
            g.right.position = new Vector3(0.03f, -0.035f, -0.035f);
            g.right.rotation = new Vector3(0f, 0f, -90f);
            g.right.SetCurl(25f, 20f, 85f, 85f, 85f);
            // 左手：右手を左側から包む（両手で構える）
            g.left.position = new Vector3(-0.045f, -0.05f, -0.01f);
            g.left.rotation = new Vector3(0f, 0f, 90f);
            g.left.SetCurl(10f, 70f, 75f, 75f, 75f);
        }

        static GripPose LoadOrCreateGrip(string name, System.Action<GripPose> defaults)
        {
            EnsureFolder(GripDir);
            string path = $"{GripDir}/{name}.asset";
            var grip = AssetDatabase.LoadAssetAtPath<GripPose>(path);
            if (grip != null) return grip;
            grip = ScriptableObject.CreateInstance<GripPose>();
            defaults(grip);
            AssetDatabase.CreateAsset(grip, path);
            return grip;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
