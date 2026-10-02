using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    /// ステージ1ボス「植物に埋もれた村の魔法少女」
    /// ・瘤が残っている間はHPが回復し続ける
    /// ・瘤を壊すと根を吸収して大きくなり、被ダメージが軽減されていく
    /// ・瘤が残ったままHPを削りきると、一定時間無敵になってHPを回復する
    /// ・瘤がすべて破壊され、かつHP40%以下でフェーズ2（巨木の怪物）へ。HPは全体で共通、HP0で撃破
    public class BossController : MonoBehaviour, IDamageable
    {
        public string bossName = "植物に埋もれた村の魔法少女";
        public float maxHp = 5000f;

        [Header("瘤")]
        public float regenPerKnot = 8f;
        public float reductionPerKnot = 0.1f;
        public float growthPerKnot = 0.08f;

        [Header("瘤が残ったままHP0になったとき")]
        public float reviveDuration = 5f;
        public float reviveHealFraction = 0.3f;

        [Header("フェーズ2")]
        public float phase2Threshold = 0.4f;
        public float phase2ExtraReduction = 0.15f;
        public float maxReduction = 0.75f;
        public float transformDuration = 3f;
        public Transform phase2Point;

        [Header("見た目")]
        public GameObject girlVisual;
        public GameObject phase2Visual;
        public GameObject[] rootWraps;

        [Header("移動（フェーズ1）")]
        public float moveSpeed = 2.2f;
        public float keepDistance = 6f;
        public float turnSpeed = 120f;

        public float Hp { get; private set; }
        public int Phase { get; private set; } = 1;
        public bool IsReviving { get; private set; }
        public bool IsTransforming { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsAlive => !IsDead;
        public int KnotsTotal => knots.Count;
        public int KnotsRemaining { get; private set; }
        public bool IsRegenerating => KnotsRemaining > 0 && !IsDead;
        public float DamageReduction =>
            Mathf.Min(maxReduction, (KnotsTotal - KnotsRemaining) * reductionPerKnot + (Phase == 2 ? phase2ExtraReduction : 0f));

        public BossRigBase Rig => Phase == 2 ? treeRig : girlRig;
        /// 攻撃の起点（フェーズ2は巨木の正面）
        public Vector3 AttackOrigin => Phase == 2 ? transform.position + transform.forward * 3f : transform.position;

        readonly List<Knot> knots = new List<Knot>();
        BossAttacks attacks;
        BossVoice voice;
        GirlRig girlRig;
        TreeRig treeRig;
        Vector3 girlBaseScale;

        void Awake()
        {
            Hp = maxHp;
            attacks = GetComponent<BossAttacks>();
            voice = GetComponent<BossVoice>();
            girlRig = girlVisual.GetComponent<GirlRig>();
            treeRig = phase2Visual.GetComponent<TreeRig>();
        }

        void Start()
        {
            knots.AddRange(FindObjectsByType<Knot>(FindObjectsSortMode.None));
            foreach (var k in knots) k.Bind(this);
            KnotsRemaining = knots.Count;
            girlBaseScale = girlVisual.transform.localScale;
            phase2Visual.SetActive(false);
            foreach (var w in rootWraps) w.SetActive(false);
        }

        void Update()
        {
            if (!GameManager.IsPlaying || IsDead) return;

            if (IsRegenerating && !IsReviving)
                Hp = Mathf.Min(maxHp, Hp + regenPerKnot * KnotsRemaining * Time.deltaTime);

            if (Phase == 1 && !IsTransforming && KnotsRemaining == 0 && Hp <= maxHp * phase2Threshold)
                StartCoroutine(TransformRoutine());

            if (Phase == 1 && !IsTransforming && !IsReviving && !attacks.IsBusy) MoveTowardPlayer();
        }

        void MoveTowardPlayer()
        {
            var player = PlayerHealth.I;
            if (player == null || !player.IsAlive) return;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            FaceDirection(to, turnSpeed);
            float scale = girlVisual.transform.localScale.y / girlBaseScale.y;
            if (to.magnitude > keepDistance) transform.position += to.normalized * moveSpeed * scale * Time.deltaTime;
        }

        public void FaceDirection(Vector3 dir, float degPerSec)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), degPerSec * Time.deltaTime);
        }

        public float TakeDamage(float amount, Vector3 hitPoint)
        {
            if (IsDead || IsReviving || IsTransforming || !GameManager.IsPlaying) return 0f;

            float before = Hp;
            Hp = Mathf.Max(0f, Hp - amount * (1f - DamageReduction));
            if (Hp <= 0f)
            {
                if (KnotsRemaining > 0) StartCoroutine(ReviveRoutine());
                else if (Phase == 1) Hp = 1f; // 次のフレームでフェーズ2へ
                else Die();
            }
            return before - Hp;
        }

        public void OnKnotDestroyed(Knot knot)
        {
            KnotsRemaining--;
            int destroyed = KnotsTotal - KnotsRemaining;
            if (destroyed - 1 < rootWraps.Length) rootWraps[destroyed - 1].SetActive(true);
            girlVisual.transform.localScale = girlBaseScale * (1f + growthPerKnot * destroyed);
            VfxLibrary.RootLine(knot.transform.position, transform.position + Vector3.up);
            voice.Say(KnotsRemaining == 0 ? "イタイ…イタイ…" : "イタイ", 2f);
        }

        IEnumerator ReviveRoutine()
        {
            IsReviving = true;
            attacks.CancelAll();
            girlRig.Play(BossAction.Revive);
            voice.Say("ゴハン…ゴハン…", reviveDuration);
            float target = maxHp * reviveHealFraction;
            for (float t = 0f; t < reviveDuration; t += Time.deltaTime)
            {
                Hp = Mathf.Lerp(0f, target, t / reviveDuration);
                girlRig.Progress = t / reviveDuration;
                yield return null;
            }
            Hp = target;
            girlRig.Play(BossAction.None);
            IsReviving = false;
            attacks.Delay(1f);
        }

        IEnumerator TransformRoutine()
        {
            IsTransforming = true;
            attacks.CancelAll();
            girlRig.Play(BossAction.Transform);
            voice.Say("タスケテ…タスケテ…", transformDuration + 1f);

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 endPos = phase2Point != null ? phase2Point.position : startPos;
            Quaternion endRot = phase2Point != null ? phase2Point.rotation : startRot;
            Vector3 girlScale = girlVisual.transform.localScale;

            // 少女がもがきながら巨木へ引きずり込まれる
            float half = transformDuration * 0.5f;
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float k = t / half;
                transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k * k), Quaternion.Slerp(startRot, endRot, k));
                yield return null;
            }
            transform.SetPositionAndRotation(endPos, endRot);

            // 少女が崩れ、巨木の怪物が現れる
            phase2Visual.SetActive(true);
            phase2Visual.transform.localScale = Vector3.one * 0.01f;
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float k = t / half;
                girlVisual.transform.localScale = girlScale * (1f - k);
                phase2Visual.transform.localScale = Vector3.one * Mathf.Max(0.01f, k * k);
                yield return null;
            }
            phase2Visual.transform.localScale = Vector3.one;
            girlVisual.SetActive(false);

            Phase = 2;
            IsTransforming = false;
            attacks.Delay(1.5f);
        }

        void Die()
        {
            IsDead = true;
            attacks.CancelAll();
            treeRig.Play(BossAction.Dead);
            voice.Say("オカー…サ…ン", 5f);
            if (GameManager.I != null) GameManager.I.OnBossDefeated();
        }
    }
}
