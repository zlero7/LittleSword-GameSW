using UnityEngine;
using Unity.Netcode;
using LittleSword.Player.Weapon;
using LittleSword.InputSystem;

namespace LittleSword.Player
{
    public class Archer : BasePlayer
    {
        [SerializeField] private GameObject ArrowPrefab;
        [SerializeField] private Transform firePoint;

        private bool isCharging = false;
        private float chargeTime = 0f;

        [SerializeField] private UnityEngine.UI.Slider chargeSlider;

        protected new void Update()
        {
            if (!IsOwner) return;
            base.Update();

            if (isCharging)
            {
                chargeTime += Time.deltaTime;
                chargeTime = Mathf.Min(chargeTime, playerStats.maxChargeTime);
                if (chargeSlider != null)
                    chargeSlider.value = chargeTime / playerStats.maxChargeTime;
            }
        }

        protected override void Attack()
        {
            if (!IsOwner) return;
            animator.SetTrigger("Attack");
        }

        public void OnAttackHold()
        {
            if (!IsOwner) return;
            if (IsDead) return;
            if (!isCharging)
            {
                isCharging = true;
                chargeTime = 0f;
                if (chargeSlider != null)
                    chargeSlider.gameObject.SetActive(true);
            }
        }

        public void OnAttackRelease()
        {
            if (!IsOwner) return;
            if (IsDead) return;
            if (!isCharging) return;

            isCharging = false;
            if (chargeSlider != null)
                chargeSlider.gameObject.SetActive(false);

            float chargeRatio = chargeTime / playerStats.maxChargeTime;
            animator.SetTrigger("Attack");

            // ✅ 방향을 클라이언트에서 계산해서 서버로 전달
            bool flipX = spriteRenderer.flipX;
            Vector3 spawnPos = firePoint.position;
            FireChargedArrowServerRpc(chargeRatio, flipX, spawnPos);
        }

        public void OnArcherAttackEvent()
        {
            if (!IsOwner) return;
            if (!isCharging)
            {
                // ✅ 방향을 클라이언트에서 계산해서 서버로 전달
                bool flipX = spriteRenderer.flipX;
                Vector3 spawnPos = firePoint.position;
                FireArrowServerRpc(flipX, spawnPos);
            }
        }

        // ✅ flipX와 spawnPos를 서버로 전달 - 방향 동기화
        [ServerRpc]
        private void FireArrowServerRpc(bool flipX, Vector3 spawnPos)
        {
            Quaternion rot = Quaternion.Euler(0, flipX ? 180 : 0, 0);
            GameObject arrow = Instantiate(ArrowPrefab, spawnPos, rot);
            arrow.GetComponent<NetworkObject>().Spawn();
            arrow.GetComponent<Arrow>().Init(playerStats.fireForce, playerStats.attackDamage, OwnerClientId);
        }

        [ServerRpc]
        private void FireChargedArrowServerRpc(float chargeRatio, bool flipX, Vector3 spawnPos)
        {
            Quaternion rot = Quaternion.Euler(0, flipX ? 180 : 0, 0);
            GameObject arrow = Instantiate(ArrowPrefab, spawnPos, rot);
            arrow.GetComponent<NetworkObject>().Spawn();

            float damageMultiplier = Mathf.Lerp(1f, playerStats.maxChargeDamageMultiplier, chargeRatio);
            float forceMultiplier = Mathf.Lerp(1f, playerStats.maxChargeForceMultiplier, chargeRatio);
            int chargedDamage = Mathf.RoundToInt(playerStats.attackDamage * damageMultiplier);
            float chargedForce = playerStats.fireForce * forceMultiplier;

            arrow.GetComponent<Arrow>().Init(chargedForce, chargedDamage, OwnerClientId);

            if (chargeRatio >= 1f)
            {
                var arrowSprite = arrow.GetComponent<SpriteRenderer>();
                if (arrowSprite != null)
                    arrowSprite.color = Color.red;
            }
        }
    }
}