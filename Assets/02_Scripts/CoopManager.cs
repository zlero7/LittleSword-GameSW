using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using TMPro;
using LittleSword.Player;

namespace LittleSword.UI
{
    // CoopManager: 관전 시점 전환/부활/게임오버 관리
    // Canvas GameObject에 부착
    public class CoopManager : NetworkBehaviour
    {
        [Header("관전 UI")]
        [SerializeField] private GameObject spectatePanel;
        [SerializeField] private TextMeshProUGUI spectateText;

        [Header("게임오버 UI")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("부활 UI")]
        [SerializeField] private GameObject revivePanel;

        [Header("설정")]
        [SerializeField] private float gameOverDelay = 10f;
        [SerializeField] private Transform[] respawnPoints;

        private BasePlayer myPlayer;
        private Camera myCamera;
        private bool isSpectating = false;
        private bool isGameOver = false;
        private List<BasePlayer> alivePlayers = new List<BasePlayer>();
        private int spectateIndex = 0;
        private BasePlayer currentSpectateTarget = null;

        private NetworkVariable<int> deadPlayerCount = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private void Start()
        {
            if (spectatePanel != null) spectatePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (revivePanel != null) revivePanel.SetActive(false);

            if (StageManager.Instance != null)
                StageManager.Instance.OnBossKill += OnBossKill;

            StartCoroutine(FindMyPlayer());
        }

        private IEnumerator FindMyPlayer()
        {
            yield return new WaitForSeconds(0.5f);

            foreach (var p in FindObjectsByType<BasePlayer>(FindObjectsSortMode.None))
            {
                var netObj = p.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    myPlayer = p;
                    myCamera = p.GetComponentInChildren<Camera>(true);
                    break;
                }
            }
        }

        private void Update()
        {
            if (isGameOver || myPlayer == null) return;

            if (isSpectating)
            {
                UpdateSpectateCamera();

                if (Keyboard.current != null &&
                    Keyboard.current.tabKey.wasPressedThisFrame &&
                    alivePlayers.Count > 0)
                {
                    // 순환: 자신의 시체(null) → 생존자 0 → 1 → ... → 마지막 생존자 → 다시 자신의 시체
                    if (currentSpectateTarget == null)
                    {
                        spectateIndex = 0;
                        currentSpectateTarget = alivePlayers[0];
                    }
                    else
                    {
                        spectateIndex++;
                        currentSpectateTarget = spectateIndex >= alivePlayers.Count
                            ? null
                            : alivePlayers[spectateIndex];
                    }
                }
            }
        }

        public void StartSpectating()
        {
            if (isSpectating || isGameOver) return;

            isSpectating = true;
            // 죽은 직후에는 다른 생존자로 자동 전환하지 않고 자신의 시체를 먼저 보여준다.
            currentSpectateTarget = null;

            if (myCamera != null)
            {
                myCamera.transform.SetParent(null);
                myCamera.gameObject.SetActive(true);
            }

            if (spectatePanel != null) spectatePanel.SetActive(true);
            ReportDeadServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReportDeadServerRpc()
        {
            deadPlayerCount.Value++;

            int totalPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count;

            if (deadPlayerCount.Value >= totalPlayers)
                GameOverClientRpc();
        }

        private void UpdateSpectateCamera()
        {
            alivePlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None)
                .Where(p => !p.IsDead)
                .OrderBy(p => p.GetComponent<NetworkObject>().OwnerClientId)
                .ToList();

            if (myCamera == null) return;

            // 관전 중이던 대상이 죽었으면(자신의 시체를 보는 중이 아닐 때만) 다음 생존자로 전환.
            // currentSpectateTarget이 null인 경우는 "자신의 시체를 보는 중" 상태이므로
            // Tab을 누르기 전까지는 자동으로 전환하지 않는다.
            if (currentSpectateTarget != null && currentSpectateTarget.IsDead)
            {
                if (alivePlayers.Count == 0) return;
                spectateIndex = Mathf.Clamp(spectateIndex, 0, alivePlayers.Count - 1);
                currentSpectateTarget = alivePlayers[spectateIndex];
            }

            BasePlayer viewTarget = currentSpectateTarget != null ? currentSpectateTarget : myPlayer;
            if (viewTarget == null) return;

            Vector3 targetPos = viewTarget.transform.position + Vector3.back * 10f;
            myCamera.transform.position = Vector3.Lerp(
                myCamera.transform.position, targetPos, Time.deltaTime * 5f);

            if (spectateText != null)
                spectateText.text = currentSpectateTarget != null
                    ? $"관전 중 - {currentSpectateTarget.gameObject.name} (Tab으로 전환)"
                    : "자신의 시체를 보는 중 (Tab으로 다른 플레이어 관전)";
        }

        [ClientRpc]
        private void GameOverClientRpc()
        {
            isGameOver = true;
            isSpectating = false;

            currentSpectateTarget = null;
            spectateIndex = 0;

            // ✅ [수정] 카메라를 끄는 대신 게임오버 UI가 보이도록 반드시 활성화 유지
            // 모든 유저가 죽은 상황이므로 카메라를 끄면 "No cameras rendering" 발생
            if (myCamera != null)
            {
                // 관전 중이었다면 이미 SetParent(null) 된 상태 — 위치만 고정
                // 관전 중이 아니었다면(호스트 등) 카메라가 플레이어 자식에 있으므로 분리
                myCamera.transform.SetParent(null);
                myCamera.gameObject.SetActive(true);
            }

            if (spectatePanel != null) spectatePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(true);

            // ✅ [수정] 카운트다운을 텍스트에 즉시 표시 후 코루틴 시작
            if (countdownText != null)
                countdownText.text = $"{Mathf.CeilToInt(gameOverDelay)}초 후 로비로 이동...";

            StartCoroutine(GameOverCountdown());
        }

        private IEnumerator GameOverCountdown()
        {
            float timer = gameOverDelay;
            while (timer > 0f)
            {
                if (countdownText != null)
                    countdownText.text = $"{Mathf.CeilToInt(timer)}초 후 로비로 이동...";
                yield return new WaitForSeconds(1f);
                timer -= 1f;
            }

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();

            yield return new WaitForSeconds(0.5f);
            // 씬 이름만 사용 (부분 경로는 raw SceneManager가 매칭 실패함)
            SceneManager.LoadScene("Start");
        }

        private void OnBossKill(int stage)
        {
            if (!IsServer) return;

            deadPlayerCount.Value = 0;

            var allPlayers = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                if (!player.IsDead) continue;

                Vector3 revivePos = player.transform.position;
                if (respawnPoints != null && respawnPoints.Length > 0)
                {
                    var netObj = player.GetComponent<NetworkObject>();
                    int idx = netObj != null
                        ? (int)(netObj.OwnerClientId % (ulong)respawnPoints.Length)
                        : 0;
                    revivePos = respawnPoints[idx].position;
                }

                player.Revive(revivePos);
            }

            ShowRevivePanelClientRpc();
        }

        [ClientRpc]
        private void ShowRevivePanelClientRpc()
        {
            if (!isSpectating) return;

            isSpectating = false;

            currentSpectateTarget = null;
            spectateIndex = 0;

            if (myCamera != null && myPlayer != null)
            {
                myCamera.transform.SetParent(myPlayer.transform);
                myCamera.transform.localPosition = new Vector3(0f, 0f, -10f);
            }

            if (spectatePanel != null) spectatePanel.SetActive(false);
            StartCoroutine(ShowRevivePanel());
        }

        private IEnumerator ShowRevivePanel()
        {
            if (revivePanel != null)
            {
                revivePanel.SetActive(true);
                yield return new WaitForSeconds(2f);
                revivePanel.SetActive(false);
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (StageManager.Instance != null)
                StageManager.Instance.OnBossKill -= OnBossKill;
        }
    }
}