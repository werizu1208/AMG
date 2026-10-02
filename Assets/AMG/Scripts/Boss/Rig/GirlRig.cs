using UnityEngine;

namespace AMG
{
    /// フェーズ1の少女の姿。基本形状の骨を二関節IKで動かすプロシージャルアニメーション。
    /// ・足は地面に接地し、体が離れると一歩ずつ踏み出す
    /// ・ハンマー攻撃のタメ中、振り上げる右腕から倒木が生えてくる。叩きつけた後は枯れて縮む
    /// ・左手で詠唱（根・葉・召喚）、首は人形のようにカクッと傾ぐ
    public class GirlRig : BossRigBase
    {
        [Header("骨")]
        public Transform hips;
        public Transform spine;
        public Transform head;
        public Transform legLUpper, legLLower, footL;
        public Transform legRUpper, legRLower, footR;
        public Transform armLUpper, armLLower, handL;
        public Transform armRUpper, armRLower, handR;

        [Header("倒木（ハンマーのタメ中だけ右腕から生える）")]
        public Transform log;
        public float logLength = 6f;
        public float logWitherTime = 0.5f;   // 叩きつけた後、枯れて縮むまでの時間

        [Header("歩行")]
        public float hipHeight = 0.85f;
        public float footSpacing = 0.14f;
        public float stepThreshold = 0.45f;
        public float stepDuration = 0.3f;
        public float stepHeight = 0.22f;
        public float stepLead = 0.25f;

        [Header("ぎこちなさ")]
        public float headRollMax = 28f;
        public float headYawLimit = 75f;

        public override Vector3 CastPoint => handL.position;

        readonly Vector3[] planted = new Vector3[2];
        readonly Vector3[] stepFrom = new Vector3[2];
        readonly Vector3[] stepTo = new Vector3[2];
        readonly float[] stepT = { 1f, 1f };
        readonly bool[] stepping = new bool[2];

        Vector3 lastPos;
        Vector3 velocity;
        float crouch;
        float headRoll, headRollTarget, nextHeadSnap;
        Quaternion spineRot = Quaternion.identity;
        Vector3 leftHandTarget;
        float logGrowth;
        Vector3 lastGripLocal;
        Vector3 lastDirLocal = Vector3.down;

        float S => transform.lossyScale.y;
        bool IsBurrowing => Action == BossAction.BurrowDown || Action == BossAction.Underground;

        /// 再生していないとき、シーンで立ち姿に見えるように骨を一度だけ整える（実行中はIKが上書きする）
        public void ApplyEditorPose()
        {
            hips.localPosition = new Vector3(0f, hipHeight, 0f);
            spine.localRotation = Quaternion.identity;
            for (int i = 0; i < 2; i++)
            {
                planted[i] = GroundPoint(FootHome(i));
                stepping[i] = false;
            }
            leftHandTarget = handL.position;
            Vector3 grip = UpdateLog(0f);   // 倒木は非表示、右手は垂れ下がる位置
            UpdateArms(grip, 1f);
            UpdateLegs();
            head.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        }

        void Start()
        {
            for (int i = 0; i < 2; i++) planted[i] = GroundPoint(FootHome(i));
            lastPos = transform.position;
            leftHandTarget = handL.position;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            velocity = Vector3.Lerp(velocity, (transform.position - lastPos) / dt, 10f * dt);
            lastPos = transform.position;

            UpdateFeet(dt);
            UpdateBody(dt);
            Vector3 grip = UpdateLog(dt);
            UpdateArms(grip, dt);
            UpdateLegs();
            UpdateHead(dt);
        }

        // ---------- 足：接地と踏み出し ----------

        Vector3 FootHome(int i) => transform.TransformPoint(new Vector3(i == 0 ? -footSpacing : footSpacing, 0f, 0f));

        Vector3 GroundPoint(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 6f, Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
                return hit.point;
            p.y = 0f;
            return p;
        }

        void UpdateFeet(float dt)
        {
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

            if (Action == BossAction.Transform || Action == BossAction.Dead) return;

            // 地中に潜っている間は、足を地面ではなく体の高さに置く（脚が地上へ伸びないように）
            if (IsBurrowing)
            {
                for (int i = 0; i < 2; i++)
                {
                    stepping[i] = false;
                    planted[i] = FootHome(i);
                }
                return;
            }

            // 片足ずつ。体から一番離れた足から踏み出す
            if (stepping[0] || stepping[1]) return;
            int best = -1;
            float bestDist = stepThreshold * S;
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
            if (!stepping[i]) return planted[i];
            float t = Smooth(stepT[i]);
            return Vector3.Lerp(stepFrom[i], stepTo[i], t) + Vector3.up * Mathf.Sin(stepT[i] * Mathf.PI) * stepHeight * S;
        }

        void UpdateLegs()
        {
            Vector3 fwd = transform.forward;
            Vector3 ankle = Vector3.up * 0.05f * S;
            SolveLeg(legLUpper, legLLower, footL, FootPosition(0) + ankle, fwd);
            SolveLeg(legRUpper, legRLower, footR, FootPosition(1) + ankle, fwd);
        }

        void SolveLeg(Transform upper, Transform lower, Transform foot, Vector3 target, Vector3 fwd)
        {
            // 膝は前に曲げる
            TwoBoneIK.Solve(upper, lower, foot, target, upper.position + fwd * S + Vector3.down * 0.3f * S);
            foot.rotation = Quaternion.LookRotation(Flat(fwd).normalized, Vector3.up);
        }

        // ---------- 胴体 ----------

        void UpdateBody(float dt)
        {
            float targetCrouch = Action switch
            {
                BossAction.RootCast => 0.32f,
                BossAction.Revive => 0.28f,
                BossAction.HammerSlam => 0.2f,
                BossAction.MeleeWindup => 0.12f,
                BossAction.BurrowDown => 0.35f,
                BossAction.Underground => 0.35f,
                BossAction.Dead => 0.5f,
                _ => 0f,
            };
            crouch = Mathf.Lerp(crouch, targetCrouch, 8f * dt);

            float bob = 0f;
            for (int i = 0; i < 2; i++)
                if (stepping[i]) bob = Mathf.Sin(stepT[i] * Mathf.PI) * 0.05f;
            hips.localPosition = new Vector3(0f, hipHeight - crouch - bob, 0f);

            Vector3 localVel = transform.InverseTransformDirection(velocity) / Mathf.Max(0.01f, S);
            float lean = Mathf.Clamp(localVel.z * 4f, -10f, 15f);
            float side = Mathf.Clamp(-localVel.x * 4f, -10f, 10f);
            float twist = 0f;
            switch (Action)
            {
                // 格闘：上半身を左へひねって溜め、右へ振り抜く
                case BossAction.MeleeWindup: twist = -35f * Smooth(Progress); lean -= 5f; break;
                case BossAction.MeleeStrike: twist = Mathf.Lerp(-35f, 40f, Progress); lean += 15f * Progress; break;
                case BossAction.BurrowDown:
                case BossAction.Underground: lean += 55f; break;
                case BossAction.Emerge: lean -= 20f * (1f - Progress); break;
                case BossAction.RootCast: lean += 35f; break;
                case BossAction.HammerWindup: lean -= 18f * Smooth(Progress); break;
                case BossAction.HammerSlam: lean += 30f * Progress; break;
                case BossAction.Revive: lean += 40f; side += Mathf.Sin(Time.time * 40f) * 4f; break;
                case BossAction.Dead: lean += 60f; break;
            }
            Quaternion target = Quaternion.Euler(lean, twist, side);
            if (Action == BossAction.Transform)
                target = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(-30f, 30f), Random.Range(-30f, 30f));
            float follow = Action == BossAction.Transform || Action == BossAction.MeleeStrike ? 30f : 10f;
            spineRot = Quaternion.Slerp(spineRot, target, follow * dt);
            spine.localRotation = spineRot;
        }

        // ---------- 倒木：引きずる → 振り上げる → 叩きつける ----------

        Vector3 UpdateLog(float dt)
        {
            float s = S;
            Vector3 up = Vector3.up;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            Vector3 pos = transform.position;

            // 普段の右手は左手と同じく力なく垂れ下がる。倒木はその腕の延長（下向き）から生え始める
            Vector3 hangGrip = hips.position + (right * 0.3f - up * 0.4f + fwd * 0.05f * Mathf.Sin(Time.time * 1.7f + 1.5f)) * s;
            Vector3 hangDir = (-up * 0.9f + fwd * 0.25f).normalized;

            Vector3 hFwd = hammerDir.sqrMagnitude > 0.01f ? Flat(hammerDir).normalized : fwd;
            Vector3 overGrip = pos + (up * 2.0f + hFwd * 0.1f) * s;
            Vector3 overDir = (up * 0.85f - hFwd * 0.5f).normalized;
            float h = 1.1f;
            Vector3 slamGrip = pos + (hFwd * 0.5f + up * h) * s;
            Vector3 slamDir = (hFwd * Mathf.Sqrt(Mathf.Max(0.01f, logLength * logLength - h * h)) - up * h).normalized;

            Vector3 grip;
            Vector3 dir;
            switch (Action)
            {
                case BossAction.HammerWindup:
                {
                    // 腕を振り上げながら、腕の延長として倒木が生えてくる（タメの7割で生え切る）
                    float t = Smooth(Progress);
                    grip = Vector3.Lerp(hangGrip, overGrip, t);
                    dir = Vector3.Slerp(hangDir, overDir, t);
                    logGrowth = Smooth(Mathf.Clamp01(Progress / 0.7f));
                    RememberLogPose(grip, dir);
                    break;
                }
                case BossAction.HammerSlam:
                {
                    float t = Progress * Progress;
                    grip = Vector3.Lerp(overGrip, slamGrip, t);
                    dir = Vector3.Slerp(overDir, slamDir, t);
                    logGrowth = 1f;
                    RememberLogPose(grip, dir);
                    break;
                }
                default:
                    if (logGrowth > 0f)
                    {
                        // 叩きつけた（または中断された）姿勢のまま、枯れて縮んでいく
                        logGrowth = Mathf.MoveTowards(logGrowth, 0f, dt / logWitherTime);
                        grip = transform.TransformPoint(lastGripLocal);
                        dir = transform.TransformDirection(lastDirLocal);
                    }
                    else
                    {
                        grip = hangGrip;
                        dir = hangDir;
                    }
                    break;
            }

            bool visible = logGrowth > 0.01f;
            if (log.gameObject.activeSelf != visible) log.gameObject.SetActive(visible);
            if (visible)
            {
                // 長さ（+Z）は生え具合、太さは生え始めほど細い
                float thick = Mathf.Lerp(0.35f, 1f, logGrowth);
                log.localScale = new Vector3(thick, thick, logGrowth);
                log.SetPositionAndRotation(grip, Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.95f ? fwd : up));
            }
            return grip;
        }

        void RememberLogPose(Vector3 grip, Vector3 dir)
        {
            lastGripLocal = transform.InverseTransformPoint(grip);
            lastDirLocal = transform.InverseTransformDirection(dir);
        }

        // ---------- 腕 ----------

        void UpdateArms(Vector3 grip, float dt)
        {
            float s = S;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            Vector3 up = Vector3.up;
            Vector3 chest = armLUpper.parent.position;

            // 右手：普段は垂れ下がり、ハンマー時は倒木の根元になる
            TwoBoneIK.Solve(armRUpper, armRLower, handR, grip, chest + (right * 0.6f - fwd * 0.6f - up * 0.4f) * s);

            // 左手：詠唱
            Vector3 target;
            bool snap = false;
            switch (Action)
            {
                case BossAction.HammerWindup:
                case BossAction.HammerSlam:
                    target = grip + log.forward * 0.4f * s * logGrowth;
                    snap = true;
                    break;
                case BossAction.MeleeWindup:
                    target = chest + (-right * 0.55f - fwd * 0.35f + up * 0.25f) * s;
                    break;
                case BossAction.MeleeStrike:
                {
                    // 横薙ぎ：左後方から右前方へ一気に振り抜く
                    Vector3 from = chest + (-right * 0.55f - fwd * 0.35f + up * 0.25f) * s;
                    Vector3 to = chest + (fwd * 0.8f + right * 0.3f) * s;
                    target = Vector3.Lerp(from, to, 1f - (1f - Progress) * (1f - Progress));
                    snap = true;
                    break;
                }
                case BossAction.BurrowDown:
                case BossAction.Underground:
                case BossAction.RootCast:
                    target = transform.position + (fwd * 0.7f - right * 0.2f + up * 0.3f) * s;
                    break;
                case BossAction.Emerge:
                    target = chest + (up * 0.7f - right * 0.2f + fwd * 0.1f) * s;
                    break;
                case BossAction.LeafCast:
                    target = chest + (PlayerPos + up * 1.2f - chest).normalized * 0.6f * s;
                    break;
                case BossAction.Summon:
                    target = chest + (-right * 0.35f + up * 0.55f + fwd * 0.1f) * s;
                    break;
                case BossAction.Revive:
                    target = chest + (fwd * 0.2f + right * 0.05f - up * 0.05f) * s;
                    break;
                case BossAction.Transform:
                    target = chest + Random.insideUnitSphere * 0.6f * s;
                    snap = true;
                    break;
                default:
                    // 力なく垂れ下がって揺れる
                    target = hips.position + (-right * 0.3f - up * 0.4f + fwd * 0.05f * Mathf.Sin(Time.time * 1.7f)) * s;
                    break;
            }
            leftHandTarget = snap ? target : Vector3.Lerp(leftHandTarget, target, 10f * dt);
            TwoBoneIK.Solve(armLUpper, armLLower, handL, leftHandTarget, chest + (-right * 0.6f - fwd * 0.6f - up * 0.4f) * s);
        }

        // ---------- 首 ----------

        void UpdateHead(float dt)
        {
            Vector3 look = PlayerPos + Vector3.up * 1.2f - head.position;
            Vector3 dir = ClampLook(transform.forward, look, headYawLimit);
            float roll = JerkyRoll(ref headRoll, ref headRollTarget, ref nextHeadSnap, headRollMax, dt);
            if (Action == BossAction.Transform) roll = Random.Range(-60f, 60f);
            if (Action == BossAction.Dead) dir = (transform.forward - Vector3.up * 1.5f).normalized;
            head.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
