using UnityEngine;

namespace AMG
{
    /// プレイヤー（特殊部隊員）のプロシージャルアニメーション。ボスと同じく、基本形状の骨を二関節IKで動かす。
    /// ・足は地面に接地し、移動に合わせて一歩ずつ踏み出す。ジャンプ中は足を引き上げる
    /// ・両手は小銃をIKで保持する。構え中（エイム・射撃直後）は画面中央の照準点へ銃口を向け、
    ///   普段は銃口を下げたローレディ姿勢、ダッシュ中は銃を体に引き寄せる
    /// ・上半身は照準の上下に合わせて傾き、回避中はかがむ
    [DefaultExecutionOrder(50)] // プレイヤーの移動とカメラの後に姿勢を決める
    public class PlayerRig : MonoBehaviour
    {
        [Header("骨")]
        public Transform hips;
        public Transform spine;
        public Transform chest;
        public Transform head;
        public Transform legLUpper, legLLower, footL;
        public Transform legRUpper, legRLower, footR;
        public Transform armLUpper, armLLower, handL;
        public Transform armRUpper, armRLower, handR;

        [Header("小銃（原点＝床尾。+Z が銃口方向）")]
        public Transform rifle;
        public float rightHandOnRifle = 0.24f;   // 床尾から右手（グリップ）まで
        public float leftHandOnRifle = 0.50f;    // 床尾から左手（ハンドガード）まで

        [Header("歩行")]
        public float hipHeight = 0.95f;
        public float footSpacing = 0.12f;
        public float stepThreshold = 0.3f;
        public float stepDuration = 0.2f;
        public float stepHeight = 0.14f;
        public float stepLead = 0.12f;

        [Header("姿勢")]
        public float headYawLimit = 70f;
        [Range(0f, 1f)] public float spinePitchFollow = 0.5f;   // 照準の上下を上半身がどれだけ追うか

        PlayerController controller;
        PlayerHealth health;

        readonly Vector3[] planted = new Vector3[2];
        readonly Vector3[] stepFrom = new Vector3[2];
        readonly Vector3[] stepTo = new Vector3[2];
        readonly float[] stepT = { 1f, 1f };
        readonly bool[] stepping = new bool[2];

        Vector3 lastPos;
        Vector3 velocity;
        float crouch;
        Quaternion spineRot = Quaternion.identity;
        Vector3 aimDir;
        float airTuck;

        Transform Body => controller != null ? controller.transform : transform;

        void Start()
        {
            controller = GetComponentInParent<PlayerController>();
            health = GetComponentInParent<PlayerHealth>();
            for (int i = 0; i < 2; i++) planted[i] = GroundPoint(FootHome(i));
            lastPos = Body.position;
            aimDir = Body.forward;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || controller == null) return;
            if (health != null && !health.IsAlive) return;   // 倒れたら姿勢を止める

            velocity = Vector3.Lerp(velocity, (Body.position - lastPos) / dt, 12f * dt);
            lastPos = Body.position;

            UpdateFeet(dt);
            UpdateBody(dt);
            UpdateRifle(dt);
            UpdateArms();
            UpdateLegs();
            UpdateHead();
        }

        /// 再生していないとき、シーンで立ち姿に見えるように骨を一度だけ整える
        public void ApplyEditorPose()
        {
            controller = GetComponentInParent<PlayerController>();
            hips.localPosition = new Vector3(0f, hipHeight, 0f);
            spine.localRotation = Quaternion.identity;
            for (int i = 0; i < 2; i++)
            {
                planted[i] = GroundPoint(FootHome(i));
                stepping[i] = false;
            }
            aimDir = LowReadyDirection();
            PlaceRifle(aimDir);
            UpdateArms();
            UpdateLegs();
            head.rotation = Quaternion.LookRotation(Body.forward, Vector3.up);
        }

        // ---------- 足 ----------

        Vector3 FootHome(int i) => Body.TransformPoint(new Vector3(i == 0 ? -footSpacing : footSpacing, 0f, 0f));

        static Vector3 GroundPoint(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            p.y = Mathf.Min(p.y, 0f);
            return p;
        }

        void UpdateFeet(float dt)
        {
            bool airborne = !controller.IsGrounded && Body.position.y > GroundPoint(Body.position).y + 0.15f;
            airTuck = Mathf.MoveTowards(airTuck, airborne ? 1f : 0f, dt * 6f);

            for (int i = 0; i < 2; i++)
            {
                if (!stepping[i]) continue;
                stepT[i] += dt / stepDuration;
                if (stepT[i] >= 1f)
                {
                    stepT[i] = 1f;
                    stepping[i] = false;
                    planted[i] = stepTo[i];
                }
            }

            if (airborne)
            {
                // 空中では足を体についてこさせる（着地した場所から改めて接地する）
                for (int i = 0; i < 2; i++)
                {
                    stepping[i] = false;
                    planted[i] = FootHome(i);
                }
                return;
            }

            if (stepping[0] || stepping[1]) return;
            int best = -1;
            float bestDist = stepThreshold;
            for (int i = 0; i < 2; i++)
            {
                Vector3 desired = GroundPoint(FootHome(i) + velocity * stepLead);
                float dist = Vector3.Distance(Flat(planted[i]), Flat(desired));
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            if (best < 0) return;
            stepping[best] = true;
            stepT[best] = 0f;
            stepFrom[best] = planted[best];
            stepTo[best] = GroundPoint(FootHome(best) + velocity * stepLead);
        }

        Vector3 FootPosition(int i)
        {
            Vector3 p;
            if (!stepping[i]) p = planted[i];
            else
            {
                float t = stepT[i] * stepT[i] * (3f - 2f * stepT[i]);
                p = Vector3.Lerp(stepFrom[i], stepTo[i], t) + Vector3.up * Mathf.Sin(stepT[i] * Mathf.PI) * stepHeight;
            }
            // ジャンプ中は膝を曲げて足を引き上げる
            return Vector3.Lerp(p, FootHome(i) + Vector3.up * (0.35f + i * 0.1f), airTuck);
        }

        void UpdateLegs()
        {
            Vector3 fwd = Body.forward;
            Vector3 ankle = Vector3.up * 0.08f;
            SolveLeg(legLUpper, legLLower, footL, FootPosition(0) + ankle, fwd);
            SolveLeg(legRUpper, legRLower, footR, FootPosition(1) + ankle, fwd);
        }

        void SolveLeg(Transform upper, Transform lower, Transform foot, Vector3 target, Vector3 fwd)
        {
            TwoBoneIK.Solve(upper, lower, foot, target, upper.position + fwd + Vector3.down * 0.3f);
            foot.rotation = Quaternion.LookRotation(Flat(fwd).normalized, Vector3.up);
        }

        // ---------- 胴体 ----------

        void UpdateBody(float dt)
        {
            float targetCrouch = controller.IsDodging ? 0.28f : controller.IsAiming ? 0.05f : 0f;
            crouch = Mathf.Lerp(crouch, targetCrouch, 12f * dt);

            float bob = 0f;
            for (int i = 0; i < 2; i++)
                if (stepping[i]) bob = Mathf.Sin(stepT[i] * Mathf.PI) * 0.035f;
            hips.localPosition = new Vector3(0f, hipHeight - crouch - bob, 0f);

            // 照準の上下に合わせて上半身を傾ける（構え中のみ）
            float pitch = 0f;
            if (controller.IsCombatReady && TPSCamera.I != null)
                pitch = -Mathf.Asin(Mathf.Clamp(TPSCamera.I.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg * spinePitchFollow;

            Vector3 localVel = Body.InverseTransformDirection(velocity);
            float lean = Mathf.Clamp(localVel.z * 1.5f, -6f, 12f) + (controller.IsDodging ? 20f : 0f);
            float side = Mathf.Clamp(-localVel.x * 2f, -8f, 8f);
            Quaternion target = Quaternion.Euler(lean + pitch, 0f, side);
            if (controller.IsStunned)
                target *= Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(-6f, 6f), Random.Range(-6f, 6f));
            spineRot = Quaternion.Slerp(spineRot, target, 12f * dt);
            spine.localRotation = spineRot;
        }

        // ---------- 小銃 ----------

        Vector3 LowReadyDirection() => (Body.forward * 0.75f - Vector3.up * 0.65f - Body.right * 0.15f).normalized;

        Vector3 StockPosition() => chest.position + Body.right * 0.14f + Vector3.up * 0.04f + Body.forward * 0.06f;

        void UpdateRifle(float dt)
        {
            Vector3 desired;
            if (controller.IsCombatReady && TPSCamera.I != null)
            {
                // 画面中央の照準点へ銃口を向ける
                var cam = TPSCamera.I.transform;
                Vector3 point = Physics.Raycast(cam.position, cam.forward, out var hit, 200f, Layers.ShootMask, QueryTriggerInteraction.Ignore)
                    ? hit.point
                    : cam.position + cam.forward * 200f;
                desired = (point - StockPosition()).normalized;
            }
            else if (controller.IsSprinting)
                desired = (Body.forward * 0.35f - Vector3.up * 0.8f - Body.right * 0.45f).normalized;
            else
                desired = LowReadyDirection();

            float follow = controller.IsCombatReady ? 25f : 8f;
            aimDir = Vector3.Slerp(aimDir, desired, 1f - Mathf.Exp(-follow * dt));
            PlaceRifle(aimDir);
        }

        void PlaceRifle(Vector3 dir)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (up.sqrMagnitude < 1e-4f) up = Body.forward;
            rifle.SetPositionAndRotation(StockPosition(), Quaternion.LookRotation(dir, up));
        }

        // ---------- 腕 ----------

        void UpdateArms()
        {
            Vector3 right = Body.right;
            Vector3 down = -rifle.up;
            Vector3 rightGrip = rifle.position + rifle.forward * rightHandOnRifle + down * 0.06f;
            Vector3 leftGrip = rifle.position + rifle.forward * leftHandOnRifle + down * 0.05f;
            // 肘は下・外へ
            TwoBoneIK.Solve(armRUpper, armRLower, handR, rightGrip, chest.position + right * 0.5f + Vector3.down * 0.6f);
            TwoBoneIK.Solve(armLUpper, armLLower, handL, leftGrip, chest.position - right * 0.45f + Vector3.down * 0.6f);
        }

        // ---------- 首 ----------

        void UpdateHead()
        {
            Vector3 look = controller.IsCombatReady ? aimDir : Body.forward;
            Vector3 flatBody = Flat(Body.forward);
            Vector3 flatLook = Flat(look);
            if (flatLook.sqrMagnitude > 1e-4f)
            {
                float angle = Vector3.SignedAngle(flatBody, flatLook, Vector3.up);
                float clamped = Mathf.Clamp(angle, -headYawLimit, headYawLimit);
                look = Quaternion.AngleAxis(clamped - angle, Vector3.up) * look;
            }
            head.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
