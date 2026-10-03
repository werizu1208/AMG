using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// ボスの攻撃。予兆を出してからリグに動きを指示し、判定を行う。
    /// ・根 / 倒木のハンマー / 葉の刃 / 案山子の召喚 / 潜行（フェーズ1のみ）
    /// ・格闘：プレイヤーが近づいたときだけ割り込みで出す（ノックバック）
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

        [Header("格闘（近接時のみ・ノックバック）")]
        public float meleeRange1 = 2.6f;     // 少女の大きさに合わせて伸びる
        public float meleeRange2 = 9f;
        public float meleeAngle = 85f;       // 正面から左右何度まで当たるか
        public float meleeDamage = 20f;
        public float meleeKnockback = 13f;
        public float meleeKnockUp = 4f;
        public float meleeWindup1 = 0.4f;
        public float meleeWindup2 = 0.6f;
        public float meleeStrikeTime = 0.15f;
        public float meleeCooldown = 3f;

        [Header("潜行：地中に潜り、下から根とともに突き出す（フェーズ1のみ）")]
        public float burrowCooldown = 12f;
        public float burrowPreferDistance = 10f;  // これより離れているときに選ばれやすい
        public float burrowDepth = 2.5f;
        public float burrowSinkTime = 0.7f;
        public float burrowTravelSpeed = 9f;
        public float burrowMaxTravelTime = 2.5f;
        public float burrowLockTime = 0.8f;       // 噴き出す地点が決まってから噴き出すまで（予兆）
        public float burrowRadius = 2.8f;
        public float burrowDamage = 30f;
        public float burrowKnockback = 6f;
        public float burrowKnockUp = 9f;
        public float burrowEmergeTime = 0.25f;
        public float burrowRecoverTime = 0.9f;    // 突き出した後の隙

        public bool IsBusy { get; private set; }

        BossController boss;
        float nextAttackTime;
        float nextSummonTime;
        float nextMeleeTime;
        float nextBurrowTime;
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
                nextBurrowTime = Time.time + 8f;
            }
            var player = PlayerHealth.I;
            if (IsBusy || boss.IsDead || boss.IsTransforming || boss.IsReviving || player == null || !player.IsAlive) return;

            // 近づかれたら、通常の攻撃間隔を待たずに格闘で振り払う
            if (Time.time >= nextMeleeTime && PlayerInMeleeRange(player))
            {
                current = StartCoroutine(Run(Melee(boss.Phase == 2)));
                return;
            }
            if (Time.time < nextAttackTime) return;
            current = StartCoroutine(Run(ChooseAttack()));
        }

        float MeleeRange(bool p2) => p2 ? meleeRange2 : meleeRange1 * boss.SizeScale;

        bool PlayerInMeleeRange(PlayerHealth player)
        {
            bool p2 = boss.Phase == 2;
            Vector3 to = Flat(player.transform.position - transform.position);
            return to.magnitude <= MeleeRange(p2) && Vector3.Angle(transform.forward, to) <= meleeAngle + 30f;
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

            // 潜行中に中断されたら地上へ戻す
            if (boss.IsBurrowed || transform.position.y < -0.01f)
            {
                boss.IsBurrowed = false;
                Vector3 p = transform.position;
                p.y = 0f;
                transform.position = p;
            }
        }

        IEnumerator Run(IEnumerator attack)
        {
            IsBusy = true;
            bool p2 = boss.Phase == 2;
            yield return attack;
            boss.Rig.Play(BossAction.None);
            IsBusy = false;
            current = null;
            nextAttackTime = Time.time + (p2 ? phase2Interval : phase1Interval);
        }

        IEnumerator ChooseAttack()
        {
            bool p2 = boss.Phase == 2;
            float dist = Flat(PlayerHealth.I.transform.position - boss.AttackOrigin).magnitude;

            float wRoot = 3f;
            float wLeaf = 3f;
            float wHammer = dist < (p2 ? hammerArea2.y : hammerArea1.y - 0.5f) ? 5f : (p2 ? 1f : 0.3f);
            float wSummon = Time.time >= nextSummonTime && Scarecrow.Alive < maxScarecrows ? 2.5f : 0f;
            float wBurrow = !p2 && Time.time >= nextBurrowTime ? (dist > burrowPreferDistance ? 4f : 1f) : 0f;
            float r = Random.value * (wRoot + wLeaf + wHammer + wSummon + wBurrow);

            if ((r -= wRoot) < 0f) return RootSpikes(p2);
            if ((r -= wLeaf) < 0f) return LeafBlades(p2);
            if ((r -= wHammer) < 0f) return Hammer(p2);
            if ((r -= wSummon) < 0f) return Summon(p2);
            return Burrow();
        }

        /// 現在のアクションの姿勢を保ったまま待つ（進行度を巻き戻さない）
        IEnumerator Hold(float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime) yield return null;
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

            // ボスの足元から各目標へ根が這い進む（演出のみ。噴き出す直前に到達する）
            var crawls = new List<GameObject>();
            Vector3 origin = Flat(boss.AttackOrigin);
            foreach (var t in targets)
            {
                Vector3 toTarget = Flat(t - origin);
                Vector3 start = origin + (toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward) * (p2 ? 2f : 0.7f);
                var crawl = p2
                    ? RootCrawl.Create(start, t, delay * 0.9f, 0.7f, 0.3f, 1f, VfxLibrary.I.RootMaterial)
                    : RootCrawl.Create(start, t, delay * 0.9f, 0.35f, 0.15f, 0.5f, VfxLibrary.I.RootMaterial);
                temporary.Add(crawl.gameObject);
                crawls.Add(crawl.gameObject);
            }

            yield return Act(BossAction.RootCast, delay);
            foreach (var m in marks) Remove(m);

            var spikes = new List<GameObject>(crawls);
            foreach (var t in targets)
            {
                spikes.Add(SpawnSpikes(t, radius));
                if (Flat(player.transform.position - t).magnitude <= radius && player.transform.position.y < 2.5f)
                    player.TakeHit(rootDamage);
            }
            yield return Hold(0.6f);
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
                // 根元が太く先が尖った根。外側のものほど外へ反らせて、地面を割って噴き出したように見せる
                Vector2 o = Random.insideUnitCircle * radius * 0.8f;
                float h = Random.Range(1.5f, 3f);
                Vector3 outward = new Vector3(o.x, 0f, o.y) / Mathf.Max(0.01f, radius);
                Vector3 bend = (outward * 0.8f + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f))) * h * 0.35f;
                RootCrawl.CreateSpike(root.transform, new Vector3(o.x, 0f, o.y), h, Random.Range(0.18f, 0.32f), bend, lib.RootMaterial);
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

            yield return Hold(0.6f);
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
                marks.Add(CircleTelegraph(p, 0.8f, false));
            }
            yield return Act(BossAction.Summon, 0.9f);
            foreach (var m in marks) Remove(m);
            foreach (var p in points) Scarecrow.Spawn(p, transform.rotation);
            yield return Hold(0.3f);
        }

        // ---------- 格闘（近接時のみ・ノックバック） ----------

        IEnumerator Melee(bool p2)
        {
            nextMeleeTime = Time.time + meleeCooldown;
            var player = PlayerHealth.I;
            float range = MeleeRange(p2);

            // 少女は素早く向き直る
            if (!p2)
            {
                for (float t = 0f; t < 0.12f; t += Time.deltaTime)
                {
                    boss.FaceDirection(player.transform.position - transform.position, 900f);
                    yield return null;
                }
            }

            // 正面の扇状の範囲（矩形で近似）を短く予兆
            Vector3 origin = Flat(transform.position);
            Quaternion rot = Quaternion.LookRotation(Flat(transform.forward));
            var mark = RectTelegraph(origin, rot, range * 2f, range);
            yield return Act(BossAction.MeleeWindup, p2 ? meleeWindup2 : meleeWindup1);
            yield return Act(BossAction.MeleeStrike, meleeStrikeTime);
            Remove(mark);

            Vector3 to = Flat(player.transform.position - transform.position);
            if (to.magnitude <= range + 0.5f && Vector3.Angle(transform.forward, to) <= meleeAngle && player.TakeHit(meleeDamage))
            {
                Vector3 away = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
                player.GetComponent<PlayerController>().Knockback(away * meleeKnockback + Vector3.up * meleeKnockUp);
            }
            yield return Hold(0.45f);
        }

        // ---------- 潜行：地中に潜り、下から根とともに突き出す（フェーズ1のみ） ----------

        IEnumerator Burrow()
        {
            nextBurrowTime = Time.time + burrowCooldown;
            var player = PlayerHealth.I;
            var lib = VfxLibrary.I;
            float s = boss.SizeScale;
            float depth = burrowDepth * s;
            var trail = new List<GameObject>();

            // 1. 地面に潜る
            Vector3 start = Flat(transform.position);
            boss.Rig.Play(BossAction.BurrowDown);
            for (float t = 0f; t < burrowSinkTime; t += Time.deltaTime)
            {
                float k = t / burrowSinkTime;
                boss.Rig.Progress = k;
                transform.position = start + Vector3.down * depth * k * k;
                yield return null;
            }
            boss.IsBurrowed = true;

            // 2. 土を盛り上げ、根の跡を残しながらプレイヤーへ向かって地中を進む
            boss.Rig.Play(BossAction.Underground);
            var mound = Prim.Create(PrimitiveType.Sphere, "BurrowMound", null, start, new Vector3(1.8f, 0.6f, 1.8f) * s, lib.spike);
            temporary.Add(mound);
            Vector3 pos = start;
            Vector3 lastTrail = start;
            for (float t = 0f; t < burrowMaxTravelTime; t += Time.deltaTime)
            {
                Vector3 to = Flat(player.transform.position) - pos;
                if (to.magnitude < 0.3f) break;
                pos += to.normalized * Mathf.Min(to.magnitude, burrowTravelSpeed * Time.deltaTime);
                boss.FaceDirection(to, 720f);
                transform.position = pos + Vector3.down * depth;
                mound.transform.position = pos + Vector3.up * (0.1f + Mathf.Sin(t * 25f) * 0.05f);

                if ((pos - lastTrail).magnitude > 0.8f)
                {
                    Vector3 seg = pos - lastTrail;
                    var piece = Prim.Create(PrimitiveType.Cylinder, "BurrowTrail", null, (pos + lastTrail) * 0.5f + Vector3.up * 0.05f,
                        new Vector3(0.35f * s, seg.magnitude * 0.55f, 0.35f * s), lib.RootMaterial, false, Quaternion.FromToRotation(Vector3.up, seg));
                    temporary.Add(piece);
                    trail.Add(piece);
                    lastTrail = pos;
                }
                yield return null;
            }

            // 3. 噴き出す地点が決まる（予兆）。土の盛り上がりが震える
            float radius = burrowRadius * s;
            var mark = CircleTelegraph(pos, radius);
            for (float t = 0f; t < burrowLockTime; t += Time.deltaTime)
            {
                mound.transform.position = pos + Vector3.up * 0.1f + Random.insideUnitSphere * 0.08f;
                mound.transform.localScale = new Vector3(1.8f, 0.6f + t / burrowLockTime * 0.5f, 1.8f) * s;
                yield return null;
            }
            Remove(mark);
            Remove(mound);

            // 4. 根とともに突き出す
            boss.IsBurrowed = false;
            var spikes = SpawnSpikes(pos, radius);
            Vector3 toPlayer = Flat(player.transform.position - pos);
            if (toPlayer.magnitude <= radius && player.transform.position.y < 2.5f && player.TakeHit(burrowDamage))
            {
                Vector3 away = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : transform.forward;
                player.GetComponent<PlayerController>().Knockback(away * burrowKnockback + Vector3.up * burrowKnockUp);
            }
            boss.Rig.Play(BossAction.Emerge);
            for (float t = 0f; t < burrowEmergeTime; t += Time.deltaTime)
            {
                float k = t / burrowEmergeTime;
                boss.Rig.Progress = k;
                transform.position = pos + Vector3.down * depth * (1f - k) * (1f - k);
                yield return null;
            }
            transform.position = pos;
            boss.Rig.Progress = 1f;

            // 5. 突き出した直後は隙ができる
            yield return Hold(burrowRecoverTime);
            Remove(spikes);
            foreach (var piece in trail) Remove(piece);
        }

        // ---------- 予兆表示 ----------

        /// dangerous = 攻撃範囲かどうか（HUDの危険表示の対象。召喚地点などは false）
        GameObject CircleTelegraph(Vector3 center, float radius, bool dangerous = true)
        {
            var go = Prim.Create(PrimitiveType.Cylinder, "Telegraph", null, center + Vector3.up * 0.03f,
                new Vector3(radius * 2f, 0.01f, radius * 2f), VfxLibrary.I.telegraph);
            if (dangerous) DangerZone.AddCircle(go, center, radius);
            temporary.Add(go);
            return go;
        }

        GameObject RectTelegraph(Vector3 origin, Quaternion rot, float width, float length)
        {
            var go = Prim.Create(PrimitiveType.Cube, "Telegraph", null, origin + rot * Vector3.forward * length * 0.5f + Vector3.up * 0.03f,
                new Vector3(width, 0.02f, length), VfxLibrary.I.telegraph, false, rot);
            DangerZone.AddRect(go, origin, rot, width, length);
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
