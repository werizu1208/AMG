using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// ボスの攻撃4種。予兆を出してからリグに動きを指示し、判定を行う。
    /// フェーズ2は「雑になるが攻撃範囲が広がる」
    public class BossAttacks : MonoBehaviour
    {
        [Header("共通")]
        public float phase1Interval = 1.6f;
        public float phase2Interval = 2.3f;

        [Header("地面から突き出す根")]
        public float rootDamage = 25f;
        public float rootRadius1 = 2.2f;
        public float rootRadius2 = 3.4f;
        public float rootDelay1 = 1.0f;
        public float rootDelay2 = 1.25f;
        public int rootCount2 = 4;

        [Header("倒木のハンマー（タメあり・スタン）")]
        public float hammerDamage = 45f;
        public float hammerStun = 1.0f;
        public float hammerWindup1 = 1.3f;
        public float hammerWindup2 = 1.7f;
        public float hammerSlamTime = 0.18f;
        public Vector2 hammerArea1 = new Vector2(3f, 7f);   // 幅, 長さ
        public Vector2 hammerArea2 = new Vector2(7f, 16f);

        [Header("葉の刃")]
        public float leafDamage = 12f;
        public float leafSpeed = 20f;
        public int leavesPerLine = 5;
        public float leafWindup = 0.7f;
        public float leafSpreadAngle2 = 20f;

        [Header("案山子の召喚")]
        public int summonCount1 = 2;
        public int summonCount2 = 3;
        public int maxScarecrows = 6;
        public float summonCooldown = 18f;

        public bool IsBusy { get; private set; }

        BossController boss;
        float nextAttackTime;
        float nextSummonTime;
        bool started;
        Coroutine current;
        readonly List<GameObject> temporary = new List<GameObject>();

        void Awake()
        {
            boss = GetComponent<BossController>();
        }

        void Update()
        {
            if (!GameManager.IsPlaying) return;
            if (!started)
            {
                started = true;
                nextAttackTime = Time.time + 2.5f;
                nextSummonTime = Time.time + 12f;
            }
            var player = PlayerHealth.I;
            if (IsBusy || boss.IsDead || boss.IsTransforming || boss.IsReviving || player == null || !player.IsAlive) return;
            if (Time.time < nextAttackTime) return;
            current = StartCoroutine(AttackRoutine());
        }

        public void Delay(float seconds)
        {
            nextAttackTime = Time.time + seconds;
        }

        public void CancelAll()
        {
            if (current != null) StopCoroutine(current);
            current = null;
            IsBusy = false;
            foreach (var go in temporary)
                if (go != null) Destroy(go);
            temporary.Clear();
            if (boss.Rig != null) boss.Rig.Play(BossAction.None);
        }

        IEnumerator AttackRoutine()
        {
            IsBusy = true;
            bool p2 = boss.Phase == 2;
            float dist = Flat(PlayerHealth.I.transform.position - boss.AttackOrigin).magnitude;

            float wRoot = 3f;
            float wLeaf = 3f;
            float wHammer = dist < (p2 ? hammerArea2.y : hammerArea1.y - 0.5f) ? 5f : (p2 ? 1f : 0.3f);
            float wSummon = Time.time >= nextSummonTime && Scarecrow.Alive < maxScarecrows ? 2.5f : 0f;
            float r = Random.value * (wRoot + wLeaf + wHammer + wSummon);

            if ((r -= wRoot) < 0f) yield return RootSpikes(p2);
            else if ((r -= wLeaf) < 0f) yield return LeafBlades(p2);
            else if ((r -= wHammer) < 0f) yield return Hammer(p2);
            else yield return Summon(p2);

            boss.Rig.Play(BossAction.None);
            IsBusy = false;
            current = null;
            nextAttackTime = Time.time + (p2 ? phase2Interval : phase1Interval);
        }

        /// アクション再生しつつ進行度をリグに伝える
        IEnumerator Act(BossAction action, float duration)
        {
            var rig = boss.Rig;
            rig.Play(action);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                rig.Progress = t / duration;
                yield return null;
            }
            rig.Progress = 1f;
        }

        // ---------- 地面から突き出す根 ----------

        IEnumerator RootSpikes(bool p2)
        {
            var player = PlayerHealth.I;
            int count = p2 ? rootCount2 : 1;
            float radius = p2 ? rootRadius2 : rootRadius1;
            float delay = p2 ? rootDelay2 : rootDelay1;

            var targets = new List<Vector3> { Flat(player.transform.position) };
            for (int i = 1; i < count; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(radius * 1.2f, radius * 3f);
                targets.Add(targets[0] + new Vector3(off.x, 0f, off.y));
            }

            var marks = new List<GameObject>();
            foreach (var t in targets) marks.Add(CircleTelegraph(t, radius));
            yield return Act(BossAction.RootCast, delay);
            foreach (var m in marks) Remove(m);

            var spikes = new List<GameObject>();
            foreach (var t in targets)
            {
                spikes.Add(SpawnSpikes(t, radius));
                if (Flat(player.transform.position - t).magnitude <= radius && player.transform.position.y < 2.5f)
                    player.TakeHit(rootDamage);
            }
            yield return Act(BossAction.RootCast, 0.6f);
            foreach (var s in spikes) Remove(s);
        }

        GameObject SpawnSpikes(Vector3 center, float radius)
        {
            var lib = VfxLibrary.I;
            var root = new GameObject("RootSpikes");
            root.transform.position = center;
            temporary.Add(root);
            int n = Mathf.RoundToInt(radius * 3f);
            for (int i = 0; i < n; i++)
            {
                Vector2 o = Random.insideUnitCircle * radius * 0.8f;
                float h = Random.Range(1.5f, 3f);
                var rot = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                Prim.Create(PrimitiveType.Cylinder, "Spike", root.transform, new Vector3(o.x, h * 0.5f, o.y),
                    new Vector3(0.35f, h * 0.5f, 0.35f), lib.spike, false, rot);
            }
            return root;
        }

        // ---------- 倒木のハンマー ----------

        IEnumerator Hammer(bool p2)
        {
            var player = PlayerHealth.I;
            Vector2 area = p2 ? hammerArea2 : hammerArea1;
            float windup = p2 ? hammerWindup2 : hammerWindup1;

            // フェーズ1は少女がプレイヤーへ向き直る
            if (!p2)
            {
                for (float t = 0f; t < 0.25f; t += Time.deltaTime)
                {
                    boss.FaceDirection(player.transform.position - transform.position, 720f);
                    yield return null;
                }
            }

            Vector3 origin = Flat(boss.AttackOrigin);
            Vector3 dir = Flat(player.transform.position - origin);
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
            if (!p2) dir = transform.forward;
            Quaternion rot = Quaternion.LookRotation(dir);

            boss.Rig.SetHammer(origin, dir, area.y);
            var mark = RectTelegraph(origin, rot, area.x, area.y);
            yield return Act(BossAction.HammerWindup, windup);
            yield return Act(BossAction.HammerSlam, hammerSlamTime);
            Remove(mark);

            Vector3 rel = Quaternion.Inverse(rot) * (player.transform.position - origin);
            if (rel.z >= -0.5f && rel.z <= area.y && Mathf.Abs(rel.x) <= area.x * 0.5f && rel.y < 2.5f)
                player.TakeHit(hammerDamage, hammerStun);
            VfxLibrary.Burst(origin + dir * area.y * 0.6f, area.x, VfxLibrary.I.explosion, 0.3f);

            yield return Act(BossAction.HammerSlam, 0.6f);
        }

        // ---------- 葉の刃 ----------

        IEnumerator LeafBlades(bool p2)
        {
            int lines = p2 ? 3 : 1;
            var rig = boss.Rig;
            float s = p2 ? 2f : 1f;

            // 葉が宙に舞い上がる
            var leaves = new List<LeafBlade>();
            for (int l = 0; l < lines; l++)
            {
                for (int i = 0; i < leavesPerLine; i++)
                {
                    var leaf = LeafBlade.Create(rig.CastPoint + Random.insideUnitSphere * 0.8f * s, s);
                    temporary.Add(leaf.gameObject);
                    leaves.Add(leaf);
                }
            }
            rig.Play(BossAction.LeafCast);
            for (float t = 0f; t < leafWindup; t += Time.deltaTime)
            {
                rig.Progress = t / leafWindup;
                foreach (var leaf in leaves)
                    if (leaf != null) leaf.Hover(rig.CastPoint);
                yield return null;
            }

            // 一直線上に飛ばす
            Vector3 from = rig.CastPoint;
            Vector3 aim = (PlayerHealth.I.Center - from).normalized;
            for (int i = 0; i < leavesPerLine; i++)
            {
                for (int l = 0; l < lines; l++)
                {
                    var leaf = leaves[l * leavesPerLine + i];
                    if (leaf == null) continue;
                    float angle = lines == 1 ? 0f : Mathf.Lerp(-leafSpreadAngle2, leafSpreadAngle2, l / (lines - 1f));
                    temporary.Remove(leaf.gameObject);
                    leaf.transform.position = from;
                    leaf.Launch(Quaternion.AngleAxis(angle, Vector3.up) * aim, leafSpeed, leafDamage);
                }
                yield return new WaitForSeconds(0.12f);
            }
        }

        // ---------- 案山子の召喚 ----------

        IEnumerator Summon(bool p2)
        {
            int count = p2 ? summonCount2 : summonCount1;
            nextSummonTime = Time.time + summonCooldown;

            var points = new List<Vector3>();
            var marks = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * 0.5f) * 40f;
                Vector3 p = Flat(boss.AttackOrigin) + Quaternion.AngleAxis(angle, Vector3.up) * transform.forward * (p2 ? 6f : 3f);
                points.Add(p);
                marks.Add(CircleTelegraph(p, 0.8f));
            }
            yield return Act(BossAction.Summon, 0.9f);
            foreach (var m in marks) Remove(m);
            foreach (var p in points) Scarecrow.Spawn(p, transform.rotation);
            yield return Act(BossAction.Summon, 0.3f);
        }

        // ---------- 予兆表示 ----------

        GameObject CircleTelegraph(Vector3 center, float radius)
        {
            var go = Prim.Create(PrimitiveType.Cylinder, "Telegraph", null, center + Vector3.up * 0.03f,
                new Vector3(radius * 2f, 0.01f, radius * 2f), VfxLibrary.I.telegraph);
            temporary.Add(go);
            return go;
        }

        GameObject RectTelegraph(Vector3 origin, Quaternion rot, float width, float length)
        {
            var go = Prim.Create(PrimitiveType.Cube, "Telegraph", null, origin + rot * Vector3.forward * length * 0.5f + Vector3.up * 0.03f,
                new Vector3(width, 0.02f, length), VfxLibrary.I.telegraph, false, rot);
            temporary.Add(go);
            return go;
        }

        void Remove(GameObject go)
        {
            temporary.Remove(go);
            if (go != null) Destroy(go);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
