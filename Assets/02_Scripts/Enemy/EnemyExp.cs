using UnityEngine;
using Unity.Netcode;
using LittleSword.Player;

namespace LittleSword.Enemy
{
    public class EnemyExp : NetworkBehaviour
    {
        [Header("경험치 설정")]
        [SerializeField] private int expReward = 30;
        [SerializeField] private bool isBoss = false;

        // 마지막으로 데미지를 준 플레이어 ClientId
        private ulong lastAttackerClientId = ulong.MaxValue;

        // 공격자 기록 - Warrior의 ExecuteAttackServerRpc, Arrow의 OnTriggerEnter2D에서 호출
        public void SetLastAttacker(ulong clientId)
        {
            lastAttackerClientId = clientId;
        }

        public void DropExp()
        {
            if (!IsServer) return;

            int reward = isBoss ? expReward * 5 : expReward;

            // ✅ ClientRpc로 내려보내지 않고, 서버에서 직접 LevelSystem.GainExp 호출
            //    GainExp는 이제 IsServer 체크를 하지 않으므로 서버에서 바로 처리 가능
            var allLevelSystems = FindObjectsByType<LevelSystem>(FindObjectsSortMode.None);

            foreach (var levelSystem in allLevelSystems)
            {
                var basePlayer = levelSystem.GetComponent<BasePlayer>();
                if (basePlayer == null) continue;

                // 공격자가 기록된 경우 → 공격자 클라이언트 소유 플레이어에게만 지급
                if (lastAttackerClientId != ulong.MaxValue)
                {
                    if (basePlayer.OwnerClientId == lastAttackerClientId)
                    {
                        levelSystem.GainExp(reward);
                        Debug.Log($"[EnemyExp] 경험치 지급! +{reward}");
                    }
                }
                else
                {
                    // 공격자 정보 없으면 모든 플레이어에게 지급
                    levelSystem.GainExp(reward);
                }
            }
        }
    }
}