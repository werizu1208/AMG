using UnityEditor;
using UnityEngine;

namespace AMG.EditorTools
{
    /// 銃ごとの握り方を、シーンを見ながら手で調整するツール。メニュー「A・M・G > 握り方エディタ」
    /// ・武器を選ぶと、プレイヤーがその銃を正面に構えた姿勢になる（再生していないとき）
    /// ・シーンビューの移動・回転ハンドルで、左右の手首の位置と向きを動かせる
    /// ・ウィンドウのスライダーで、指の曲げや片手持ちを変えられる
    /// ・値は GripPose アセットに保存される（再生中に変えても残る）
    public class GripEditorWindow : EditorWindow
    {
        PlayerRig rig;
        int weaponIndex;
        bool editRight = true;
        bool editLeft = true;
        Vector2 scroll;

        [MenuItem("A・M・G/握り方エディタ")]
        static void Open()
        {
            var w = GetWindow<GripEditorWindow>("握り方エディタ");
            w.minSize = new Vector2(320, 420);
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (rig != null && !Application.isPlaying) rig.EndPreview();
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            rig = null;
            Repaint();
        }

        PlayerRig FindRig()
        {
            if (rig == null) rig = Object.FindFirstObjectByType<PlayerRig>(FindObjectsInactive.Include);
            return rig;
        }

        void OnGUI()
        {
            var r = FindRig();
            if (r == null)
            {
                EditorGUILayout.HelpBox("シーンにプレイヤー（PlayerRig）が見つかりません。", MessageType.Warning);
                return;
            }
            if (r.weaponVisuals.Length == 0)
            {
                EditorGUILayout.HelpBox("PlayerRig に銃（Weapon Visuals）が登録されていません。メニュー「A・M・G > 銃のモデルと握り方を設定」を実行してください。", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var labels = new string[r.weaponVisuals.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = string.IsNullOrEmpty(r.weaponVisuals[i].label) ? $"武器 {i + 1}" : r.weaponVisuals[i].label;

            if (Application.isPlaying)
            {
                weaponIndex = Mathf.Clamp(r.CurrentIndex, 0, labels.Length - 1);
                EditorGUILayout.HelpBox($"再生中：いま持っている「{labels[weaponIndex]}」を編集します（武器の切り替えはゲーム内で）。変えた値は再生を止めても残ります。", MessageType.None);
            }
            else
            {
                weaponIndex = EditorGUILayout.Popup("武器", Mathf.Clamp(weaponIndex, 0, labels.Length - 1), labels);
            }

            var visual = r.weaponVisuals[weaponIndex];
            EditorGUI.BeginChangeCheck();
            var grip = (GripPose)EditorGUILayout.ObjectField("握り方アセット", visual.grip, typeof(GripPose), false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(r, "握り方アセットを変更");
                visual.grip = grip;
                EditorUtility.SetDirty(r);
            }

            EditorGUI.BeginChangeCheck();
            var hold = EditorGUILayout.Vector3Field(new GUIContent("構える位置（右・上・前）", "胸から見た銃の原点（後端）の位置"), visual.holdOffset);
            var muzzle = EditorGUILayout.Vector3Field(new GUIContent("銃口の位置", "銃のローカル座標"), visual.muzzle);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(r, "構える位置を変更");
                visual.holdOffset = hold;
                visual.muzzle = muzzle;
                EditorUtility.SetDirty(r);
            }

            if (visual.grip == null)
            {
                EditorGUILayout.HelpBox("この武器には握り方アセットがありません。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                Preview();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("シーンビューのハンドルで動かす手", EditorStyles.boldLabel);
            editRight = EditorGUILayout.ToggleLeft("右手", editRight);
            editLeft = EditorGUILayout.ToggleLeft("左手", editLeft);

            HandGUI("右手", visual.grip, visual.grip.right);
            HandGUI("左手", visual.grip, visual.grip.left);

            EditorGUILayout.Space();
            if (GUILayout.Button("右手の握りを左右反転して左手へコピー"))
            {
                Undo.RecordObject(visual.grip, "左手へコピー");
                var src = visual.grip.right;
                var dst = visual.grip.left;
                dst.position = new Vector3(-src.position.x, src.position.y, src.position.z);
                dst.rotation = new Vector3(src.rotation.x, -src.rotation.y, -src.rotation.z);
                for (int f = 0; f < 5; f++) dst.Get(f).CopyFrom(src.Get(f));
                EditorUtility.SetDirty(visual.grip);
            }
            EditorGUILayout.EndScrollView();
            Preview();
        }

        static readonly string[] JointNames = { "付け根", "中", "先" };
        readonly System.Collections.Generic.HashSet<string> openJoints = new System.Collections.Generic.HashSet<string>();

        void HandGUI(string title, GripPose pose, GripPose.HandGrip g)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            bool holding = EditorGUILayout.Toggle("銃を持つ", g.holding);
            Vector3 pos = EditorGUILayout.Vector3Field("手首の位置", g.position);
            Vector3 rot = EditorGUILayout.Vector3Field("手の向き", g.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(pose, "握り方を変更");
                g.holding = holding; g.position = pos; g.rotation = rot;
                EditorUtility.SetDirty(pose);
            }

            EditorGUILayout.LabelField("指の角度（X 曲げ＋で手のひら側 / Y 開き＋で親指側 / Z ひねり）", EditorStyles.miniLabel);
            for (int f = 0; f < 5; f++)
            {
                var finger = g.Get(f);
                string key = title + f;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(GripPose.HandGrip.FingerNames[f], EditorStyles.boldLabel, GUILayout.Width(70));
                bool open = GUILayout.Toggle(openJoints.Contains(key), "関節ごと", EditorStyles.miniButton, GUILayout.Width(64));
                if (open) openJoints.Add(key); else openJoints.Remove(key);
                if (GUILayout.Button("0", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    Undo.RecordObject(pose, "指の角度をリセット");
                    finger.angle = Vector3.zero;
                    finger.joints = new Vector3[3];
                    EditorUtility.SetDirty(pose);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                EditorGUI.indentLevel++;
                float x = EditorGUILayout.Slider("X 曲げ", finger.angle.x, -30f, 110f);
                float y = EditorGUILayout.Slider("Y 開き", finger.angle.y, -45f, 45f);
                float z = EditorGUILayout.Slider("Z ひねり", finger.angle.z, -60f, 60f);
                var joints = new Vector3[3];
                for (int j = 0; j < 3; j++) joints[j] = finger.Joint(j);
                if (open)
                    for (int j = 0; j < 3; j++)
                        joints[j] = EditorGUILayout.Vector3Field(JointNames[j], joints[j]);
                EditorGUI.indentLevel--;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(pose, "指の角度を変更");
                    finger.angle = new Vector3(x, y, z);
                    finger.joints = joints;
                    EditorUtility.SetDirty(pose);
                }
            }
        }

        /// 再生していないときは、選んだ武器を構えた姿勢にする（再生中はゲームのIKがそのまま反映する）
        void Preview()
        {
            if (rig == null || Application.isPlaying) return;
            rig.PreviewGrip(weaponIndex);
            SceneView.RepaintAll();
        }

        void OnSceneGUI(SceneView view)
        {
            var r = FindRig();
            if (r == null || r.rifle == null || weaponIndex >= r.weaponVisuals.Length) return;
            var grip = r.weaponVisuals[weaponIndex].grip;
            if (grip == null) return;

            bool changed = false;
            if (editRight) changed |= HandHandle(r.rifle, grip, grip.right, new Color(1f, 0.4f, 0.3f), "右手");
            if (editLeft) changed |= HandHandle(r.rifle, grip, grip.left, new Color(0.3f, 0.7f, 1f), "左手");
            if (changed)
            {
                Preview();
                Repaint();
            }
        }

        static bool HandHandle(Transform rifle, GripPose pose, GripPose.HandGrip g, Color color, string label)
        {
            if (!g.holding) return false;
            Vector3 pos = rifle.TransformPoint(g.position);
            Quaternion rot = rifle.rotation * Quaternion.Euler(g.rotation);
            Handles.color = color;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.025f, EventType.Repaint);
            Handles.Label(pos + Vector3.up * 0.04f, label);

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(pos, Tools.pivotRotation == PivotRotation.Local ? rot : Quaternion.identity);
            Quaternion newRot = Handles.RotationHandle(rot, pos);
            if (!EditorGUI.EndChangeCheck()) return false;

            Undo.RecordObject(pose, "握り方を変更");
            g.position = rifle.InverseTransformPoint(newPos);
            g.rotation = (Quaternion.Inverse(rifle.rotation) * newRot).eulerAngles;
            EditorUtility.SetDirty(pose);
            return true;
        }
    }
}
