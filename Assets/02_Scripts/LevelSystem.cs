using UnityEngine;
using System;
using Unity.Netcode;

namespace LittleSword.Player
{
    // ✅ ClientRpc 사용을 위해 MonoBehaviour → NetworkBehaviour로 변경
    public class LevelSystem : NetworkBehaviour
    {
        [Header("레벨 설정")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int currentExp = 0;
        [SerializeField] private int maxLevel = 20;

        [Header("레벨업 스탯 증가량")]
        [SerializeField] private int hpPerLevel = 20;
        [SerializeField] private int attackPerLevel = 5;
        [SerializeField] private float speedPerLevel = 0.1f;

        private BasePlayer player;

        public event Action<int> OnLevelUp;
        public event Action<int, int> OnExpChanged;

        public int CurrentLevel => currentLevel;
        public int CurrentExp => currentExp;
        public int ExpToNextLevel => GetExpForLevel(currentLevel + 1);

        private void Awake()
        {
            player = GetComponent<BasePlayer>();
        }

        // ✅ EnemyExp.DropExp()가 IsServer 보장 후 서버에서만 호출 → 여기서 중복 체크 불필요
        public void GainExp(int amount)
        {
            if (currentLevel >= maxLevel) return;

            currentExp += amount;
            Debug.Log($"[LevelSystem] GainExp 호출! +{amount}, 현재 exp: {currentExp}");

            // ✅ 경험치 변화를 클라이언트 HUD에 동기화
            SyncExpClientRpc(currentExp, ExpToNextLevel);

            while (currentExp >= ExpToNextLevel && currentLevel < maxLevel)
            {
                currentExp -= ExpToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            currentLevel++;
            Debug.Log($"[LevelUp] Lv.{currentLevel}!");

            // ✅ 서버에서 직접 스탯 반영 (이미 서버 컨텍스트이므로 ServerRpc 불필요)
            if (player != null)
                player.ApplyLevelUpStats(hpPerLevel, attackPerLevel, speedPerLevel);

            // ✅ 레벨업 이벤트를 클라이언트 HUD에 전파
            SyncLevelUpClientRpc(currentLevel);
        }

        // ✅ 경험치 변화를 클라이언트 HUD(OnExpChanged 이벤트)에 전달
        [ClientRpc]
        private void SyncExpClientRpc(int exp, int nextExp)
        {
            OnExpChanged?.Invoke(exp, nextExp);
        }

        // ✅ 레벨업 이벤트를 클라이언트 HUD(OnLevelUp 이벤트)에 전달
        [ClientRpc]
        private void SyncLevelUpClientRpc(int newLevel)
        {
            currentLevel = newLevel; // 클라이언트 로컬 값 동기화
            OnLevelUp?.Invoke(newLevel);
        }

        public int GetExpForLevel(int level)
        {
            int baseExp = 50;
            return Mathf.RoundToInt(baseExp * Mathf.Pow(level, 1.5f));
        }
    }
}