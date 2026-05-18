using LittleSword.Enemy;
using LittleSword.Interfaces;
using LittleSword.Player;
using Unity.Netcode;
using UnityEngine;

namespace LittleSword.Player
{
    public class Warrior : BasePlayer
    {
        [SerializeField] private LayerMask enemyLayer;
        [SerializeField] private Vector2 size = new Vector2(1.0f, 2.0f);
        [SerializeField] private Vector2 offset = new Vector2(0.5f, 0.0f);

        private int comboStep = 0;
        private float comboTimer = 0f;
        private bool comboInputReceived = false;

        protected new void Update()
        {
            if (!IsOwner) return; // ✅ Archer와 동일하게 IsOwner 체크 추가
            base.Update();

            if (comboTimer > 0f)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f)
                    comboStep = 0;
            }
        }

        protected override void Attack()
        {
            if (!IsOwner) return;
            if (IsDead) return;

            if (comboStep == 0)
            {
                comboStep = 1;
                comboTimer = playerStats.comboResetTime;
                animator.SetTrigger("Attack");
            }
            else if (comboStep == 1 && comboTimer > 0f)
            {
                comboStep = 0;
                comboTimer = 0f;
                comboInputReceived = true;
                animator.SetTrigger("Attack2");
            }
        }

        // 애니메이션 이벤트: 1타
        public void OnWarriorAttack()
        {
            if (!IsOwner) return;
            if (IsDead) return; // ✅ IsDead 체크 추가

            // ✅ 방향을 클라이언트에서 계산해서 서버로 전달
            bool flipX = spriteRenderer.flipX;
            ExecuteAttackServerRpc(playerStats.attackDamage, flipX);
        }

        // 애니메이션 이벤트: 2타
        public void OnWarriorAttack2()
        {
            if (!IsOwner) return;
            if (IsDead) return; // ✅ IsDead 체크 추가

            // ✅ 방향을 클라이언트에서 계산해서 서버로 전달
            bool flipX = spriteRenderer.flipX;
            int comboDamage = Mathf.RoundToInt(playerStats.attackDamage * playerStats.combo2DamageMultiplier);
            ExecuteAttackServerRpc(comboDamage, flipX);
            comboInputReceived = false;
        }

        // ✅ flipX를 서버로 전달 - 방향 동기화 (Archer의 ServerRpc 패턴과 동일)
        [ServerRpc]
        private void ExecuteAttackServerRpc(int damage, bool flipX)
        {
            Vector2 direction = flipX ? Vector2.left : Vector2.right;
            Vector2 center = (Vector2)transform.position + direction * offset;
            Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0, enemyLayer);
            foreach (var col in colliders)
            {
                // 공격자 ID 설정
                col.GetComponent<LittleSword.Enemy.EnemyExp>()?.SetLastAttacker(OwnerClientId);
                col.GetComponent<IDamageable>()?.TakeDamage(damage);
            }
        }

        private void OnDrawGizmos()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            Vector2 direction = spriteRenderer.flipX ? Vector2.left : Vector2.right;
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawCube(transform.position + new Vector3(offset.x, offset.y, 0.0f), new Vector3(size.x, size.y, 0.0f));
        }
    }
}