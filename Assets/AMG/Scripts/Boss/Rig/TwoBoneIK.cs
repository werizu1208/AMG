using UnityEngine;

namespace AMG
{
    /// 解析的な二関節IK（腕・脚用）。
    /// 規約：骨はローカル+Z方向に伸び、lower は upper のローカル+Z上、end は lower のローカル+Z上にある。
    public static class TwoBoneIK
    {
        /// pole は関節（肘・膝）を曲げたい方向にある点
        public static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 root = upper.position;
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, end.position);
            if (a < 1e-5f || b < 1e-5f) return;

            Vector3 toTarget = target - root;
            float d = toTarget.magnitude;
            if (d < 1e-5f) return;
            Vector3 dir = toTarget / d;
            d = Mathf.Clamp(d, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);

            // 余弦定理で根元の角度を求める
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);

            Vector3 bend = Vector3.ProjectOnPlane(pole - root, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.forward, dir);
            if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.right, dir);
            bend.Normalize();

            Vector3 mid = root + dir * (a * cosA) + bend * (a * sinA);
            Vector3 endPos = root + dir * d;
            upper.rotation = Quaternion.LookRotation(mid - root, bend);
            lower.rotation = Quaternion.LookRotation(endPos - mid, bend);
        }
    }
}
