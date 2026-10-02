using UnityEngine;

namespace AMG
{
    /// 骨（Transform）ごとの見た目の差し替え口。
    /// replacement に本番モデル（プレハブやFBX）を入れると、仮の形状（placeholders）を隠して差し替える。
    /// IKは骨を動かすだけなので、見た目を差し替えてもアニメーションはそのまま動く。
    /// 再生していないときもシーンにプレビューを表示する（プレビューはシーンには保存されない）。
    /// 再生中に調整した値は、再生を止めても残る（VisualSlotPlayModePersistence）
    [ExecuteAlways]
    public class VisualSlot : MonoBehaviour
    {
        [Tooltip("本番モデル（プレハブ / FBX）。空なら仮の形状のまま")]
        public GameObject replacement;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;
        [Tooltip("マイナスにすると反転（左右で同じモデルを使うときなど）")]
        public Vector3 scale = Vector3.one;

        [Tooltip("差し替えたときに隠す仮の形状")]
        public GameObject[] placeholders = new GameObject[0];

        GameObject instance;

        /// 差し替えたモデル（差し替えていなければ null）
        public GameObject Instance => instance;

        void OnEnable()
        {
            Apply();
        }

        void OnDisable()
        {
            if (!Application.isPlaying) RemovePreview();
        }

        public void Apply()
        {
            if (instance != null) return;
            if (replacement == null)
            {
                SetPlaceholders(true);
                return;
            }

            instance = Instantiate(replacement, transform);
            instance.name = replacement.name;
            ApplyOffset();
            Prim.SetLayerRecursive(instance, gameObject.layer);
            // 当たり判定はボス側（胴体のカプセル・弱点）の仕組みを使うので、モデルのコライダーは外す
            foreach (var col in instance.GetComponentsInChildren<Collider>())
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            // 編集中のプレビューはシーンに保存しない・選択もさせない
            if (!Application.isPlaying)
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                    t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            SetPlaceholders(false);
        }

        void RemovePreview()
        {
            if (instance != null) DestroyImmediate(instance);
            instance = null;
        }

        void SetPlaceholders(bool visible)
        {
            foreach (var p in placeholders)
                if (p != null && p.activeSelf != visible) p.SetActive(visible);
        }

        void ApplyOffset()
        {
            instance.transform.localPosition = positionOffset;
            instance.transform.localRotation = Quaternion.Euler(rotationOffset);
            instance.transform.localScale = scale;
        }

#if UNITY_EDITOR
        // インスペクターで値を変えたらすぐ反映する（モデルの差し替えは次のエディタ更新で作り直す）
        void OnValidate()
        {
            if (Application.isPlaying)
            {
                if (instance != null) ApplyOffset();
                return;
            }
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                if (instance != null && instance.name != (replacement != null ? replacement.name : null)) RemovePreview();
                if (instance == null) Apply();
                else ApplyOffset();
            };
        }
#endif
    }
}
