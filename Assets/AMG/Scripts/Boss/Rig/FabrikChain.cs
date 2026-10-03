using UnityEngine;

namespace AMG
{
    /// FABRIK による多関節IK（巨木の根の腕・地面の根用）。
    /// joints はすべてこのオブジェクトの直下に並べる。joints[0] が根元（固定）、最後が先端。
    public class FabrikChain : MonoBehaviour
    {
        public Transform[] joints;
        public int iterations = 10;
        public float tolerance = 0.02f;
        /// 毎フレーム中間の関節を持ち上げ、根が折れ曲がらずに弧を描くようにする
        public float archBias = 0.15f;

        [HideInInspector] public Vector3 target;

        public Vector3 Tip => joints[joints.Length - 1].position;
        public Vector3 Base => joints[0].position;
        public float TotalLength => totalLocalLength * transform.lossyScale.y;

        float[] localLengths;
        float totalLocalLength;
        Vector3[] p;

        void Awake()
        {
            localLengths = new float[joints.Length - 1];
            for (int i = 0; i < localLengths.Length; i++)
            {
                localLengths[i] = Vector3.Distance(joints[i].localPosition, joints[i + 1].localPosition);
                totalLocalLength += localLengths[i];
            }
            p = new Vector3[joints.Length];
            target = Tip;
        }

        public void Solve()
        {
            int n = joints.Length;
            float s = transform.lossyScale.y;
            for (int i = 0; i < n; i++) p[i] = joints[i].position;
            for (int i = 1; i < n - 1; i++) p[i] += Vector3.up * archBias * s;

            Vector3 basePos = p[0];
            if (Vector3.Distance(basePos, target) >= totalLocalLength * s)
            {
                // 届かない：まっすぐ伸ばす
                Vector3 dir = (target - basePos).normalized;
                for (int i = 1; i < n; i++) p[i] = p[i - 1] + dir * localLengths[i - 1] * s;
            }
            else
            {
                for (int iter = 0; iter < iterations; iter++)
                {
                    p[n - 1] = target;
                    for (int i = n - 2; i >= 0; i--)
                        p[i] = p[i + 1] + (p[i] - p[i + 1]).normalized * localLengths[i] * s;
                    p[0] = basePos;
                    for (int i = 1; i < n; i++)
                        p[i] = p[i - 1] + (p[i] - p[i - 1]).normalized * localLengths[i - 1] * s;
                    if ((p[n - 1] - target).sqrMagnitude < tolerance * tolerance) break;
                }
            }

            for (int i = 0; i < n; i++) joints[i].position = p[i];
            RefreshRotations();
        }

        /// 関節の位置に合わせて、各関節を次の関節の方向へ向け直す（位置を後から動かしたとき用）
        public void RefreshRotations()
        {
            int n = joints.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 fwd = i < n - 1 ? joints[i + 1].position - joints[i].position : joints[i].position - joints[i - 1].position;
                if (fwd.sqrMagnitude > 1e-6f)
                {
                    Vector3 up = Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
                    joints[i].rotation = Quaternion.LookRotation(fwd, up);
                }
            }
        }
    }
}
