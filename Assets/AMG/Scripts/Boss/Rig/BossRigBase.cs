using UnityEngine;

namespace AMG
{
    public enum BossAction { None, RootCast, HammerWindup, HammerSlam, LeafCast, Summon, Revive, Transform, Dead }

    /// ボスのプロシージャルアニメーションの共通部分。
    /// BossAttacks / BossController が Play と Progress で「今何をしているか」を伝え、リグがIKで体を動かす
    public abstract class BossRigBase : MonoBehaviour
    {
        public BossAction Action { get; private set; }
        /// 現在のアクションの進行度（0〜1）
        [HideInInspector] public float Progress;

        [HideInInspector] public Vector3 hammerOrigin;
        [HideInInspector] public Vector3 hammerDir = Vector3.forward;
        [HideInInspector] public float hammerLength = 7f;

        /// 葉の刃などの発射位置
        public abstract Vector3 CastPoint { get; }

        public void Play(BossAction action)
        {
            Action = action;
            Progress = 0f;
        }

        public void SetHammer(Vector3 origin, Vector3 dir, float length)
        {
            hammerOrigin = origin;
            hammerDir = dir;
            hammerLength = length;
        }

        protected Vector3 PlayerPos =>
            PlayerHealth.I != null ? PlayerHealth.I.transform.position : transform.position + transform.forward * 5f;

        protected static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        protected static float Smooth(float t) => t * t * (3f - 2f * t);

        /// 人形のようにカクッと傾く首の角度を作る
        protected float JerkyRoll(ref float current, ref float targetRoll, ref float nextSnap, float maxRoll, float dt)
        {
            if (Time.time >= nextSnap)
            {
                targetRoll = Random.value < 0.3f ? 0f : Random.Range(-maxRoll, maxRoll);
                nextSnap = Time.time + Random.Range(0.6f, 2.2f);
            }
            current = Mathf.MoveTowards(current, targetRoll, 400f * dt);
            return current;
        }

        /// 体の正面から一定角度以内に収めた視線方向
        protected static Vector3 ClampLook(Vector3 bodyForward, Vector3 desired, float maxAngle)
        {
            Vector3 flatDesired = Flat(desired);
            Vector3 flatBody = Flat(bodyForward);
            if (flatDesired.sqrMagnitude < 1e-4f || flatBody.sqrMagnitude < 1e-4f) return bodyForward;
            float angle = Vector3.SignedAngle(flatBody, flatDesired, Vector3.up);
            float clamped = Mathf.Clamp(angle, -maxAngle, maxAngle);
            Vector3 dir = Quaternion.AngleAxis(clamped - angle, Vector3.up) * desired;
            return dir.normalized;
        }
    }
}
