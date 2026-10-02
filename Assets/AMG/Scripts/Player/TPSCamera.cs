using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// 肩越しのTPSカメラ。右クリックでエイム（寄り＋FOV縮小）、壁めり込み防止つき
    [RequireComponent(typeof(Camera))]
    public class TPSCamera : MonoBehaviour
    {
        public static TPSCamera I { get; private set; }

        public Transform target;
        public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        public float sensitivity = 0.1f;

        [Header("距離")]
        public float distance = 4.2f;
        public float aimDistance = 1.9f;
        public float shoulder = 0.75f;
        public float aimShoulder = 0.6f;

        [Header("画角")]
        public float fov = 60f;
        public float aimFov = 45f;

        [Header("制限")]
        public float minPitch = -35f;
        public float maxPitch = 70f;
        public float collisionRadius = 0.25f;

        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        Camera cam;
        PlayerController player;
        float yaw;
        float pitch = 8f;
        float currentDistance;
        float currentShoulder;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
        }

        void Start()
        {
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                player = target.GetComponent<PlayerController>();
            }
            currentDistance = distance;
            currentShoulder = shoulder;
        }

        public void AddRecoil(float up, float side)
        {
            pitch -= up;
            yaw += side;
        }

        void LateUpdate()
        {
            if (target == null) return;

            var mouse = Mouse.current;
            if (mouse != null && GameManager.IsPlaying && GameManager.CursorLocked)
            {
                Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x;
                pitch -= delta.y;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            bool aiming = player != null && player.IsAiming;
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            currentDistance = Mathf.Lerp(currentDistance, aiming ? aimDistance : distance, k);
            currentShoulder = Mathf.Lerp(currentShoulder, aiming ? aimShoulder : shoulder, k);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aiming ? aimFov : fov, k);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 shoulderPos = Cast(pivot, pivot + rot * Vector3.right * currentShoulder);
            Vector3 pos = Cast(shoulderPos, shoulderPos + rot * Vector3.back * currentDistance);
            transform.SetPositionAndRotation(pos, rot);
        }

        Vector3 Cast(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.001f) return to;
            Vector3 dir = d / len;
            if (Physics.SphereCast(from, collisionRadius, dir, out var hit, len, Layers.CameraMask, QueryTriggerInteraction.Ignore))
                return from + dir * Mathf.Max(0f, hit.distance - 0.05f);
            return to;
        }
    }
}
