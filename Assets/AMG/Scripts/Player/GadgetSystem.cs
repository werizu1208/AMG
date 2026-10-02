using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// ガジェット①：グレネード（E長押しで予測線を表示、離して投擲） / ガジェット②：回復キット（F）
    /// どちらも使用後にリチャージ時間があり、連発はできない
    public class GadgetSystem : MonoBehaviour
    {
        [Header("グレネード")]
        public float grenadeCooldown = 12f;
        public float grenadeDamage = 150f;
        public float grenadeRadius = 4.5f;
        public float grenadeFuse = 1.4f;
        public float throwSpeed = 16f;

        [Header("グレネードの転がり")]
        [Tooltip("跳ね返りの強さ（0=跳ねない / 1=ほぼ減衰なし）")]
        [Range(0f, 1f)] public float grenadeBounciness = 0.15f;
        [Tooltip("地面との摩擦（大きいほど滑らない）")]
        public float grenadeFriction = 1f;
        [Tooltip("着地後の減速（大きいほどすぐ止まる。空中には影響しない）")]
        public float grenadeGroundDrag = 3f;

        [Header("予測線")]
        public float arcTimeStep = 0.03f;
        public float arcWidth = 0.05f;

        [Header("回復キット")]
        public float medkitCooldown = 25f;     // 使用した時点から計測
        public float medkitHeal = 50f;
        public float medkitDuration = 1.5f;

        public bool IsHealing { get; private set; }
        public bool IsAimingGrenade { get; private set; }
        public float GrenadeCooldownRemaining => Mathf.Max(0f, grenadeReadyTime - Time.time);
        public float MedkitCooldownRemaining => Mathf.Max(0f, medkitReadyTime - Time.time);
        public bool GrenadeReady => GrenadeCooldownRemaining <= 0f;
        public bool MedkitReady => MedkitCooldownRemaining <= 0f;

        const float GrenadeSize = 0.25f;

        PlayerController controller;
        PlayerHealth health;
        CharacterController cc;
        float grenadeReadyTime;
        float medkitReadyTime;
        PhysicsMaterial grenadePhysics;
        LineRenderer arcLine;
        Transform arcMarker;
        readonly List<Vector3> arcPoints = new();

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            cc = GetComponent<CharacterController>();
        }

        void OnDestroy()
        {
            if (arcLine != null) Destroy(arcLine.gameObject);
            if (arcMarker != null) Destroy(arcMarker.gameObject);
        }

        void Update()
        {
            var kb = Keyboard.current;
            bool usable = kb != null && controller.CanUseWeapons;

            if (usable && GrenadeReady)
            {
                if (kb.eKey.isPressed) IsAimingGrenade = true;
                else if (IsAimingGrenade)
                {
                    IsAimingGrenade = false;
                    ThrowGrenade();
                }
            }
            else IsAimingGrenade = false;   // 回避・スタンなどで構えを解く

            if (IsAimingGrenade)
            {
                controller.FaceForward(0.15f);
                UpdateArc();
            }
            SetArcVisible(IsAimingGrenade);

            if (usable && kb.fKey.wasPressedThisFrame) UseMedkit();
        }

        void GetThrow(out Vector3 origin, out Vector3 velocity)
        {
            var camT = Camera.main.transform;
            origin = transform.position + Vector3.up * 1.5f + camT.forward * 0.6f;
            velocity = (camT.forward + Vector3.up * 0.25f).normalized * throwSpeed;
        }

        void ThrowGrenade()
        {
            grenadeReadyTime = Time.time + grenadeCooldown;
            controller.FaceForward(0.4f);
            GetThrow(out var origin, out var velocity);

            var mat = VfxLibrary.I != null ? VfxLibrary.I.grenade : null;
            var go = Prim.Create(PrimitiveType.Sphere, "Grenade", null, origin, Vector3.one * GrenadeSize, mat, true);
            go.layer = Layers.Player;
            var col = go.GetComponent<Collider>();
            col.sharedMaterial = GetGrenadePhysics();
            Physics.IgnoreCollision(col, cc);

            var rb = go.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = velocity;
            go.AddComponent<Grenade>().Init(grenadeDamage, grenadeRadius, grenadeFuse, grenadeGroundDrag);
        }

        /// Inspectorで値を変えてもすぐ反映されるよう、投げるたびに値を入れ直す
        PhysicsMaterial GetGrenadePhysics()
        {
            if (grenadePhysics == null) grenadePhysics = new PhysicsMaterial("Grenade");
            grenadePhysics.bounciness = grenadeBounciness;
            grenadePhysics.dynamicFriction = grenadeFriction;
            grenadePhysics.staticFriction = grenadeFriction;
            grenadePhysics.bounceCombine = PhysicsMaterialCombine.Minimum;
            grenadePhysics.frictionCombine = PhysicsMaterialCombine.Maximum;
            return grenadePhysics;
        }

        // ---------- 予測線 ----------

        /// 投擲時と同じ初速・重力で軌道をなぞり、最初に当たる地点（または起爆地点）までを線で表示する
        void UpdateArc()
        {
            EnsureArcObjects();
            GetThrow(out var pos, out var vel);
            Vector3 g = Physics.gravity;
            float r = GrenadeSize * 0.5f;
            float step = Mathf.Max(0.005f, arcTimeStep);

            arcPoints.Clear();
            arcPoints.Add(pos);
            Vector3 normal = Vector3.up;
            for (float t = 0f; t < grenadeFuse; t += step)
            {
                Vector3 next = pos + vel * step + 0.5f * step * step * g;
                vel += g * step;
                Vector3 d = next - pos;
                if (Physics.SphereCast(pos, r, d.normalized, out var hit, d.magnitude, Layers.ShootMask, QueryTriggerInteraction.Ignore))
                {
                    pos = hit.point;
                    normal = hit.normal;
                    arcPoints.Add(pos);
                    break;
                }
                pos = next;
                arcPoints.Add(pos);
            }

            arcLine.widthMultiplier = arcWidth;
            arcLine.positionCount = arcPoints.Count;
            for (int i = 0; i < arcPoints.Count; i++) arcLine.SetPosition(i, arcPoints[i]);

            // 着弾点に爆発範囲の円を出す
            arcMarker.SetPositionAndRotation(pos + normal * 0.03f, Quaternion.FromToRotation(Vector3.up, normal));
            arcMarker.localScale = new Vector3(grenadeRadius * 2f, 0.01f, grenadeRadius * 2f);
        }

        void EnsureArcObjects()
        {
            if (arcLine != null) return;
            var lib = VfxLibrary.I;

            var lineGo = new GameObject("GrenadeArc");
            lineGo.layer = Layers.Player;
            arcLine = lineGo.AddComponent<LineRenderer>();
            arcLine.useWorldSpace = true;
            arcLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arcLine.receiveShadows = false;
            arcLine.numCapVertices = 2;
            if (lib != null) arcLine.sharedMaterial = lib.tracer;

            var marker = Prim.Create(PrimitiveType.Cylinder, "GrenadeArcMarker", null, Vector3.zero, Vector3.one,
                lib != null ? lib.explosion : null);
            marker.layer = Layers.Player;
            var mr = marker.GetComponent<Renderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            arcMarker = marker.transform;
        }

        void SetArcVisible(bool visible)
        {
            if (arcLine == null) return;
            if (arcLine.gameObject.activeSelf != visible) arcLine.gameObject.SetActive(visible);
            if (arcMarker.gameObject.activeSelf != visible) arcMarker.gameObject.SetActive(visible);
        }

        // ---------- 回復キット ----------

        void UseMedkit()
        {
            if (!MedkitReady || IsHealing || health.Hp >= health.maxHp) return;
            medkitReadyTime = Time.time + medkitCooldown;
            StartCoroutine(HealRoutine());
        }

        IEnumerator HealRoutine()
        {
            IsHealing = true;
            for (float t = 0f; t < medkitDuration && health.IsAlive; t += Time.deltaTime)
            {
                health.Heal(medkitHeal * Time.deltaTime / medkitDuration);
                yield return null;
            }
            IsHealing = false;
        }
    }
}
