using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// ステージ7のラスボス「原初の魔法少女」。フェーズ制 × 弱点型（4段階）
    /// ・フェーズ1：傷一つない姿。「星落とし」の後に力尽きている間だけ、胸の弱点が露出する
    /// ・フェーズ2：衣装が汚れ、傷がついた姿。弱点の仕組みはフェーズ1と同じ
    /// ・フェーズ3：片腕を失う（Phase3 のモデルに替わる）。失った腕の付け根が薄赤く光り、常に弱点になる。
    ///   動くたびに体から彗星の軌道のような光が伸びる
    /// ・フェーズ4：体中にひびが入り、宙に浮いて一定の位置に固定される。
    ///   降ってくる隕石を壊すまでダメージが通らず、壊すと本体のすべてが弱点になる
    public class PrimordialBoss : BossBase
    {
        public static PrimordialBoss I { get; private set; }

        [Header("見た目（PrimordialRig を付けたオブジェクト。モデルはその子）")]
        public GameObject form1;   // フェーズ1・2（Avatar）
        public GameObject form3;   // フェーズ3・4（Phase3）

        [Header("フェーズ（最大HPに対する割合で次へ）")]
        public float[] phaseThresholds = { 0.75f, 0.5f, 0.25f };
        public float phaseChangeTime = 2.6f;
        [Tooltip("フェーズ2で衣装を汚す色（元の色に掛ける）")]
        public Color phase2Tint = new Color(0.72f, 0.66f, 0.68f);

        [Header("移動（フェーズ1〜3）")]
        public float moveSpeed = 3.2f;
        public float keepDistance = 12f;
        public float turnSpeed = 200f;
        public float arenaRadius = 34f;
        public float blinkCooldown = 7f;

        [Header("フェーズ4（宙に浮いて固定）")]
        public Vector3 floatPoint = new Vector3(0f, 0f, 6f);
        public float floatHeight = 3.2f;

        [Header("弱点")]
        [Tooltip("フェーズ1・2：力尽きている間に露出する胸の弱点")]
        public float coreMultiplier = 2.5f;
        public Vector3 coreOffset = new Vector3(0f, 0.02f, 0.1f);
        public float coreRadius = 0.16f;
        public float exposeDuration = 5f;
        [Tooltip("フェーズ3：失った腕の付け根")]
        public float stumpMultiplier = 2f;
        public float stumpRadius = 0.15f;
        [Tooltip("フェーズ4：隕石を壊した後、本体のすべてが弱点になる")]
        public float vulnerableMultiplier = 1.6f;
        public float vulnerableDuration = 9f;

        [Header("フェーズ4の隕石")]
        public float gateMeteorHp = 650f;
        public float gateFallTime = 24f;
        public float gateHeight = 45f;
        public float gateBlastRadius = 10f;
        public float gateBlastDamage = 60f;

        [Header("マテリアル")]
        public Material telegraphMaterial;   // 攻撃の予兆（ピンク）
        public Material cometMaterial;       // 彗星の光・軌道
        public Material burstMaterial;       // 着弾の光（透過）
        public Material circleMaterial;      // 魔法陣
        public Material meteorMaterial;      // 隕石
        public Material meteorHotMaterial;   // 着地して赤熱した隕石
        public Material familiarMaterial;    // 星の使い魔
        public Material weakMaterial;        // 弱点の光

        [Header("台詞")]
        public string[] introLines = { "……来てしまったのね。", "ここまで、たくさん傷ついてきたでしょう？", "お願い、帰って。もう誰も傷つけたくないの。" };
        public string[] phase1Lines = { "あなたたちの武器、とても痛いわ。…それでいいの。", "空を見て。あの日も、こんな色だった。", "私はただ、星と一緒に来ただけなのに。", "まだ間に合うわ。引き返して。", "ねえ、あなたの名前を聞いてもいい？", "この街にも、たくさんの人がいたのよね。" };
        public string[] phase2Lines = { "服が、汚れちゃった。", "痛い…ねえ、あなたも痛いでしょう？", "私が消えれば、みんな救われるの？", "あの子たちも、こうやって消えていったのね。", "どうして、そんなに強くいられるの。" };
        public string[] phase3Lines = { "……いや。", "止まって…お願い…", "まだ…消えたくない…", "どうして…" };
        public string[] phase4Lines = { "…………", "ごめんなさい", "もう、戻れない" };
        public string[] transitionLines = { "", "…どうして、そこまでして。", "あ……腕が……", "……星が、呼んでる。" };
        public string[] exposedLines = { "はぁ…はぁ…", "少し…疲れちゃった", "待って…" };
        public string[] deathLines = { "…ありがとう。", "…やっと、眠れる。" };

        public bool IsTransitioning { get; private set; }
        /// フェーズ1・2：胸の弱点が露出している
        public bool IsExposed { get; private set; }
        /// フェーズ4：隕石を壊して、本体が無防備になっている
        public bool IsVulnerable { get; private set; }
        public GateMeteor CurrentGate { get; private set; }

        public PrimordialRig Rig => Phase >= 3 ? rig3 : rig1;
        /// 攻撃の起点（胸の高さ）
        public Vector3 CastPoint => Rig != null && Rig.Chest != null ? Rig.Chest.position : transform.position + Vector3.up * 1.2f;

        PrimordialRig rig1, rig3;
        PrimordialAttacks attacks;
        BossVoice voice;
        GameObject core;
        GameObject stump;
        readonly List<TrailRenderer> cometTrails = new List<TrailRenderer>();
        float nextLine;
        float nextBlink;
        float strafeSign = 1f;
        float nextStrafeFlip;
        bool started;

        void Reset()
        {
            bossName = "原初の魔法少女";
            stageTitle = "α版　ステージ7：隕石落下後のクレーター（ラスボス）";
            loadoutHint = "魔法少女が力尽きたときだけ、胸の弱点が露出する。\n腕を失えばその付け根が、最後は降ってくる隕石を壊したときだけ本体が無防備になる。\nソロ出撃：HPが0になると即死。";
        }

        void Awake()
        {
            I = this;
            Hp = maxHp;
            attacks = GetComponent<PrimordialAttacks>();
            voice = GetComponent<BossVoice>();
            rig1 = form1 != null ? form1.GetComponent<PrimordialRig>() : null;
            rig3 = form3 != null ? form3.GetComponent<PrimordialRig>() : null;
        }

        void Start()
        {
            if (form3 != null) form3.SetActive(false);
            core = CreateCore();
            if (rig3 != null)
                foreach (var t in new[] { rig3.Hips, rig3.HandR, rig3.HandL, rig3.Head })
                    if (t != null)
                    {
                        var trail = Stage7Fx.AddTrail(t.gameObject, cometMaterial, 0.18f, 0.5f);
                        trail.emitting = false;
                        cometTrails.Add(trail);
                    }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Stage7Fx.ClearSpawned();
        }

        // ---------- 弱点 ----------

        /// フェーズ1・2の胸の弱点。普段は隠れていて、力尽きたときだけ光って当たるようになる
        GameObject CreateCore()
        {
            var chest = rig1 != null ? rig1.Chest : null;
            if (chest == null) return null;
            var go = new GameObject("WeakCore");
            go.layer = Layers.Enemy;
            go.transform.SetParent(chest, false);
            go.transform.position = chest.position + form1.transform.rotation * coreOffset;
            var col = go.AddComponent<SphereCollider>();
            col.radius = coreRadius / Mathf.Max(0.0001f, chest.lossyScale.x);
            go.AddComponent<WeakPoint>().Init(this, coreMultiplier);
            var glow = Prim.Create(PrimitiveType.Sphere, "Glow", go.transform, Vector3.zero, Vector3.one * col.radius * 2f, weakMaterial);
            glow.layer = Layers.Enemy;
            go.SetActive(false);
            return go;
        }

        /// フェーズ3の失った腕の付け根。モデルの光る付け根（P3_WeakpointShoulder）の位置に、いちばん近い骨の子として付ける。
        /// 表示されてからでないと位置がわからないので、フェーズ3の姿に替わった直後に作る
        GameObject CreateStump()
        {
            if (form3 == null) return null;
            var mark = FindDeep(form3.transform, "P3_WeakpointShoulder") ?? FindDeep(form3.transform, "P3_WeakpointCore");
            var rend = mark != null ? mark.GetComponent<Renderer>() : null;
            var bones = new List<Transform>();
            foreach (var n in new[] { "shoulder.L", "shoulder.R", "upper_arm.L", "upper_arm.R" })
            {
                var b = FindDeep(form3.transform, n);
                if (b != null) bones.Add(b);
            }
            if (rig3 != null && rig3.Chest != null) bones.Add(rig3.Chest);
            if (bones.Count == 0) return null;

            Vector3 pos = rend != null && rend.bounds.size.sqrMagnitude > 1e-6f ? rend.bounds.center : bones[0].position;
            Transform parent = bones[0];
            foreach (var b in bones)
                if ((b.position - pos).sqrMagnitude < (parent.position - pos).sqrMagnitude) parent = b;

            var go = new GameObject("WeakStump");
            go.layer = Layers.Enemy;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var col = go.AddComponent<SphereCollider>();
            col.radius = stumpRadius / Mathf.Max(0.0001f, parent.lossyScale.x);
            go.AddComponent<WeakPoint>().Init(this, stumpMultiplier);
            return go;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// 力尽きてうなだれ、胸の弱点を露出する（星落としの後）
        public IEnumerator ExposeRoutine()
        {
            IsExposed = true;
            if (core != null) core.SetActive(true);
            Rig.Play(PrimordialPose.Exhausted);
            voice.Say(exposedLines[Random.Range(0, exposedLines.Length)], 2.5f);
            for (float t = 0f; t < exposeDuration && IsExposed; t += Time.deltaTime) yield return null;
            CloseCore();
        }

        void CloseCore()
        {
            IsExposed = false;
            if (core != null) core.SetActive(false);
        }

        // ---------- HUD ----------

        public override string StatusText
        {
            get
            {
                string s = $"フェーズ{Phase}";
                if (IsTransitioning) s += "　<崩壊中>";
                if (IsExposed) s += "　<弱点露出！胸を狙え>";
                if (Phase == 3 && !IsTransitioning) s += "　弱点：失った腕の付け根";
                if (Phase == 4 && !IsTransitioning)
                    s += IsVulnerable ? "　<無防備！全身が弱点>" : "　<隕石を壊すまで無敵>";
                return s;
            }
        }

        public override Color HpBarColor =>
            IsVulnerable || IsExposed ? new Color(1f, 0.55f, 0.75f) : new Color(0.85f, 0.45f, 0.7f);

        public override float[] PhaseMarkers => phaseThresholds;

        // ---------- 進行 ----------

        void Update()
        {
            if (!GameManager.IsPlaying || IsDead) return;
            if (!started)
            {
                started = true;
                StartCoroutine(Intro());
            }

            var player = PlayerHealth.I;
            if (Rig != null && player != null) Rig.LookTarget = player.Center + Vector3.up * 0.4f;

            if (Phase == 4)
            {
                // 宙に浮いて一定の位置に固定。体だけプレイヤーへ向ける
                if (player != null) FaceDirection(player.transform.position - transform.position, turnSpeed * 0.5f);
            }
            else if (!IsTransitioning && !IsExposed && !attacks.IsBusy) Move(player);

            bool trailOn = Phase >= 3 && !IsDead;
            foreach (var t in cometTrails) if (t != null) t.emitting = trailOn;

            Chatter();
        }

        IEnumerator Intro()
        {
            nextLine = Time.time + 12f;
            foreach (var line in introLines)
            {
                voice.Say(line, 3.2f);
                yield return new WaitForSeconds(3.4f);
            }
        }

        /// 戦闘中もたくさん話しかける。フェーズ3からは口数が減る
        void Chatter()
        {
            if (Time.time < nextLine || IsTransitioning) return;
            string[] pool = Phase switch { 1 => phase1Lines, 2 => phase2Lines, 3 => phase3Lines, _ => phase4Lines };
            if (pool.Length > 0) voice.Say(pool[Random.Range(0, pool.Length)], 3f);
            nextLine = Time.time + (Phase <= 2 ? Random.Range(8f, 13f) : Random.Range(18f, 28f));
        }

        void Move(PlayerHealth player)
        {
            if (player == null || !player.IsAlive) return;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            FaceDirection(to, turnSpeed);
            Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

            // 一定の距離を保ちながら、横へ回り込む
            if (Time.time >= nextStrafeFlip)
            {
                strafeSign = Random.value < 0.5f ? -1f : 1f;
                nextStrafeFlip = Time.time + Random.Range(2.5f, 5f);
            }
            Vector3 move = Vector3.Cross(Vector3.up, dir) * strafeSign * 0.6f;
            if (dist > keepDistance + 2f) move += dir;
            else if (dist < keepDistance - 3f) move -= dir;
            float speed = moveSpeed * (Phase >= 3 ? 1.35f : 1f);
            Vector3 next = transform.position + move.normalized * speed * Time.deltaTime;

            // 近づかれすぎたら、星屑を散らしてふっと離れる
            if (dist < 5f && Time.time >= nextBlink)
            {
                nextBlink = Time.time + blinkCooldown;
                Vector2 r = Random.insideUnitCircle.normalized;
                next = player.transform.position + new Vector3(r.x, 0f, r.y) * keepDistance;
                if (VfxLibrary.I != null) VfxLibrary.Burst(CastPoint, 1.5f, burstMaterial, 0.3f);
            }

            Vector3 flat = new Vector3(next.x, 0f, next.z);
            if (flat.magnitude > arenaRadius) flat = flat.normalized * arenaRadius;
            transform.position = new Vector3(flat.x, transform.position.y, flat.z);
        }

        public void FaceDirection(Vector3 dir, float degPerSec)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degPerSec * Time.deltaTime);
        }

        // ---------- ダメージ ----------

        public override float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead || IsTransitioning || !GameManager.IsPlaying) return 0f;
            if (Phase == 4 && !IsVulnerable) return 0f;

            float before = Hp;
            float scaled = Phase == 4 ? amount * vulnerableMultiplier : amount;
            Hp = Mathf.Max(0f, Hp - scaled);

            // 次のフェーズの境目を飛び越えないよう、境目で止めて移行する
            if (Phase < 4 && Phase - 1 < phaseThresholds.Length)
            {
                float line = maxHp * phaseThresholds[Phase - 1];
                if (Hp <= line)
                {
                    Hp = line;
                    StartCoroutine(PhaseChange(Phase + 1));
                }
            }
            else if (Hp <= 0f) Die();

            float dealt = before - Hp;
            if (Phase == 4 && dealt > 0f) CombatEvents.ReportWeakPointHit();
            return dealt;
        }

        IEnumerator PhaseChange(int next)
        {
            IsTransitioning = true;
            attacks.CancelAll();
            CloseCore();
            Rig.Play(PrimordialPose.Hurt);
            if (next - 1 < transitionLines.Length) voice.Say(transitionLines[next - 1], phaseChangeTime + 1f);
            if (VfxLibrary.I != null) VfxLibrary.Burst(CastPoint, 3f, burstMaterial, 0.6f);

            yield return new WaitForSeconds(phaseChangeTime * 0.5f);

            switch (next)
            {
                case 2:
                    TintForm(form1, phase2Tint);
                    break;
                case 3:
                    // 片腕を失った姿へ
                    if (VfxLibrary.I != null) VfxLibrary.Burst(CastPoint, 4f, burstMaterial, 0.8f);
                    form1.SetActive(false);
                    form3.SetActive(true);
                    rig3.Play(PrimordialPose.Hurt);
                    yield return null;   // 表示されて位置が決まってから弱点を付ける
                    stump = CreateStump();
                    break;
                case 4:
                    if (stump != null) stump.SetActive(false);
                    TintForm(form3, new Color(0.85f, 0.8f, 0.85f));
                    yield return RiseToFloatPoint();
                    break;
            }

            yield return new WaitForSeconds(phaseChangeTime * 0.5f);
            Phase = next;
            Rig.Play(next == 4 ? PrimordialPose.Float : PrimordialPose.Idle);
            IsTransitioning = false;
            attacks.Delay(1.2f);
            nextLine = Time.time + 6f;
            if (next == 4) StartCoroutine(GateLoop());
        }

        /// 体中にひびが入った姿で、クレーターの中心の上空へ浮き上がる
        IEnumerator RiseToFloatPoint()
        {
            Vector3 from = transform.position;
            Vector3 to = new Vector3(floatPoint.x, from.y, floatPoint.z);
            float fromHeight = Rig.extraHeight;
            for (float t = 0f; t < 1.5f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.5f);
                transform.position = Vector3.Lerp(from, to, k);
                Rig.extraHeight = Mathf.Lerp(fromHeight, floatHeight, k);
                yield return null;
            }
            transform.position = to;
            Rig.extraHeight = floatHeight;
        }

        /// 元の色に掛けて汚す（実行中だけのコピーを変えるので、元のマテリアルは変わらない）
        static void TintForm(GameObject form, Color tint)
        {
            if (form == null) return;
            foreach (var r in form.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.materials)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                    else if (m.HasProperty("_Color")) m.color *= tint;
                }
        }

        // ---------- フェーズ4：降ってくる隕石 ----------

        IEnumerator GateLoop()
        {
            while (!IsDead)
            {
                yield return new WaitForSeconds(2f);
                if (IsDead) yield break;

                var player = PlayerHealth.I;
                Vector3 landing = player != null ? Vector3.Lerp(new Vector3(transform.position.x, 0f, transform.position.z), player.transform.position, 0.6f) : Vector3.zero;
                landing.y = Stage7Fx.GroundHeight(landing);
                CurrentGate = GateMeteor.Spawn(landing, gateHeight, this);
                var gate = CurrentGate;
                while (!gate.Resolved && !IsDead) yield return null;
                CurrentGate = null;
                if (IsDead) yield break;

                if (gate.WasDestroyed)
                {
                    // 隕石を壊されて、無防備になる
                    IsVulnerable = true;
                    voice.Say("…あ", 2f);
                    Rig.Play(PrimordialPose.Exhausted);
                    attacks.Delay(vulnerableDuration);
                    yield return new WaitForSeconds(vulnerableDuration);
                    IsVulnerable = false;
                    if (!IsDead) Rig.Play(PrimordialPose.Float);
                }
                else voice.Say("…ごめんね", 2.5f);
            }
        }

        // ---------- 撃破 ----------

        void Die()
        {
            IsDead = true;
            IsVulnerable = false;
            attacks.CancelAll();
            Stage7Fx.ClearSpawned();
            StartCoroutine(DeathRoutine());
            if (GameManager.I != null) GameManager.I.OnBossDefeated();
        }

        IEnumerator DeathRoutine()
        {
            Rig.Play(PrimordialPose.Dead);
            float fromHeight = Rig.extraHeight;
            voice.Say(deathLines[0], 3f);
            for (float t = 0f; t < 3f; t += Time.deltaTime)
            {
                Rig.Progress = t / 3f;
                Rig.extraHeight = Mathf.Lerp(fromHeight, 0f, t / 3f);
                yield return null;
            }
            if (deathLines.Length > 1) voice.Say(deathLines[1], 4f);
        }
    }
}
