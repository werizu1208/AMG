using UnityEngine;

namespace AMG
{
    /// フェーズ2の巨木の怪物。根の腕（FABRIK）をプロシージャルに動かす。
    /// ・左右の根の腕：振り上げて叩きつける（ハンマー）、地面に突き刺す（根）、掲げる（葉・召喚）
    /// ・地面を這う根：常にうごめく
    /// ・樹皮に残った少女の顔：プレイヤーを見つめ、カクッと傾ぐ
    public class TreeRig : BossRigBase
    {
        public Transform face;
        public FabrikChain tendrilL;
        public FabrikChain tendrilR;
        public FabrikChain[] groundRoots;

        public float faceYawLimit = 60f;
        public float headRollMax = 35f;

        [Header("待機中の根の腕のうねり")]
        [Tooltip("先端がさまよう範囲（m）")]
        public float idleWander = 2.5f;
        [Tooltip("先端がさまよう速さ")]
        public float idleWanderSpeed = 0.35f;
        [Tooltip("根の途中が横にうねる大きさ（m）")]
        public float idleWaveAmplitude = 0.7f;
        [Tooltip("うねりが根元から先端へ伝わる速さ")]
        public float idleWaveSpeed = 2.2f;
        [Tooltip("根1本あたりの波の数")]
        public float idleWaveCount = 1.5f;

        public override Vector3 CastPoint => face.position + face.forward * 1.2f * transform.lossyScale.y;

        Vector3[] rootRestDir;
        float[] rootSeeds;
        float headRoll, headRollTarget, nextHeadSnap;
        Vector3 slamStart;
        float seedL, seedR;
        float idleWeight;
        float wavePhaseL, wavePhaseR;
        // うねりを加える前の関節位置。次のフレームのIKはここから解く（揺れが積み重ならないように）
        Vector3[] unwavedL, unwavedR;

        void Start()
        {
            seedL = Random.Range(0f, 100f);
            seedR = Random.Range(0f, 100f);
            rootRestDir = new Vector3[groundRoots.Length];
            rootSeeds = new float[groundRoots.Length];
            for (int i = 0; i < groundRoots.Length; i++)
            {
                rootRestDir[i] = transform.InverseTransformDirection(Flat(groundRoots[i].Tip - groundRoots[i].Base).normalized);
                rootSeeds[i] = Random.Range(0f, 100f);
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // 攻撃していないときだけ、うねりをなめらかに強める
            idleWeight = Mathf.MoveTowards(idleWeight, Action == BossAction.None ? 1f : 0f, dt * 2f);

            Restore(tendrilL, unwavedL);
            Restore(tendrilR, unwavedR);
            UpdateTendril(tendrilL, -1f, dt);
            UpdateTendril(tendrilR, 1f, dt);
            Undulate(tendrilL, seedL, ref wavePhaseL, ref unwavedL, dt);
            Undulate(tendrilR, seedR, ref wavePhaseR, ref unwavedR, dt);
            UpdateGroundRoots();
            UpdateFace(dt);
        }

        /// 待機中の先端の目標。ノイズでランダムにさまよう
        Vector3 IdleTarget(float side)
        {
            float s = transform.lossyScale.y;
            float t = Time.time * idleWanderSpeed;
            float seed = side < 0f ? seedL : seedR;
            Vector3 wander = new Vector3(
                Noise(t, seed),
                Noise(t, seed + 31.7f) * 0.7f,
                Noise(t, seed + 57.3f)) * idleWander;
            return transform.position + (transform.right * side * 5f + transform.forward * 4f + Vector3.up * 4f + wander) * s;
        }

        static void Restore(FabrikChain chain, Vector3[] unwaved)
        {
            if (unwaved == null || unwaved.Length != chain.joints.Length) return;
            for (int i = 0; i < unwaved.Length; i++) chain.joints[i].position = unwaved[i];
        }

        /// -1〜1 のなめらかなノイズ
        static float Noise(float t, float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;

        /// 根元から先端へ伝わる波で、根の途中の関節を横にうねらせる（両端は動かさない）
        void Undulate(FabrikChain chain, float seed, ref float phase, ref Vector3[] unwaved, float dt)
        {
            var joints = chain.joints;
            int n = joints.Length;
            if (unwaved == null || unwaved.Length != n) unwaved = new Vector3[n];
            for (int i = 0; i < n; i++) unwaved[i] = joints[i].position;
            if (idleWeight <= 0f) return;
            float s = transform.lossyScale.y;

            // 波の速さも少し揺らして、機械的な繰り返しに見えないようにする
            phase += dt * idleWaveSpeed * (0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 0.3f, seed));

            Vector3 axis = (chain.Tip - chain.Base).normalized;
            Vector3 sideA = Vector3.Cross(axis, Vector3.up);
            if (sideA.sqrMagnitude < 1e-4f) sideA = Vector3.Cross(axis, Vector3.forward);
            sideA.Normalize();
            Vector3 sideB = Vector3.Cross(axis, sideA);

            for (int i = 1; i < n - 1; i++)
            {
                float u = i / (float)(n - 1);
                float envelope = Mathf.Sin(u * Mathf.PI);   // 両端0・中央で最大
                float w = u * idleWaveCount * Mathf.PI * 2f;
                Vector3 offset = sideA * Mathf.Sin(phase - w) + sideB * Mathf.Cos(phase * 0.8f - w * 0.9f) * 0.6f;
                joints[i].position += offset * (idleWaveAmplitude * envelope * idleWeight * s);
            }
            chain.RefreshRotations();
        }

        void UpdateTendril(FabrikChain chain, float side, float dt)
        {
            float s = transform.lossyScale.y;
            Vector3 fwd = transform.forward;
            Vector3 right = transform.right;
            Vector3 up = Vector3.up;
            Vector3 pos = transform.position;
            Vector3 desired = IdleTarget(side);
            float follow = 4f;
            bool isHammerArm = side > 0f;

            switch (Action)
            {
                case BossAction.HammerWindup when isHammerArm:
                {
                    // 叩きつける線の真上へ高く振り上げる
                    Vector3 raised = hammerOrigin + hammerDir * hammerLength * 0.3f + up * 13f * s;
                    desired = Vector3.Lerp(IdleTarget(side), raised, Smooth(Progress));
                    slamStart = chain.Tip;
                    follow = 8f;
                    break;
                }
                case BossAction.HammerSlam when isHammerArm:
                {
                    Vector3 end = hammerOrigin + hammerDir * hammerLength + up * 0.6f * s;
                    chain.target = Vector3.Lerp(slamStart, end, Progress * Progress);
                    chain.Solve();
                    return;
                }
                case BossAction.MeleeWindup when !isHammerArm:
                    // 左の根の腕を大きく引き絞る
                    desired = pos + (-right * 9f + fwd * 3f + up * 3f) * s;
                    follow = 10f;
                    break;
                case BossAction.MeleeStrike when !isHammerArm:
                {
                    // 足元の正面を左から右へ薙ぎ払う
                    float angle = Mathf.Lerp(-85f, 85f, 1f - (1f - Progress) * (1f - Progress));
                    chain.target = pos + (Quaternion.AngleAxis(angle, up) * fwd * 8f + up * 1f) * s;
                    chain.Solve();
                    return;
                }
                case BossAction.RootCast:
                    // 地面に突き刺して根を地中に送る
                    desired = pos + (fwd * 5f + right * side * 2.5f - up * 1.5f) * s;
                    follow = 6f;
                    break;
                case BossAction.LeafCast:
                    desired = pos + (fwd * 3f + right * side * 3f + up * 12f) * s;
                    follow = 5f;
                    break;
                case BossAction.Summon:
                    desired = pos + (fwd * 2f + right * side * 8f + up * 7f) * s;
                    follow = 5f;
                    break;
                case BossAction.Dead:
                    desired = pos + (fwd * 6f + right * side * 7f - up * 1f) * s;
                    follow = 1.5f;
                    break;
            }

            chain.target = Vector3.Lerp(chain.target, desired, 1f - Mathf.Exp(-follow * dt));
            chain.Solve();
        }

        void UpdateGroundRoots()
        {
            float s = transform.lossyScale.y;
            float t = Time.time;
            bool dead = Action == BossAction.Dead;
            for (int i = 0; i < groundRoots.Length; i++)
            {
                var chain = groundRoots[i];
                Vector3 rest = transform.TransformDirection(rootRestDir[i]);
                Vector3 side = Vector3.Cross(Vector3.up, rest);
                float seed = rootSeeds[i];
                float amp = dead ? 0f : 1f;
                Vector3 wiggle = (side * Mathf.Sin(t * 1.3f + seed) * 0.8f + Vector3.up * (0.3f + Mathf.Sin(t * 2.1f + seed) * 0.4f)) * amp;
                chain.target = chain.Base + rest * chain.TotalLength * 0.85f + wiggle * s;
                chain.Solve();
            }
        }

        void UpdateFace(float dt)
        {
            Vector3 look = PlayerPos + Vector3.up * 1.2f - face.position;
            Vector3 dir = ClampLook(transform.forward, look, faceYawLimit);
            float roll = JerkyRoll(ref headRoll, ref headRollTarget, ref nextHeadSnap, headRollMax, dt);
            if (Action == BossAction.Dead)
            {
                dir = (transform.forward - Vector3.up).normalized;
                roll = 40f;
            }
            face.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
