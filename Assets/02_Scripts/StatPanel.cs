using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LittleSword.Player;
using UnityEngine.InputSystem;

namespace LittleSword.UI
{
    // StatPanel: F키로 열리는 스탯 분배 창
    // Canvas 하위 패널 GameObject에 부착
    public class StatPanel : MonoBehaviour
    {
        [Header("패널 루트")]
        [SerializeField] private GameObject panelRoot;

        [Header("포인트 표시")]
        [SerializeField] private TextMeshProUGUI statPointText;   // "잔여 포인트: 3"

        [Header("스탯 행 - 힘 (공격력)")]
        [SerializeField] private TextMeshProUGUI atkValueText;
        [SerializeField] private Button atkPlusButton;

        [Header("스탯 행 - 체력 (최대 HP)")]
        [SerializeField] private TextMeshProUGUI hpValueText;
        [SerializeField] private Button hpPlusButton;

        [Header("스탯 행 - 이동속도")]
        [SerializeField] private TextMeshProUGUI spdValueText;
        [SerializeField] private Button spdPlusButton;

        // 참조
        private BasePlayer player;
        private LevelSystem levelSystem;
        private bool isConnected = false;

        // 스탯 포인트
        private int statPoints = 0;

        // 레벨업당 지급 포인트
        private const int PointsPerLevel = 2;

        // 스탯 1포인트당 증가량
        private const int AtkPerPoint = 5;
        private const int HpPerPoint = 20;
        private const float SpdPerPoint = 0.2f;

        private void Start()
        {
            // 버튼 연결
            if (atkPlusButton != null) atkPlusButton.onClick.AddListener(() => AllocateStat(StatType.Attack));
            if (hpPlusButton != null) hpPlusButton.onClick.AddListener(() => AllocateStat(StatType.HP));
            if (spdPlusButton != null) spdPlusButton.onClick.AddListener(() => AllocateStat(StatType.Speed));

            // 초기에는 닫힌 상태
            if (panelRoot != null) panelRoot.SetActive(false);

            RefreshUI();
        }

        private void Update()
        {
            // ✅ 멀티플레이: 로컬 플레이어 스폰 이후에 연결 시도
            if (!isConnected)
                TryConnectPlayer();

            if (Keyboard.current.fKey.wasPressedThisFrame)
                Toggle();
        }

        // ✅ FindFirstObjectByType 대신 NetworkManager로 로컬 플레이어를 정확히 가져옴
        //    호스트/클라이언트 모두 자기 캐릭터에만 스탯 포인트가 적용됨
        private void TryConnectPlayer()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) return;
            if (!nm.IsHost && !nm.IsConnectedClient) return;

            var localPlayerObj = nm.SpawnManager?.GetLocalPlayerObject();
            if (localPlayerObj == null) return;

            player = localPlayerObj.GetComponent<BasePlayer>();
            if (player == null) return;

            levelSystem = localPlayerObj.GetComponent<LevelSystem>();
            if (levelSystem == null) return;

            // 레벨업 이벤트 구독
            levelSystem.OnLevelUp += HandleLevelUp;

            Debug.Log($"StatPanel - LevelSystem: {levelSystem}");

            isConnected = true;
            RefreshUI();
        }

        // ── 공개 메서드 ────────────────────────────────────
        public void Toggle()
        {
            if (panelRoot == null) return;
            bool next = !panelRoot.activeSelf;
            panelRoot.SetActive(next);
            if (next) RefreshUI();
        }

        // ── 레벨업 핸들러 ──────────────────────────────────
        private void HandleLevelUp(int newLevel)
        {
            statPoints += PointsPerLevel;
            RefreshUI();
        }

        // ── 스탯 분배 ──────────────────────────────────────
        private enum StatType { Attack, HP, Speed }

        private void AllocateStat(StatType type)
        {
            if (statPoints <= 0) return;
            if (player == null || player.playerStats == null) return;

            statPoints--;

            switch (type)
            {
                case StatType.Attack:
                    // ✅ NetworkVariable 기반으로 스탯 변경 요청
                    player.RequestLevelUpServerRpc(0, AtkPerPoint, 0f);
                    break;
                case StatType.HP:
                    player.RequestLevelUpServerRpc(HpPerPoint, 0, 0f);
                    break;
                case StatType.Speed:
                    player.RequestLevelUpServerRpc(0, 0, SpdPerPoint);
                    break;
            }

            RefreshUI();
        }

        // ── UI 갱신 ────────────────────────────────────────
        private void RefreshUI()
        {
            if (player == null || player.playerStats == null) return;

            if (statPointText != null)
                statPointText.text = $"잔여 포인트: {statPoints}";

            if (atkValueText != null)
                atkValueText.text = $"{player.playerStats.attackDamage}";

            if (hpValueText != null)
                hpValueText.text = $"{player.playerStats.maxHP}";

            if (spdValueText != null)
                spdValueText.text = $"{player.playerStats.moveSpeed:F1}";

            // 포인트 없으면 버튼 비활성화
            bool canAllocate = statPoints > 0;
            if (atkPlusButton != null) atkPlusButton.interactable = canAllocate;
            if (hpPlusButton != null) hpPlusButton.interactable = canAllocate;
            if (spdPlusButton != null) spdPlusButton.interactable = canAllocate;
        }

        private void OnDestroy()
        {
            if (levelSystem != null)
                levelSystem.OnLevelUp -= HandleLevelUp;
        }
    }
}