using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AMG
{
    /// ガジェット①：グレネード（E） / ガジェット②：回復キット（F）
    public class GadgetSystem : MonoBehaviour
    {
        [Header("グレネード")]
        public int maxGrenades = 3;
        public float grenadeDamage = 150f;
        public float grenadeRadius = 4.5f;
        public float grenadeFuse = 1.4f;
        public float throwSpeed = 16f;

        [Header("回復キット")]
        public int maxMedkits = 2;
        public float medkitHeal = 50f;
        public float medkitDuration = 1.5f;

        public int Grenades { get; private set; }
        public int Medkits { get; private set; }
        public bool IsHealing { get; private set; }

        PlayerController controller;
        PlayerHealth health;
        CharacterController cc;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            cc = GetComponent<CharacterController>();
            Grenades = maxGrenades;
            Medkits = maxMedkits;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !controller.CanUseWeapons) return;
            if (kb.eKey.wasPressedThisFrame) ThrowGrenade();
            if (kb.fKey.wasPressedThisFrame) UseMedkit();
        }

        void ThrowGrenade()
        {
            if (Grenades <= 0) return;
            Grenades--;

            var camT = Camera.main.transform;
            Vector3 origin = transform.position + Vector3.up * 1.5f + camT.forward * 0.6f;
            var mat = VfxLibrary.I != null ? VfxLibrary.I.grenade : null;
            var go = Prim.Create(PrimitiveType.Sphere, "Grenade", null, origin, Vector3.one * 0.25f, mat, true);
            go.layer = Layers.Player;
            Physics.IgnoreCollision(go.GetComponent<Collider>(), cc);

            var rb = go.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.linearVelocity = (camT.forward + Vector3.up * 0.25f).normalized * throwSpeed;
            go.AddComponent<Grenade>().Init(grenadeDamage, grenadeRadius, grenadeFuse);
        }

        void UseMedkit()
        {
            if (Medkits <= 0 || IsHealing || health.Hp >= health.maxHp) return;
            Medkits--;
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
