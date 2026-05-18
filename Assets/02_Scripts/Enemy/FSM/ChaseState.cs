using UnityEngine;
using Logger = LittleSword.Common.Logger;

namespace LittleSword.Enemy.FSM
{
    public class ChaseState : IState
    {
        private readonly float detectInterval;
        private float lastDetectTime;
        private Enemy enemy;

        public ChaseState(Enemy enemy, float detectInterval = 0.3f)
        {
            this.enemy = enemy;
            this.detectInterval = detectInterval;
            lastDetectTime = Time.time - detectInterval;
        }

        public void Enter()
        {
            Logger.Log("Chase 진입");
            // ✅ [버그2 수정] 서버 Animator + 모든 클라이언트 Animator 동기화
            enemy.animator.SetBool(Enemy.hashIsRun, true);
            enemy.SyncChaseAnimClientRpc();
        }

        public void Update()
        {
            if (Time.time - lastDetectTime >= detectInterval)
            {
                lastDetectTime = Time.time;
                Logger.Log("Chase 갱신");

                if (enemy.DetectPlayer())
                {
                    enemy.MoveToPlayer();

                    if (enemy.IsInAttackRange())
                    {
                        enemy.StopMoving();
                        enemy.ChangeState<AttackState>();
                    }
                }
                else
                {
                    enemy.StopMoving();
                    enemy.ChangeState<IdleState>();
                }
            }
        }

        public void Exit()
        {
            Logger.Log("Chase 종료");
        }
    }
}