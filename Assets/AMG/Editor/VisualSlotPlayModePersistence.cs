using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AMG.EditorTools
{
    /// 再生中に調整した VisualSlot の位置・向き・大きさを、再生を止めても残す。
    /// 再生終了の直前に値を記録し、編集モードに戻ったら同じ骨の VisualSlot に書き戻す（シーンは「未保存」になるので Ctrl+S で保存）。
    /// 差し替えたモデル本体の Transform を直接動かした場合も、その値を VisualSlot に反映する
    [InitializeOnLoad]
    static class VisualSlotPlayModePersistence
    {
        struct Saved
        {
            public Vector3 position, rotation, scale;
        }

        static readonly Dictionary<string, Saved> pending = new Dictionary<string, Saved>();

        static VisualSlotPlayModePersistence()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Capture();
            else if (change == PlayModeStateChange.EnteredEditMode) Restore();
        }

        static void Capture()
        {
            pending.Clear();
            foreach (var slot in Object.FindObjectsByType<VisualSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Saved s;
                var inst = slot.Instance;
                if (inst != null)
                {
                    // モデル本体の Transform が最終的な見た目なので、こちらを正とする
                    s.position = inst.transform.localPosition;
                    s.rotation = inst.transform.localEulerAngles;
                    s.scale = inst.transform.localScale;
                }
                else
                {
                    s.position = slot.positionOffset;
                    s.rotation = slot.rotationOffset;
                    s.scale = slot.scale;
                }
                pending[PathOf(slot.transform)] = s;
            }
        }

        static void Restore()
        {
            if (pending.Count == 0) return;
            var changed = new List<string>();
            var dirtyScenes = new HashSet<Scene>();

            foreach (var slot in Object.FindObjectsByType<VisualSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!pending.TryGetValue(PathOf(slot.transform), out var s)) continue;
                if (!Differs(slot.positionOffset, s.position) && !RotationDiffers(slot.rotationOffset, s.rotation) && !Differs(slot.scale, s.scale))
                    continue;

                Undo.RecordObject(slot, "再生中の VisualSlot の値を保持");
                slot.positionOffset = Round(s.position);
                slot.rotationOffset = Round(Normalize(s.rotation));
                slot.scale = Round(s.scale);
                EditorUtility.SetDirty(slot);
                dirtyScenes.Add(slot.gameObject.scene);
                changed.Add(slot.name);
            }
            pending.Clear();

            foreach (var scene in dirtyScenes) EditorSceneManager.MarkSceneDirty(scene);
            if (changed.Count > 0)
                Debug.Log($"[A・M・G] 再生中に調整した見た目を {changed.Count} か所に残しました（{string.Join(", ", changed)}）。Ctrl+S でシーンを保存してください。");
        }

        /// シーン内での位置（親の名前と兄弟の順番）で VisualSlot を特定する
        static string PathOf(Transform t)
        {
            var sb = new StringBuilder();
            for (var p = t; p != null; p = p.parent) sb.Insert(0, "/" + p.GetSiblingIndex() + ":" + p.name);
            return t.gameObject.scene.path + sb;
        }

        static bool Differs(Vector3 a, Vector3 b) => (a - b).sqrMagnitude > 1e-8f;

        static bool RotationDiffers(Vector3 a, Vector3 b) => Quaternion.Angle(Quaternion.Euler(a), Quaternion.Euler(b)) > 0.01f;

        /// 0〜360 の角度を -180〜180 にそろえる（インスペクターと同じ見え方にする）
        static Vector3 Normalize(Vector3 e) => new Vector3(Mathf.DeltaAngle(0f, e.x), Mathf.DeltaAngle(0f, e.y), Mathf.DeltaAngle(0f, e.z));

        /// 浮動小数の誤差（0.0299999 など）を丸める
        static Vector3 Round(Vector3 v) => new Vector3(Round(v.x), Round(v.y), Round(v.z));
        static float Round(float f) => Mathf.Round(f * 10000f) / 10000f;
    }
}
