using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// TPS移動：WASD移動 / Shiftダッシュ / Spaceジャンプ / Ctrl回避 / 右クリックエイム
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("移動")]
        public float walkSpeed = 4.5f;
        public float sprintSpeed = 7.5f;
        public float aimSpeed = 2.8f;
        public float jumpHeight = 1.1f;
        public float gravity = -22f;
        public float turnSpeed = 720f;

        [Header("回避")]
        public float dodgeSpeed = 13f;
        public float dodgeDuration = 0.3f;
        public float dodgeCooldown = 0.8f;

        /// ウルトなどによる移動速度倍率
        [HideInInspector] public float moveSpeedMultiplier = 1f;

        public bool IsAiming { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsDodging => dodgeTimer > 0f;
        public bool IsStunned => stunTimer > 0f;
        public float StunRemaining => stunTimer;
        public bool CanUseWeapons => GameManager.AcceptsBattleInput && health.IsAlive && !IsDodging && !IsStunned;

        CharacterController cc;
        PlayerHealth health;
        float verticalVelocity;
        float stunTimer;
        float dodgeTimer;
        float dodgeCooldownTimer;
        float lastFireTime = -10f;
        Vector3 dodgeDir;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = GetComponent<PlayerHealth>();
        }

        public void Stun(float duration)
        {
            stunTimer = Mathf.Max(stunTimer, duration);
            IsSprinting = false;
        }

        public void MarkFired()
        {
            lastFireTime = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stunTimer = Mathf.Max(0f, stunTimer - dt);
            dodgeCooldownTimer = Mathf.Max(0f, dodgeCooldownTimer - dt);

            bool active = GameManager.IsPlaying && health.IsAlive;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            Vector2 input = Vector2.zero;
            bool jump = false, dodge = false, sprint = false;
            IsAiming = false;
            if (active && !IsStunned && kb != null && mouse != null)
            {
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                IsAiming = mouse.rightButton.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
                dodge = kb.leftCtrlKey.wasPressedThisFrame || kb.rightCtrlKey.wasPressedThisFrame;
                sprint = kb.leftShiftKey.isPressed;
            }
            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 camFwd = TPSCamera.I != null ? TPSCamera.I.PlanarForward : transform.forward;
            Vector3 camRight = Vector3.Cross(Vector3.up, camFwd);
            Vector3 moveDir = camFwd * input.y + camRight * input.x;

            bool recentlyFired = Time.time - lastFireTime < 0.35f;
            IsSprinting = sprint && input.y > 0.1f && !IsAiming && !recentlyFired;

            if (dodge && dodgeCooldownTimer <= 0f && !IsDodging && cc.isGrounded)
            {
                dodgeDir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : -camFwd;
                dodgeTimer = dodgeDuration;
                dodgeCooldownTimer = dodgeCooldown + dodgeDuration;
                health.SetDodgeInvulnerable(true);
            }

            Vector3 horizontal;
            if (IsDodging)
            {
                dodgeTimer -= dt;
                horizontal = dodgeDir * dodgeSpeed;
                if (dodgeTimer <= 0f) health.SetDodgeInvulnerable(false);
            }
            else
            {
                float speed = IsAiming ? aimSpeed : IsSprinting ? sprintSpeed : walkSpeed;
                horizontal = moveDir * speed * moveSpeedMultiplier;
            }

            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (jump && cc.isGrounded && !IsDodging) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            verticalVelocity += gravity * dt;
            cc.Move((horizontal + Vector3.up * verticalVelocity) * dt);

            // 構えている間・撃った直後はカメラの向き、それ以外は移動方向を向く
            Vector3 face = Vector3.zero;
            if (active && (IsAiming || recentlyFired)) face = camFwd;
            else if (horizontal.sqrMagnitude > 0.01f) face = horizontal;
            face.y = 0f;
            if (active && face.sqrMagnitude > 0.001f)
            {
                var target = Quaternion.LookRotation(face);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt * (IsAiming ? 2f : 1f));
            }
        }
    }
}
