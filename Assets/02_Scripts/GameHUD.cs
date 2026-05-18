using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LittleSword.Player;
using Unity.Netcode;

namespace LittleSword.UI
{
    public class GameHUD : MonoBehaviour
    {
        private void Awake()
        {
            // NGO 씬 전환 시 이전 씬(Start/LobbyList)의 Canvas가 잔존하는 경우 강제 제거
            // SetActive(false)로 숨겨진 비활성 Canvas도 포함해서 탐색
            string myScene = gameObject.scene.name;
            var allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in allCanvases)
            {
                string cScene = canvas.gameObject.scene.name;
                if (!string.IsNullOrEmpty(cScene) && cScene != myScene && cScene != "DontDestroyOnLoad")
                    Destroy(canvas.gameObject);
            }
        }

        [Header("HP UI")]
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("경험치 / 레벨 UI")]
        [SerializeField] private Slider expSlider;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject levelUpEffect;

        [Header("스테이지 UI")]
        [SerializeField] private TextMeshProUGUI stageText;
        [SerializeField] private TextMeshProUGUI enemyCountText;
        [SerializeField] private GameObject bossWarning;

        [Header("스테이지 클리어 UI")]
        [SerializeField] private GameObject clearBanner;

        private BasePlayer player;
        private LevelSystem levelSystem;
        private StageManager stageManager;
        private bool isConnected = false;

        private void Start()
        {
            stageManager = StageManager.Instance;

            if (stageManager != null)
            {
                stageManager.OnStageStart += HandleStageStart;
                stageManager.OnStageClear += HandleStageClear;
                stageManager.OnBossStage += HandleBossStage;
            }

            if (bossWarning != null) bossWarning.SetActive(false);
            if (clearBanner != null) clearBanner.SetActive(false);
        }

        private void Update()
        {
            if (!isConnected)
                TryConnectPlayer();

            // ✅ 연결된 경우에만 UI 갱신 (미연결 상태에서 매 프레임 null 체크 낭비 방지)
            if (isConnected)
                UpdateHpUI();

            if (stageManager == null)
                stageManager = StageManager.Instance;

            if (enemyCountText != null && stageManager != null)
                enemyCountText.text = $"남은 적: {stageManager.RemainingEnemies}";
        }

        private void TryConnectPlayer()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            // ✅ IsHost: 호스트(서버+클라이언트 겸임)
            //    IsConnectedClient: 순수 클라이언트가 서버와 연결 완료된 상태
            //    둘 중 하나면 시도 가능
            if (!nm.IsHost && !nm.IsConnectedClient) return;

            // ✅ GetLocalPlayerObject: PlayerSpawner가 SpawnAsPlayerObject를 완료해야 non-null
            //    클라이언트는 RPC 왕복 후 스폰되므로 Start()보다 늦을 수 있음
            //    isConnected = false 상태로 매 프레임 재시도하므로 결국 연결됨
            var localPlayerObj = nm.SpawnManager?.GetLocalPlayerObject();
            if (localPlayerObj == null) return;

            player = localPlayerObj.GetComponent<BasePlayer>();
            if (player == null) return;

            levelSystem = localPlayerObj.GetComponent<LevelSystem>();
            if (levelSystem == null) return;

            // ✅ networkHP / networkMaxHP 변경 시 즉시 UI 반영
            //    playerStats.maxHP 동기화 타이밍 문제를 완전히 우회
            player.networkHP.OnValueChanged += OnNetworkHPChanged;
            player.networkMaxHP.OnValueChanged += OnNetworkMaxHPChanged;

            levelSystem.OnLevelUp += HandleLevelUp;
            levelSystem.OnExpChanged += UpdateExpUI;

            UpdateHpUI();
            UpdateLevelUI();

            isConnected = true;
            Debug.Log($"[GameHUD] 연결 완료: {player.name}");
        }

        // ✅ NetworkVariable 콜백 → 값이 바뀌는 즉시 UI 갱신 (람다 아닌 메서드 → OnDestroy에서 해제 가능)
        private void OnNetworkHPChanged(int prev, int next) => UpdateHpUI();
        private void OnNetworkMaxHPChanged(int prev, int next) => UpdateHpUI();

        private void UpdateHpUI()
        {
            if (player == null) return;

            // ✅ playerStats.maxHP 대신 NetworkMaxHP 프로퍼티 사용
            //    클라이언트에서 playerStats 동기화 타이밍과 무관하게 항상 정확한 값
            int maxHP = player.NetworkMaxHP;
            int curHP = player.CurrentHP;

            if (maxHP <= 0) return;

            float ratio = (float)curHP / maxHP;

            if (hpSlider != null) hpSlider.value = ratio;
            if (hpText != null) hpText.text = $"{curHP} / {maxHP}";

            if (hpSlider != null)
            {
                var fill = hpSlider.fillRect?.GetComponent<Image>();
                if (fill != null)
                    fill.color = ratio < 0.3f ? Color.red : Color.green;
            }
        }

        private void UpdateExpUI(int currentExp, int nextLevelExp)
        {
            if (expSlider != null && nextLevelExp > 0)
                expSlider.value = (float)currentExp / nextLevelExp;
        }

        private void UpdateLevelUI()
        {
            if (levelSystem == null) return;
            if (levelText != null)
                levelText.text = $"Lv. {levelSystem.CurrentLevel}";
            UpdateExpUI(levelSystem.CurrentExp, levelSystem.ExpToNextLevel);
        }

        private void HandleLevelUp(int newLevel)
        {
            if (levelText != null) levelText.text = $"Lv. {newLevel}";
            if (levelUpEffect != null)
                StartCoroutine(ShowThenHide(levelUpEffect, 2f));
        }

        private void HandleStageStart(int stage)
        {
            if (stageText != null) stageText.text = $"Stage {stage}";
            if (clearBanner != null) clearBanner.SetActive(false);
        }

        private void HandleStageClear(int stage)
        {
            if (clearBanner != null)
                StartCoroutine(ShowThenHide(clearBanner, 2f));
        }

        private void HandleBossStage()
        {
            if (bossWarning != null)
                StartCoroutine(ShowThenHide(bossWarning, 3f));
        }

        private System.Collections.IEnumerator ShowThenHide(GameObject obj, float duration)
        {
            obj.SetActive(true);
            yield return new WaitForSeconds(duration);
            obj.SetActive(false);
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                // ✅ 메서드로 등록했으므로 정확히 해제 가능
                player.networkHP.OnValueChanged -= OnNetworkHPChanged;
                player.networkMaxHP.OnValueChanged -= OnNetworkMaxHPChanged;
            }
            if (levelSystem != null)
            {
                levelSystem.OnLevelUp -= HandleLevelUp;
                levelSystem.OnExpChanged -= UpdateExpUI;
            }
            if (stageManager != null)
            {
                stageManager.OnStageStart -= HandleStageStart;
                stageManager.OnStageClear -= HandleStageClear;
                stageManager.OnBossStage -= HandleBossStage;
            }
        }
    }
}