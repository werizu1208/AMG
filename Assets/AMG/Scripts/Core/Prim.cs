using UnityEngine;

namespace AMG
{
    /// 基本形状（プリミティブ）で仮モデルや骨を組み立てるヘルパー。エディタとランタイムの両方で使う
    public static class Prim
    {
        public static GameObject Create(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 localScale, Material mat, bool keepCollider = false, Quaternion? localRot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.layer = parent.gameObject.layer;
            }
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static Transform Empty(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.layer = parent.gameObject.layer;
            }
            go.transform.localPosition = localPos;
            return go.transform;
        }

        /// 骨の見た目。骨はローカル+Z方向に伸びる前提（IKの規約）
        public static GameObject Bone(Transform bone, float length, float thickness, Material mat)
        {
            return Create(PrimitiveType.Cylinder, bone.name + "_Mesh", bone, new Vector3(0f, 0f, length * 0.5f),
                new Vector3(thickness, length * 0.5f, thickness), mat, false, Quaternion.Euler(90f, 0f, 0f));
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }
    }
}
