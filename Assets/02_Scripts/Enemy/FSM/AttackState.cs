using LittleSword.Player;
using UnityEngine;
using Logger = LittleSword.Common.Logger;

namespace LittleSword.Enemy.FSM
{
    public class AttackState : IState
    {
        private readonly float attackCooldown;
        private float lastAttackTime;
        private Enemy enemy;

        public AttackState(Enemy enemy, float attackCooldown = 1.0f)
        {
            this.enemy = enemy;
            this.attackCooldown = attackCooldown;
            lastAttackTime = Time.time - this.attackCooldown;
        }

        public void Enter()
        {
            Logger.Log("Attack 진입");
            // ✅ [버그2 수정] 서버 Animator + 모든 클라이언트 Animator 동기화
            enemy.animator.SetBool(Enemy.hashIsRun, false);
            enemy.animator.SetTrigger(Enemy.hashAttack);
            enemy.SyncAttackAnimClientRpc();
        }

        public void Update()
        {
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                lastAttackTime = Time.time;

                if (enemy.Target == null || enemy.Target.GetComponent<BasePlayer>()?.IsDead == true)
                {
                    enemy.ChangeState<IdleState>();
                    return;
                }

                if (enemy.IsInAttackRange())
                {
                    enemy.animator.SetBool(Enemy.hashIsRun, false);
                    enemy.SetFacing();
                    enemy.animator.SetTrigger(Enemy.hashAttack);
                    // ✅ [버그2 수정] 반복 공격도 클라이언트에 동기화
                    enemy.SyncAttackAnimClientRpc();
                }
                else
                {
                    enemy.ChangeState<ChaseState>();
                }
            }
        }

        public void Exit()
        {
            Logger.Log("Attack 종료");
        }
    }
}