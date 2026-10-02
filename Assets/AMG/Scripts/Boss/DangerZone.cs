using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// ボスの攻撃予兆（赤い円・矩形）の範囲。プレイヤーが中にいるかをHUDの危険表示に使う。
    /// 予兆のオブジェクトに付け、オブジェクトが消えると自動で登録が外れる
    public class DangerZone : MonoBehaviour
    {
        static readonly List<DangerZone> active = new List<DangerZone>();

        bool isCircle;
        Vector3 origin;
        Quaternion rotation;
        float radius;
        float width;
        float length;

        public static void AddCircle(GameObject go, Vector3 center, float radius)
        {
            var zone = go.AddComponent<DangerZone>();
            zone.isCircle = true;
            zone.origin = center;
            zone.radius = radius;
        }

        /// origin から rotation の前方へ length、左右に width の矩形
        public static void AddRect(GameObject go, Vector3 origin, Quaternion rotation, float width, float length)
        {
            var zone = go.AddComponent<DangerZone>();
            zone.origin = origin;
            zone.rotation = rotation;
            zone.width = width;
            zone.length = length;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        bool Contains(Vector3 p)
        {
            Vector3 d = p - origin;
            d.y = 0f;
            if (isCircle) return d.magnitude <= radius;
            Vector3 rel = Quaternion.Inverse(rotation) * d;
            return rel.z >= -0.5f && rel.z <= length && Mathf.Abs(rel.x) <= width * 0.5f;
        }

        /// どれかの予兆範囲の中にいるか
        public static bool IsInside(Vector3 position)
        {
            foreach (var zone in active)
                if (zone.Contains(position)) return true;
            return false;
        }
    }
}
