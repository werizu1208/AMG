using System.Collections.Generic;
using UnityEngine;

namespace AMG
{
    public enum PrimordialPose
    {
        Idle,
        CallMeteor,   // 右手を空へ掲げる（流星群）
        Point,        // 右手で狙う（彗星の槍・突進）
        RaiseBoth,    // 両手を掲げる（召喚）
        Charge,       // 両手を掲げて浮き上がる（星落とし）
        Slam,         // 両手を振り下ろす
        Exhausted,    // 力尽きてうなだれる（弱点が露出）
        Hurt,         // 崩れていく（フェーズ移行）
        Float,        // 宙に浮いて両手を広げる（フェーズ4）
        Dead,
    }

    /// 原初の魔法少女の見た目（Avatar・Phase3 のモデル）を、骨を直接回して動かす簡易アニメーション。
    /// このコンポーネントを付けたオブジェクトの向き（+Z が正面）を体の向きとして使う。モデルはその子に置く。
    /// 毎フレーム骨を元の姿勢（モデルの Aポーズ）に戻してから、ワールド空間の回転を足すので、骨の軸の向きに依存しない
    public class PrimordialRig : MonoBehaviour
    {
        [Header("浮遊")]
        public float hover = 0.12f;
        public float bobAmplitude = 0.04f;
        public float bobSpeed = 1.6f;
        [Header("追従の速さ")]
        public float follow = 8f;

        public PrimordialPose Pose { get; private set; }
        /// 現在の動作の進行度（0〜1）
        [HideInInspector] public float Progress;
        /// 見る・狙う先（ワールド座標）
        [HideInInspector] public Vector3 LookTarget;
        /// 星落としやフェーズ4で、さらに浮き上がる高さ
        [HideInInspector] public float extraHeight;

        public Transform Hips { get { EnsureBones(); return hips; } }
        public Transform Chest { get { EnsureBones(); return chest != null ? chest : spine; } }
        public Transform Head { get { EnsureBones(); return head; } }
        public Transform HandR { get { EnsureBones(); return handR; } }
        public Transform HandL { get { EnsureBones(); return handL; } }

        Transform hips, spine, chest, head, armUL, armLL, handL, armUR, armLR, handR;
        readonly List<(Transform bone, Quaternion local)> bind = new List<(Transform, Quaternion)>();
        Transform model;
        Vector3 modelBasePos;
        float lean, roll, headDown, height, shake;
        Vector3 armL = Vector3.down, armR = Vector3.down;
        float wL, wR;
        bool bonesReady;

        void Awake()
        {
            EnsureBones();
        }

        /// 骨を探して元の姿勢を覚える（非表示のまま参照されたときにも使えるよう、初回に一度だけ行う）
        void EnsureBones()
        {
            if (bonesReady) return;
            bonesReady = true;
            model = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (model != null) modelBasePos = model.localPosition;

            var anim = GetComponentInChildren<Animator>(true);
            if (anim != null && anim.isHuman)
            {
                hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                spine = anim.GetBoneTransform(HumanBodyBones.Spine);
                chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                head = anim.GetBoneTransform(HumanBodyBones.Head);
                armUL = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                armLL = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                armUR = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                armLR = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
                handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
            }
            // 人型として読み込んでいないモデル（Phase3 など）は骨の名前で探す
            hips ??= Find("hips", "Hips", "J_Bip_C_Hips");
            spine ??= Find("spine", "Spine", "J_Bip_C_Spine");
            chest ??= Find("chest", "Chest", "upper_chest", "J_Bip_C_Chest");
            head ??= Find("head", "Head", "J_Bip_C_Head");
            armUL ??= Find("upper_arm.L", "LeftUpperArm", "J_Bip_L_UpperArm");
            armLL ??= Find("lower_arm.L", "LeftLowerArm", "J_Bip_L_LowerArm");
            handL ??= Find("hand.L", "LeftHand", "J_Bip_L_Hand");
            armUR ??= Find("upper_arm.R", "RightUpperArm", "J_Bip_R_UpperArm");
            armLR ??= Find("lower_arm.R", "RightLowerArm", "J_Bip_R_LowerArm");
            handR ??= Find("hand.R", "RightHand", "J_Bip_R_Hand");

            // アニメーションは使わないので、骨を上書きされないよう止めておく
            if (anim != null) anim.enabled = false;

            foreach (var b in new[] { hips, spine, chest, head, armUL, armLL, handL, armUR, armLR, handR })
                if (b != null) bind.Add((b, b.localRotation));
            height = hover;
        }

        Transform Find(params string[] names)
        {
            foreach (var n in names)
            {
                var t = FindDeep(transform, n);
                if (t != null) return t;
            }
            return null;
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

        public void Play(PrimordialPose pose)
        {
            Pose = pose;
            Progress = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            EnsureBones();

            // ---- 目標値 ----
            float tLean = 0f, tRoll = 0f, tHeadDown = 0f, tHeight = hover, tShake = 0f;
            Vector3 tArmL = new Vector3(-0.35f, -1f, 0.05f), tArmR = new Vector3(0.35f, -1f, 0.05f);
            float tWL = 0f, tWR = 0f;
            Vector3 toTarget = transform.InverseTransformDirection(LookTarget - (Chest != null ? Chest.position : transform.position));
            float p = Mathf.Clamp01(Progress);

            switch (Pose)
            {
                case PrimordialPose.CallMeteor:
                    tArmR = new Vector3(0.15f, 1f, 0.25f); tWR = 1f;
                    tArmL = new Vector3(-0.6f, -0.5f, 0.35f); tWL = 0.5f;
                    tLean = -8f; tHeadDown = -20f;
                    break;
                case PrimordialPose.Point:
                    tArmR = toTarget.sqrMagnitude > 0.01f ? toTarget : Vector3.forward; tWR = 1f;
                    tLean = 6f;
                    break;
                case PrimordialPose.RaiseBoth:
                    tArmL = new Vector3(-0.55f, 1f, 0.2f); tArmR = new Vector3(0.55f, 1f, 0.2f); tWL = tWR = 1f;
                    tLean = -6f; tHeadDown = -15f;
                    break;
                case PrimordialPose.Charge:
                    tArmL = new Vector3(-0.3f, 1f, 0f); tArmR = new Vector3(0.3f, 1f, 0f); tWL = tWR = 1f;
                    tLean = -12f; tHeadDown = -10f;
                    break;
                case PrimordialPose.Slam:
                    tArmL = new Vector3(-0.3f, -1f, 0.7f); tArmR = new Vector3(0.3f, -1f, 0.7f); tWL = tWR = 1f;
                    tLean = 25f;
                    break;
                case PrimordialPose.Exhausted:
                    tArmL = new Vector3(-0.1f, -1f, 0.25f); tArmR = new Vector3(0.1f, -1f, 0.25f); tWL = tWR = 0.8f;
                    tLean = 32f; tHeadDown = 30f; tHeight = 0f;
                    tRoll = Mathf.Sin(Time.time * 1.3f) * 3f;
                    break;
                case PrimordialPose.Hurt:
                    tArmL = new Vector3(-0.2f, -0.6f, 0.6f); tArmR = new Vector3(0.2f, -0.6f, 0.6f); tWL = tWR = 0.7f;
                    tLean = -14f; tHeadDown = -10f; tShake = 6f;
                    break;
                case PrimordialPose.Float:
                    tArmL = new Vector3(-1f, -0.35f, 0.2f); tArmR = new Vector3(1f, -0.35f, 0.2f); tWL = tWR = 0.8f;
                    tHeadDown = 10f;
                    break;
                case PrimordialPose.Dead:
                    tArmL = new Vector3(-0.05f, -1f, 0.1f); tArmR = new Vector3(0.05f, -1f, 0.1f); tWL = tWR = 1f;
                    tLean = 55f * p; tHeadDown = 40f; tHeight = 0f;
                    break;
            }

            float k = 1f - Mathf.Exp(-follow * dt);
            lean = Mathf.Lerp(lean, tLean, k);
            roll = Mathf.Lerp(roll, tRoll, k);
            headDown = Mathf.Lerp(headDown, tHeadDown, k);
            height = Mathf.Lerp(height, tHeight, k);
            shake = Mathf.Lerp(shake, tShake, k);
            armL = Vector3.Slerp(armL, tArmL.normalized, k);
            armR = Vector3.Slerp(armR, tArmR.normalized, k);
            wL = Mathf.Lerp(wL, tWL, k);
            wR = Mathf.Lerp(wR, tWR, k);

            // ---- 適用 ----
            foreach (var (bone, local) in bind) bone.localRotation = local;

            if (model != null)
            {
                float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobAmplitude * Mathf.Clamp01(height / Mathf.Max(0.01f, hover));
                model.localPosition = modelBasePos + Vector3.up * (height + bob + extraHeight);
            }

            Vector3 right = transform.right, fwd = transform.forward;
            Quaternion bodyRot = Quaternion.AngleAxis(lean, right) * Quaternion.AngleAxis(roll, fwd);
            if (shake > 0.01f)
                bodyRot = Quaternion.Euler(Random.Range(-shake, shake), Random.Range(-shake, shake), Random.Range(-shake, shake)) * bodyRot;
            if (spine != null) spine.rotation = bodyRot * spine.rotation;

            AimArm(armUL, armLL, armL, wL);
            AimArm(armUR, armLR, armR, wR);

            if (head != null)
            {
                Vector3 headFwd = bodyRot * fwd;
                Vector3 want = LookTarget - head.position;
                if (want.sqrMagnitude > 0.01f && Pose != PrimordialPose.Dead && Pose != PrimordialPose.Exhausted)
                {
                    // 首を回しすぎないよう、正面から最大60度までにする
                    want = Vector3.RotateTowards(headFwd, want.normalized, 60f * Mathf.Deg2Rad, 0f);
                    head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(headFwd, want), 0.7f) * head.rotation;
                }
                head.rotation = Quaternion.AngleAxis(headDown, right) * head.rotation;
            }
        }

        /// 上腕を、体の向きを基準にした方向へ向ける（肘は伸ばしたまま）
        void AimArm(Transform upper, Transform lower, Vector3 localDir, float weight)
        {
            if (upper == null || lower == null || weight <= 0.001f) return;
            Vector3 current = lower.position - upper.position;
            if (current.sqrMagnitude < 1e-6f) return;
            Vector3 want = transform.TransformDirection(localDir);
            Quaternion delta = Quaternion.FromToRotation(current.normalized, want.normalized);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, delta, weight) * upper.rotation;
        }
    }
}
