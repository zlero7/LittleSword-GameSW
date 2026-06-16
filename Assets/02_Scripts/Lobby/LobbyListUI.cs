using System.Collections.Generic;
using System.Threading.Tasks;
using LittleSword.Network;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LittleSword.UI
{
    /// <summary>
    /// "LobbyList" 씬에 배치하는 방 목록 UI.
    ///
    /// 씬 구성 예시:
    ///  Canvas
    ///    ├─ TitleText          (TextMeshProUGUI)  "방 목록"
    ///    ├─ ScrollView
    ///    │    └─ Viewport / Content  ← lobbyListParent
    ///    ├─ LobbyItemPrefab    (Prefab)            ← lobbyItemPrefab
    ///    ├─ RefreshButton      (Button)
    ///    ├─ BackButton         (Button)
    ///    └─ StatusText         (TextMeshProUGUI)
    /// </summary>
    public class LobbyListUI : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private Transform lobbyListParent;  // ScrollView > Content
        [SerializeField] private GameObject lobbyItemPrefab;  // LobbyItemUI 가 붙은 프리팹
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI statusText;

        private bool _busy;
        private int _lobbyItemIndex;

        // ─────────────────────────────────────────────────────────────────
        private void Start()
        {
            refreshButton?.onClick.AddListener(() => { if (!_busy) _ = RefreshAsync(); });
            backButton?.onClick.AddListener(OnBackClicked);

            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnError += OnLobbyError;

            // 씬이 열리면 바로 목록 갱신
            _ = RefreshAsync();
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnError -= OnLobbyError;
        }

        private void OnLobbyError(string message)
        {
            SetStatus($"오류: {message}\n새로 고침을 눌러 다시 시도하세요.");
            SetButtonsInteractable(true);
            _busy = false;
        }

        // ─────────────────────────────────────────────────────────────────
        #region 목록 갱신
        private async Task RefreshAsync()
        {
            _busy = true;
            SetStatus("방 목록을 불러오는 중...");
            SetButtonsInteractable(false);

            // LobbyManager 보장
            if (LobbyManager.Instance == null)
                new GameObject("LobbyManager").AddComponent<LobbyManager>();

            // 인증이 안 됐으면 재인증
            if (!LobbyManager.Instance.IsAuthenticated)
                await LobbyManager.Instance.AuthenticateAsync();

            List<Lobby> lobbies = await LobbyManager.Instance.FetchLobbiesAsync();

            // 기존 항목 제거
            foreach (Transform child in lobbyListParent)
                Destroy(child.gameObject);
            _lobbyItemIndex = 0;

            if (lobbies == null || lobbies.Count == 0)
            {
                SetStatus("현재 열려 있는 방이 없습니다.\n새로 고침을 눌러 다시 확인하세요.");
            }
            else
            {
                SetStatus($"{lobbies.Count}개의 방을 찾았습니다.");
                foreach (var lobby in lobbies)
                    CreateLobbyItem(lobby);
            }

            SetButtonsInteractable(true);
            _busy = false;
        }

        private void CreateLobbyItem(Lobby lobby)
        {
            GameObject go = Instantiate(lobbyItemPrefab);
            var rect = go.GetComponent<RectTransform>();
            GameObject container = new GameObject("LobbyItem_" + lobby.Name);
            var containerRect = container.AddComponent<RectTransform>();
            containerRect.SetParent(lobbyListParent, false);
            containerRect.anchorMin = new Vector2(0, 1);
            containerRect.anchorMax = new Vector2(1, 1);
            containerRect.pivot = new Vector2(0.5f, 1);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 130);
            float y = -_lobbyItemIndex * 130f - 100f;
            containerRect.anchoredPosition = new Vector2(0, y);
            rect.SetParent(containerRect, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            _lobbyItemIndex++;
            var item = go.GetComponent<LobbyItemUI>();
            if (item != null)
                item.Setup(lobby, OnLobbySelected);
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region 방 선택 → 입장
        private void OnLobbySelected(Lobby lobby)
        {
            if (_busy) return;
            _ = JoinAsync(lobby);
        }

        private async Task JoinAsync(Lobby lobby)
        {
            _busy = true;
            SetButtonsInteractable(false);
            SetStatus($"'{lobby.Name}' 에 입장하는 중...");

            bool ok = await LobbyManager.Instance.JoinLobbyAsync(lobby);

            if (!ok)
            {
                SetStatus("입장에 실패했습니다. 다시 시도하세요.");
                SetButtonsInteractable(true);
                _busy = false;
                return;
            }

            SetStatus("호스트에 연결하는 중...\n잠시 기다려 주세요.");
            // NGO 씬 전환 시 이전 Canvas가 잔존하는 문제 대비: 명시적으로 Canvas 비활성화
            HideLobbyListUI();
            _ = WaitForSceneLoadAsync();
        }

        // LobbyList 씬의 Canvas 루트 비활성화 → NGO 씬 전환 중 UI 잔존 방지
        private void HideLobbyListUI()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        private async Task WaitForSceneLoadAsync()
        {
            // 서버가 NetworkManager.SceneManager.LoadScene을 호출하면
            // NGO가 클라이언트 씬 전환을 자동으로 처리함.
            // 10초 안에 씬 전환이 없으면 연결 실패로 간주.
            float elapsed = 0f;
            const float timeout = 10f;
            while (elapsed < timeout)
            {
                await Task.Delay(500);
                elapsed += 0.5f;
                if (this == null) return; // 씬 전환으로 파괴된 경우 정상 종료
            }

            // 타임아웃: 연결 실패 처리
            if (LobbyManager.Instance != null)
                await LobbyManager.Instance.LeaveLobbyAsync();

            SetStatus("연결 시간 초과.\n다시 시도해 주세요.");
            SetButtonsInteractable(true);
            _busy = false;
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region 뒤로 가기
        private void OnBackClicked()
        {
            if (_busy) return;
            _ = LeaveAndGoBackAsync();
        }

        /// <summary>
        /// 현재 입장 중인 로비가 있으면 먼저 나간 뒤 Start 씬으로 돌아갑니다.
        /// LeaveLobbyAsync 호출로 CurrentLobby를 null로 초기화하여
        /// 재입장 시 "player is already a member" 오류를 방지합니다.
        /// </summary>
        private async Task LeaveAndGoBackAsync()
        {
            _busy = true;
            SetButtonsInteractable(false);
            SetStatus("로비에서 나가는 중...");

            if (LobbyManager.Instance != null)
                await LobbyManager.Instance.LeaveLobbyAsync();

            // 씬 이름만 사용 (부분 경로는 raw SceneManager가 매칭 실패함)
            SceneManager.LoadScene("Start");
        }
        #endregion

        // ─────────────────────────────────────────────────────────────────
        #region 헬퍼
        private void SetButtonsInteractable(bool v)
        {
            if (refreshButton) refreshButton.interactable = v;
            if (backButton) backButton.interactable = v;
        }

        private void SetStatus(string msg)
        {
            if (statusText) statusText.text = msg;
        }
        #endregion
    }
}