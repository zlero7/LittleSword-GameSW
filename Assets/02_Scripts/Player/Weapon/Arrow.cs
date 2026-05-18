using UnityEngine;
using Unity.Netcode;
using LittleSword.Interfaces;

namespace LittleSword.Player.Weapon
{
    public class Arrow : NetworkBehaviour
    {
        private Rigidbody2D rb;
        public float force = 10.0f;
        public int damage = 30;

        // 공격자 ClientId - 경험치 지급에 사용
        private ulong attackerClientId = ulong.MaxValue;

        public void Init(float force, int damage, ulong clientId = ulong.MaxValue)
        {
            this.force = force;
            this.damage = damage;
            this.attackerClientId = clientId;
        }

        public void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.AddRelativeForce(transform.right * force, ForceMode2D.Impulse);
            // 서버에서만 자동 제거
            if (IsServer)
                Invoke(nameof(DespawnArrow), 3.0f);
        }

        private void DespawnArrow()
        {
            if (IsServer && IsSpawned)
                GetComponent<NetworkObject>().Despawn(true);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 서버에서만 피해 처리
            if (!IsServer) return;

            if (other.CompareTag("Enemy"))
            {
                // 공격자 ID 기록
                other.GetComponent<LittleSword.Enemy.EnemyExp>()?.SetLastAttacker(attackerClientId);
                other.GetComponent<IDamageable>()?.TakeDamage(damage);

                if (IsSpawned)
                    GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }
}