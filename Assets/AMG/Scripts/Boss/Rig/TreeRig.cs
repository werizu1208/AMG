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

        public override Vector3 CastPoint => face.position + face.forward * 1.2f * transform.lossyScale.y;

        Vector3[] rootRestDir;
        float[] rootSeeds;
        float headRoll, headRollTarget, nextHeadSnap;
        Vector3 slamStart;

        void Start()
        {
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

            UpdateTendril(tendrilL, -1f, dt);
            UpdateTendril(tendrilR, 1f, dt);
            UpdateGroundRoots();
            UpdateFace(dt);
        }

        Vector3 IdleTarget(float side)
        {
            float s = transform.lossyScale.y;
            float t = Time.time;
            Vector3 sway = new Vector3(Mathf.Sin(t * 0.9f + side), Mathf.Sin(t * 1.3f + side * 2f) * 0.6f, Mathf.Cos(t * 0.7f + side)) * 0.8f;
            return transform.position + (transform.right * side * 5f + transform.forward * 4f + Vector3.up * 4f + sway) * s;
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
