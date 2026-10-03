using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    [System.Serializable]
    public class WeaponDef
    {
        public string name;
        [Tooltip("HUDに出す短い名前。空なら連射武器は AR、それ以外は P")]
        public string shortName;
        public float damage;
        public float fireRate;      // 1秒あたりの発射数
        public int magSize;
        public float reloadTime;
        public float hipSpread;     // 腰撃ちのばらつき（度）
        public float aimSpread;     // エイム時のばらつき（度）
        public float recoil;
        public bool automatic;
        public bool infiniteAmmo;   // 弾が減らない（リロード不要）
    }

    /// メイン・サブ武器。画面中央へのヒットスキャン。α版では予備弾は無限。
    /// エディタ・開発ビルドでは、3キーでデバッグ用の武器に持ち替えられる
    public class WeaponSystem : MonoBehaviour
    {
        public WeaponDef[] weapons =
        {
            new WeaponDef { name = "アサルトライフル", shortName = "AR", damage = 12f, fireRate = 10f, magSize = 30, reloadTime = 1.8f, hipSpread = 2.2f, aimSpread = 0.6f, recoil = 0.35f, automatic = true },
            new WeaponDef { name = "ハンドガン", shortName = "P", damage = 28f, fireRate = 4f, magSize = 12, reloadTime = 1.2f, hipSpread = 1.2f, aimSpread = 0.2f, recoil = 1.1f, automatic = false },
        };
        public Transform muzzle;
        public float range = 200f;

        [Header("デバッグ（エディタ・開発ビルドのみ。3キーで持ち替え）")]
        public bool debugWeaponEnabled = true;
        public WeaponDef debugWeapon = new WeaponDef
        {
            name = "デバッグ銃", shortName = "DEBUG", damage = 500f, fireRate = 12f, magSize = 999, reloadTime = 0f,
            hipSpread = 0f, aimSpread = 0f, recoil = 0.1f, automatic = true, infiniteAmmo = true,
        };

        /// ウルトによる倍率
        [HideInInspector] public float damageMultiplier = 1f;
        [HideInInspector] public float fireRateMultiplier = 1f;

        public int CurrentIndex { get; private set; }
        public WeaponDef Current => weapons[CurrentIndex];
        public int Ammo => ammo[CurrentIndex];
        public int AmmoOf(int index) => ammo[index];
        public bool IsReloading => reloadEnd > 0f;
        public float ReloadProgress => IsReloading ? Mathf.InverseLerp(reloadStart, reloadEnd, Time.time) : 0f;

        int[] ammo;
        float nextFire;
        float reloadStart;
        float reloadEnd = -1f;
        PlayerController controller;
        Camera cam;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (debugWeaponEnabled && (Application.isEditor || Debug.isDebugBuild))
            {
                var list = new System.Collections.Generic.List<WeaponDef>(weapons) { debugWeapon };
                weapons = list.ToArray();
            }
            ammo = new int[weapons.Length];
            for (int i = 0; i < weapons.Length; i++) ammo[i] = weapons[i].magSize;
        }

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (IsReloading && Time.time >= reloadEnd)
            {
                ammo[CurrentIndex] = Current.magSize;
                reloadEnd = -1f;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (!controller.CanUseWeapons || kb == null || mouse == null) return;

            if (kb.digit1Key.wasPressedThisFrame) Switch(0);
            if (kb.digit2Key.wasPressedThisFrame) Switch(1);
            if (kb.digit3Key.wasPressedThisFrame) Switch(2);
            if (kb.rKey.wasPressedThisFrame) StartReload();
            if (!GameManager.CursorLocked) return;

            bool trigger = Current.automatic ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
            if (trigger && !IsReloading && Time.time >= nextFire)
            {
                if (ammo[CurrentIndex] > 0) Fire();
                else StartReload();
            }
        }

        void Switch(int index)
        {
            if (index == CurrentIndex || index >= weapons.Length) return;
            CurrentIndex = index;
            reloadEnd = -1f;
            nextFire = Time.time + 0.25f;
        }

        void StartReload()
        {
            if (IsReloading || Current.infiniteAmmo || ammo[CurrentIndex] >= Current.magSize) return;
            reloadStart = Time.time;
            reloadEnd = Time.time + Current.reloadTime;
        }

        void Fire()
        {
            var w = Current;
            if (!w.infiniteAmmo) ammo[CurrentIndex]--;
            nextFire = Time.time + 1f / (w.fireRate * fireRateMultiplier);

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float spread = controller.IsAiming ? w.aimSpread : w.hipSpread;
            Vector2 r = Random.insideUnitCircle * spread;
            Vector3 dir = Quaternion.AngleAxis(r.x, cam.transform.up) * Quaternion.AngleAxis(r.y, cam.transform.right) * ray.direction;

            Vector3 end = ray.origin + dir * range;
            if (Physics.Raycast(ray.origin, dir, out var hit, range, Layers.ShootMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                    CombatEvents.ReportPlayerDamage(target.TakeDamage(w.damage * damageMultiplier, hit.point));
            }

            Vector3 from = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.4f;
            VfxLibrary.Tracer(from, end);
            if (TPSCamera.I != null) TPSCamera.I.AddRecoil(w.recoil, Random.Range(-0.3f, 0.3f) * w.recoil);
            controller.MarkFired();
        }
    }
}
