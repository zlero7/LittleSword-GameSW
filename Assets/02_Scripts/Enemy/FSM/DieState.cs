using UnityEngine;
using Unity.Netcode;
using LittleSword.Enemy;
using LittleSword;
using System.Collections;

namespace LittleSword.Enemy.FSM
{
    public class DieState : IState
    {
        private readonly Enemy enemy;

        public DieState(Enemy enemy)
        {
            this.enemy = enemy;
        }

        public void Enter()
        {
            // 사망 애니메이션 + 물리 처리는 모든 클라이언트에서 실행
            enemy.animator.SetTrigger(Enemy.hashDie);
            enemy.StopMoving();
            enemy.GetComponent<Collider2D>().enabled = false;
            enemy.rigidbody.bodyType = RigidbodyType2D.Kinematic;

            if (!enemy.IsServer) return;

            // StageManager에 사망 알림
            StageManager.Instance?.OnEnemyDead(enemy.gameObject);

            // 경험치 지급 후 다음 프레임에 Despawn
            enemy.StartCoroutine(DropExpThenDespawn());
        }

        private IEnumerator DropExpThenDespawn()
        {
            // 경험치 ClientRpc를 먼저 전송
            enemy.GetComponent<EnemyExp>()?.DropExp();

            // 한 프레임 대기 → ClientRpc가 네트워크 큐에 실린 뒤 Despawn
            yield return null;

            if (enemy != null && enemy.NetworkObject.IsSpawned)
                enemy.GetComponent<NetworkObject>().Despawn(true);
        }

        public void Update() { }
        public void Exit() { }
    }
}