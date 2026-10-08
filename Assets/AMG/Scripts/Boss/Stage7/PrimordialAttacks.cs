using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// 原初の魔法少女の攻撃。軌道はすべてピンクで、隕石を思わせる演出にする。
    /// ・流星群：プレイヤーの周りに予兆を出し、空から小さな隕石を降らせる
    /// ・彗星の槍：狙った方向へ一直線に彗星を走らせる（フェーズが進むと本数が増える）
    /// ・彗星の突進（フェーズ3）：自分が彗星となって一直線に駆け抜ける
    /// ・使い魔（星）の召喚 / 隕石の化け物の召喚（フェーズ2から）
    /// ・星落とし（フェーズ1・2）：浮き上がって叩きつける。その後は力尽きて弱点が露出する
    /// ・振り払い：近づかれたら、周りに衝撃を放って吹き飛ばす
    public class PrimordialAttacks : MonoBehaviour
    {
        [Header("攻撃の間隔（フェーズ1〜4）")]
        public float[] intervals = { 1.9f, 1.6f, 1.25f, 1.7f };

        [Header("流星群")]
        public int[] showerCounts = { 3, 4, 6, 7 };
        public float showerRadius = 3f;
        public float showerDelay = 1.5f;
        public float showerDelayFast = 1.15f;   // フェーズ3以降
        public float showerDamage = 22f;
        public float showerSpread = 9f;

        [Header("彗星の槍")]
        public int[] lanceCounts = { 1, 2, 3, 3 };
        public float lanceFanAngle = 14f;
        public float lanceWidth = 2.4f;
        public float lanceLength = 48f;
        public float lanceWindup = 1.0f;
        public float lanceWindupFast = 0.75f;
        public float lanceDamage = 28f;

        [Header("彗星の突進（フェーズ3）")]
        public float dashWindup = 0.8f;
        public float dashWidth = 3f;
        public float dashOvershoot = 6f;
        public float dashTime = 0.35f;
        public float dashDamage = 30f;

        [Header("使い魔（星）")]
        public int[] familiarCounts = { 4, 6, 6, 8 };
        public float familiarCooldown = 9f;

        [Header("隕石の化け物（フェーズ2から）")]
        public int[] beastCounts = { 0, 1, 2, 2 };
        public int maxBeasts = 3;
        public float beastCooldown = 16f;

        [Header("星落とし（フェーズ1・2。この後に弱点が露出する）")]
        public float impactCooldown = 16f;
        public float impactRiseTime = 1.3f;
        public float impactHeight = 4f;
        public float impactRadius = 8f;
        public float impactDamage = 40f;
        public float impactKnockback = 12f;

        [Header("振り払い（近づかれたとき）")]
        public float repulseRange = 3.5f;
        public float repulseRadius = 4.5f;
        public float repulseWindup = 0.55f;
        public float repulseDamage = 15f;
        public float repulseKnockback = 12f;
        public float repulseCooldown = 4f;

        public bool IsBusy { get; private set; }

        PrimordialBoss boss;
        float nextAttackTime;
        float nextFamiliarTime;
        float nextBeastTime;
        float nextImpactTime;
        float nextRepulseTime;
        int attacksSinceImpact;
        bool started;
        Coroutine current;
        readonly List<GameObject> temporary = new List<GameObject>();

        void Awake()
        {
            boss = GetComponent<PrimordialBoss>();
        }

        int P => Mathf.Clamp(boss.Phase, 1, 4) - 1;

        void Update()
        {
            if (!GameManager.IsPlaying) return;
            if (!started)
            {
                started = true;
                // 最初の台詞を言い終わるころに攻撃を始める
                nextAttackTime = Time.time + 6f;
                nextFamiliarTime = Time.time + 10f;
                nextBeastTime = Time.time + 6f;
                nextImpactTime = Time.time + 14f;
            }
            var player = PlayerHealth.I;
            if (IsBusy || boss.IsDead || boss.IsTransitioning || boss.IsExposed || player == null || !player.IsAlive) return;

            if (Time.time >= nextRepulseTime && Flat(player.transform.position - transform.position).magnitude <= repulseRange && boss.Phase < 4)
            {
                current = StartCoroutine(Run(Repulse()));
                return;
            }
            if (Time.time < nextAttackTime) return;
            current = StartCoroutine(Run(ChooseAttack()));
        }

        public void Delay(float seconds)
        {
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + seconds);
        }

        public void CancelAll()
        {
            if (current != null) StopCoroutine(current);
            current = null;
            IsBusy = false;
            foreach (var go in temporary)
                if (go != null) Destroy(go);
            temporary.Clear();
            if (boss.Rig != null)
            {
                boss.Rig.Play(boss.Phase == 4 ? PrimordialPose.Float : PrimordialPose.Idle);
                if (boss.Phase < 4) boss.Rig.extraHeight = 0f;
            }
        }

        IEnumerator Run(IEnumerator attack)
        {
            IsBusy = true;
            yield return attack;
            if (!boss.IsDead && !boss.IsVulnerable) boss.Rig.Play(boss.Phase == 4 ? PrimordialPose.Float : PrimordialPose.Idle);
            IsBusy = false;
            current = null;
            nextAttackTime = Time.time + intervals[P];
        }

        IEnumerator ChooseAttack()
        {
            int phase = boss.Phase;
            float dist = Flat(PlayerHealth.I.transform.position - transform.position).magnitude;

            float wShower = phase == 4 ? 4f : 3f;
            float wLance = 3f;
            float wDash = phase == 3 ? 3f : 0f;
            float wFamiliar = Time.time >= nextFamiliarTime ? 2f : 0f;
            float wBeast = beastCounts[P] > 0 && Time.time >= nextBeastTime && MeteorBeast.Alive.Count < maxBeasts ? 2.5f : 0f;
            // 星落としは、しばらく出していないほど選ばれやすい（必ず弱点を見せる機会を作る）
            float wImpact = phase <= 2 && Time.time >= nextImpactTime ? 3f + attacksSinceImpact * 1.5f : 0f;
            if (dist < 6f) wLance += 1f;

            float r = Random.value * (wShower + wLance + wDash + wFamiliar + wBeast + wImpact);
            attacksSinceImpact++;
            if ((r -= wShower) < 0f) return StarShower(phase);
            if ((r -= wLance) < 0f) return CometLance(phase);
            if ((r -= wDash) < 0f) return CometDash();
            if ((r -= wFamiliar) < 0f) return SummonFamiliars(phase);
            if ((r -= wBeast) < 0f || wImpact <= 0f) return wBeast > 0f ? SummonBeasts(phase) : StarShower(phase);
            attacksSinceImpact = 0;
            return StarImpact();
        }

        IEnumerator Act(PrimordialPose pose, float duration)
        {
            var rig = boss.Rig;
            rig.Play(pose);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                rig.Progress = t / duration;
                yield return null;
            }
            rig.Progress = 1f;
        }

        // ---------- 流星群 ----------

        IEnumerator StarShower(int phase)
        {
            var player = PlayerHealth.I;
            int count = showerCounts[P];
            float radius = phase == 4 ? showerRadius * 1.15f : showerRadius;
            float delay = phase >= 3 ? showerDelayFast : showerDelay;

            var targets = new List<Vector3> { Ground(player.transform.position) };
            for (int i = 1; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(radius * 1.3f, showerSpread);
                targets.Add(Ground(targets[0] + new Vector3(off.x, 0f, off.y)));
            }
            var marks = new List<GameObject>();
            foreach (var t in targets) marks.Add(CircleTelegraph(t, radius));

            // 予兆の間に、空から小さな隕石が降りてくる
            var meteors = new List<GameObject>();
            foreach (var t in targets)
            {
                var m = Prim.Create(PrimitiveType.Sphere, "Meteorite", null, t + Vector3.up * 30f, Vector3.one * 0.9f, boss.meteorMaterial);
                Stage7Fx.AddTrail(m, boss.cometMaterial, 0.8f, 0.4f);
                temporary.Add(m);
                meteors.Add(m);
            }

            boss.Rig.Play(PrimordialPose.CallMeteor);
            for (float t = 0f; t < delay; t += Time.deltaTime)
            {
                float k = t / delay;
                for (int i = 0; i < meteors.Count; i++)
                    if (meteors[i] != null)
                        meteors[i].transform.position = targets[i] + Vector3.up * Mathf.Lerp(30f, 0.4f, k * k);
                yield return null;
            }

            foreach (var m in marks) Remove(m);
            foreach (var m in meteors) Remove(m);
            foreach (var t in targets)
            {
                if (VfxLibrary.I != null) VfxLibrary.Burst(t + Vector3.up * 0.3f, radius, boss.burstMaterial, 0.35f);
                Stage7Fx.Blast(t, radius, showerDamage, 0f, 0f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        // ---------- 彗星の槍 ----------

        IEnumerator CometLance(int phase)
        {
            var player = PlayerHealth.I;
            int count = lanceCounts[P];
            float windup = phase >= 3 ? lanceWindupFast : lanceWindup;
            Vector3 origin = Ground(transform.position);
            Vector3 aim = Flat(player.transform.position - origin);
            if (aim.sqrMagnitude < 0.01f) aim = transform.forward;

            var dirs = new List<Quaternion>();
            for (int i = 0; i < count; i++)
            {
                float a = (i - (count - 1) * 0.5f) * lanceFanAngle;
                dirs.Add(Quaternion.LookRotation(aim) * Quaternion.Euler(0f, a, 0f));
            }
            var marks = new List<GameObject>();
            foreach (var rot in dirs) marks.Add(RectTelegraph(origin, rot, lanceWidth, lanceLength));

            boss.Rig.LookTarget = player.Center;
            yield return Act(PrimordialPose.Point, windup);
            foreach (var m in marks) Remove(m);

            Vector3 from = boss.Rig.HandR != null ? boss.Rig.HandR.position : boss.CastPoint;
            foreach (var rot in dirs)
            {
                Vector3 end = origin + rot * Vector3.forward * lanceLength + Vector3.up * 1f;
                StartCoroutine(Streak(from, end, 0.9f, 0.4f));
                if (InRect(player.transform.position, origin, rot, lanceWidth, lanceLength)) player.TakeHit(lanceDamage);
            }
            yield return new WaitForSeconds(0.35f);
        }

        IEnumerator Streak(Vector3 a, Vector3 b, float width, float life)
        {
            var go = new GameObject("CometStreak");
            temporary.Add(go);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = boss.cometMaterial;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (float t = 0f; t < life && go != null; t += Time.deltaTime)
            {
                float w = width * (1f - t / life);
                lr.startWidth = w;
                lr.endWidth = w * 0.3f;
                yield return null;
            }
            Remove(go);
        }

        // ---------- 彗星の突進（フェーズ3） ----------

        IEnumerator CometDash()
        {
            var player = PlayerHealth.I;
            Vector3 origin = Ground(transform.position);
            Vector3 to = Flat(player.transform.position - origin);
            if (to.sqrMagnitude < 0.01f) to = transform.forward;
            float length = Mathf.Min(to.magnitude + dashOvershoot, boss.arenaRadius * 1.6f);
            var rot = Quaternion.LookRotation(to);
            var mark = RectTelegraph(origin, rot, dashWidth, length);

            boss.Rig.LookTarget = player.Center;
            boss.FaceDirection(to, 9999f);
            yield return Act(PrimordialPose.Point, dashWindup);
            Remove(mark);

            // 彗星となって駆け抜ける（体の光の尾が軌道になる）
            Vector3 start = transform.position;
            Vector3 end = start + rot * Vector3.forward * length;
            Vector3 flatEnd = Flat(end);
            if (flatEnd.magnitude > boss.arenaRadius) end = flatEnd.normalized * boss.arenaRadius + Vector3.up * start.y;
            bool hit = false;
            for (float t = 0f; t < dashTime; t += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, end, t / dashTime);
                if (!hit && Flat(player.transform.position - transform.position).magnitude <= dashWidth * 0.6f)
                {
                    hit = true;
                    if (player.TakeHit(dashDamage))
                        player.GetComponent<PlayerController>().Knockback(Vector3.Cross(Vector3.up, rot * Vector3.forward) * 8f + Vector3.up * 4f);
                }
                yield return null;
            }
            transform.position = end;
            yield return new WaitForSeconds(0.4f);
        }

        // ---------- 召喚 ----------

        IEnumerator SummonFamiliars(int phase)
        {
            nextFamiliarTime = Time.time + familiarCooldown;
            yield return Act(PrimordialPose.RaiseBoth, 0.7f);
            var player = PlayerHealth.I;
            int count = familiarCounts[P];
            for (int i = 0; i < count; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(5f, 10f);
                Vector3 pos = player.transform.position + new Vector3(r.x, Random.Range(8f, 12f), r.y);
                StarFamiliar.Spawn(pos, boss.familiarMaterial, boss.cometMaterial);
                yield return new WaitForSeconds(0.08f);
            }
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator SummonBeasts(int phase)
        {
            nextBeastTime = Time.time + beastCooldown;
            yield return Act(PrimordialPose.RaiseBoth, 1f);
            var player = PlayerHealth.I;
            int count = Mathf.Min(beastCounts[P], maxBeasts - MeteorBeast.Alive.Count);
            for (int i = 0; i < count; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(6f, 12f);
                Vector3 pos = player.transform.position + new Vector3(r.x, 14f, r.y);
                MeteorBeast.Spawn(pos, boss);
            }
            yield return new WaitForSeconds(0.4f);
        }

        // ---------- 星落とし（フェーズ1・2） ----------

        IEnumerator StarImpact()
        {
            nextImpactTime = Time.time + impactCooldown;
            Vector3 center = Ground(transform.position);
            var mark = CircleTelegraph(center, impactRadius);
            var circle = Stage7Fx.MagicCircle(center + Vector3.up * 0.06f, impactRadius * 0.6f, boss.circleMaterial, 0f);
            temporary.Add(circle);

            var rig = boss.Rig;
            rig.Play(PrimordialPose.Charge);
            for (float t = 0f; t < impactRiseTime; t += Time.deltaTime)
            {
                float k = t / impactRiseTime;
                rig.extraHeight = Mathf.SmoothStep(0f, impactHeight, k);
                circle.transform.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);
                yield return null;
            }

            rig.Play(PrimordialPose.Slam);
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                rig.extraHeight = Mathf.Lerp(impactHeight, 0f, t / 0.15f);
                yield return null;
            }
            rig.extraHeight = 0f;
            Remove(mark);
            Remove(circle);
            if (VfxLibrary.I != null) VfxLibrary.Burst(center + Vector3.up * 0.5f, impactRadius, boss.burstMaterial, 0.5f);
            Stage7Fx.Blast(center, impactRadius, impactDamage, impactKnockback, 6f);

            // 力を使い果たして、しばらく弱点が露出する
            yield return boss.ExposeRoutine();
        }

        // ---------- 振り払い ----------

        IEnumerator Repulse()
        {
            nextRepulseTime = Time.time + repulseCooldown;
            Vector3 center = Ground(transform.position);
            var mark = CircleTelegraph(center, repulseRadius);
            yield return Act(PrimordialPose.RaiseBoth, repulseWindup);
            Remove(mark);
            if (VfxLibrary.I != null) VfxLibrary.Burst(boss.CastPoint, repulseRadius, boss.burstMaterial, 0.3f);
            Stage7Fx.Blast(center, repulseRadius, repulseDamage, repulseKnockback, 4f);
            yield return new WaitForSeconds(0.25f);
        }

        // ---------- 予兆 ----------

        GameObject CircleTelegraph(Vector3 center, float radius)
        {
            var go = Prim.Create(PrimitiveType.Cylinder, "Telegraph", null, center + Vector3.up * 0.03f,
                new Vector3(radius * 2f, 0.01f, radius * 2f), boss.telegraphMaterial);
            DangerZone.AddCircle(go, center, radius);
            temporary.Add(go);
            return go;
        }

        GameObject RectTelegraph(Vector3 origin, Quaternion rot, float width, float length)
        {
            var go = Prim.Create(PrimitiveType.Cube, "Telegraph", null, origin + rot * Vector3.forward * length * 0.5f + Vector3.up * 0.03f,
                new Vector3(width, 0.02f, length), boss.telegraphMaterial, false, rot);
            DangerZone.AddRect(go, origin, rot, width, length);
            temporary.Add(go);
            return go;
        }

        static bool InRect(Vector3 p, Vector3 origin, Quaternion rot, float width, float length)
        {
            Vector3 rel = Quaternion.Inverse(rot) * Flat(p - origin);
            return rel.z >= -0.5f && rel.z <= length && Mathf.Abs(rel.x) <= width * 0.5f && p.y < origin.y + 3f;
        }

        void Remove(GameObject go)
        {
            temporary.Remove(go);
            if (go != null) Destroy(go);
        }

        static Vector3 Ground(Vector3 p)
        {
            p.y = Stage7Fx.GroundHeight(p);
            return p;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
