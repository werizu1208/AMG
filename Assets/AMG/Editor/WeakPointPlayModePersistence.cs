using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AMG.EditorTools
{
    /// 再生中に調整したボスの弱点（位置・大きさ・倍率）を、再生を止めても残す。
    /// 骨の割り当ては再生中に変えても残さない（シーンの値のまま）。シーンは「未保存」になるので Ctrl+S で保存
    [InitializeOnLoad]
    static class WeakPointPlayModePersistence
    {
        struct Saved
        {
            public Vector3 girlOffset, treeOffset;
            public float girlRadius, treeRadius, multiplier;
        }

        static readonly Dictionary<string, Saved> pending = new Dictionary<string, Saved>();

        static WeakPointPlayModePersistence()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingPlayMode) Capture();
                else if (change == PlayModeStateChange.EnteredEditMode) Restore();
            };
        }

        static string Key(BossController b) => b.gameObject.scene.path + "/" + b.name;

        static void Capture()
        {
            pending.Clear();
            foreach (var b in Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                pending[Key(b)] = new Saved
                {
                    girlOffset = b.girlWeakPoint.offset, girlRadius = b.girlWeakPoint.radius,
                    treeOffset = b.treeWeakPoint.offset, treeRadius = b.treeWeakPoint.radius,
                    multiplier = b.weakPointMultiplier,
                };
        }

        static void Restore()
        {
            if (pending.Count == 0) return;
            foreach (var b in Object.FindObjectsByType<BossController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!pending.TryGetValue(Key(b), out var s)) continue;
                bool same = b.girlWeakPoint.offset == s.girlOffset && Mathf.Approximately(b.girlWeakPoint.radius, s.girlRadius)
                    && b.treeWeakPoint.offset == s.treeOffset && Mathf.Approximately(b.treeWeakPoint.radius, s.treeRadius)
                    && Mathf.Approximately(b.weakPointMultiplier, s.multiplier);
                if (same) continue;

                Undo.RecordObject(b, "再生中の弱点の値を保持");
                b.girlWeakPoint.offset = s.girlOffset; b.girlWeakPoint.radius = s.girlRadius;
                b.treeWeakPoint.offset = s.treeOffset; b.treeWeakPoint.radius = s.treeRadius;
                b.weakPointMultiplier = s.multiplier;
                EditorUtility.SetDirty(b);
                EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
                Debug.Log("[A・M・G] 再生中に調整したボスの弱点の値を残しました。Ctrl+S でシーンを保存してください。");
            }
            pending.Clear();
        }
    }
}
