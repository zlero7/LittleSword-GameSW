using UnityEngine;

namespace LittleSword.Enemy.FSM
{
    public class IdleState : IState
    {
        private readonly float detectInterval;
        private float lastDetectTime;
        private Enemy enemy;

        public IdleState(Enemy enemy, float detectInterval = 0.3f)
        {
            this.enemy = enemy;
            this.detectInterval = detectInterval;
            lastDetectTime = Time.time - detectInterval;
        }

        public void Enter()
        {
            // ✅ [버그2 수정] 서버 Animator + 모든 클라이언트 Animator 동기화
            enemy.animator.SetBool(Enemy.hashIsRun, false);
            enemy.SyncIdleAnimClientRpc();
        }

        public void Update()
        {
            if (Time.time - lastDetectTime >= detectInterval)
            {
                lastDetectTime = Time.time;

                if (enemy.DetectPlayer())
                    enemy.ChangeState<ChaseState>();
            }
        }

        public void Exit() { }
    }
}