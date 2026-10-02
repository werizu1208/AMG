using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// 肩越しのTPSカメラ。常にプレイヤーの右後方・上部に固定される。
    /// マウス左右でプレイヤーごと旋回し、マウス上下は視線の傾きだけを変える（カメラ位置は動かない）。
    /// 右クリックでエイム（寄り＋FOV縮小）、壁めり込み防止つき
    [DefaultExecutionOrder(-50)] // プレイヤーの移動より先にマウス入力を反映し、本体とカメラの向きがずれないようにする
    [RequireComponent(typeof(Camera))]
    public class TPSCamera : MonoBehaviour
    {
        public static TPSCamera I { get; private set; }

        public Transform target;
        public Vector3 pivotOffset = new Vector3(0f, 1.55f, 0f);
        public float sensitivity = 0.1f;

        [Header("位置（プレイヤーから見た右後方・上部）")]
        public float distance = 3.6f;
        public float aimDistance = 1.9f;
        public float shoulder = 0.8f;
        public float aimShoulder = 0.6f;
        public float height = 0.55f;
        public float aimHeight = 0.25f;

        [Header("画角")]
        public float fov = 60f;
        public float aimFov = 45f;

        [Header("視線の上下")]
        public float minPitch = -40f;
        public float maxPitch = 60f;
        public float defaultPitch = 8f;
        public float collisionRadius = 0.25f;

        /// 旋回角。プレイヤー本体の向きはこれに合わせる
        public float Yaw => yaw;
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        Camera cam;
        PlayerController player;
        float yaw;
        float pitch;
        float currentDistance;
        float currentShoulder;
        float currentHeight;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            pitch = defaultPitch;
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
            currentHeight = height;
        }

        public void AddRecoil(float up, float side)
        {
            pitch -= up;
            yaw += side;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null && GameManager.IsPlaying && GameManager.CursorLocked && PlayerHealth.I != null && PlayerHealth.I.IsAlive)
            {
                Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x;
                pitch -= delta.y;
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        void LateUpdate()
        {
            if (target == null) return;

            bool aiming = player != null && player.IsAiming;
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            currentDistance = Mathf.Lerp(currentDistance, aiming ? aimDistance : distance, k);
            currentShoulder = Mathf.Lerp(currentShoulder, aiming ? aimShoulder : shoulder, k);
            currentHeight = Mathf.Lerp(currentHeight, aiming ? aimHeight : height, k);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aiming ? aimFov : fov, k);

            // 位置は旋回角だけで決める（上下を向いてもカメラは右後方上部に留まる）
            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pivot = target.position + pivotOffset;
            Vector3 shoulderPos = Cast(pivot, pivot + yawRot * new Vector3(currentShoulder, currentHeight, 0f));
            Vector3 pos = Cast(shoulderPos, shoulderPos + yawRot * Vector3.back * currentDistance);
            transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0f));
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
