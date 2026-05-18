using System.Threading.Tasks;
using LittleSword.Network;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LittleSword.Network
{
    /// <summary>
    /// Host / Join / Server 버튼 UI
    /// [Host]   Unity Lobby + Relay 생성 → StartHost() → Basic 씬
    /// [Join]   LobbyList 씬으로 이동
    /// [Server] Lobby + Relay 생성 → StartServer() (플레이어 없이 서버만)
    /// </summary>
    public class NetworkManagerUI : MonoBehaviour
    {
        [SerializeField] private Button hostButton;
        [SerializeField] private Button clientButton;
        [SerializeField] private Button serverButton;             // 서버 전용 버튼
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private TextMeshProUGUI statusText;

        private bool _busy;

        private void Start()
        {
            hostButton?.onClick.AddListener(OnHostClicked);
            clientButton?.onClick.AddListener(OnClientClicked);
            serverButton?.onClick.AddListener(OnServerClicked);
            SetStatus("버튼을 선택하세요.");
        }

        // ── Host ──────────────────────────────────────────────────────────────
        private void OnHostClicked()
        {
            if (_busy) return;
            _ = StartHostAsync();
        }

        private async Task StartHostAsync()
        {
            _busy = true;
            SetButtonsInteractable(false);

            SetStatus("인증 중...");
            await EnsureLobbyManager();
            await LobbyManager.Instance.AuthenticateAsync();

            string roomName = GetRoomName();
            SetStatus($"'{roomName}' 방 생성 중...");

            bool ok = await LobbyManager.Instance.CreateLobbyAsync(roomName);
            if (!ok)
            {
                SetStatus("방 생성 실패.");
                SetButtonsInteractable(true);
                _busy = false;
                return;
            }

            // 씬 전환 직전 Start 씬 Canvas를 명시적으로 비활성화
            // NGO LoadScene이 Single 모드여도 일부 환경에서 이전 씬 Canvas가 남아있는 문제 방지
            HideStartSceneUI();

            // 로비 생성 즉시 씬 전환 (Delay 없음 — 클라이언트가 먼저 붙으면 끊길 수 있음)
            NetworkManager.Singleton.SceneManager.LoadScene(
                "01_Scenes/Basic", UnityEngine.SceneManagement.LoadSceneMode.Single);
            SetStatus("접속 완료!");
        }

        // ── Client ────────────────────────────────────────────────────────────
        private void OnClientClicked()
        {
            if (_busy) return;
            _ = GoToLobbyListAsync();
        }

        private async Task GoToLobbyListAsync()
        {
            _busy = true;
            SetButtonsInteractable(false);

            SetStatus("인증 중...");
            await EnsureLobbyManager();
            await LobbyManager.Instance.AuthenticateAsync();

            SetStatus("로비 목록 불러오는 중...");
            SceneManager.LoadScene("01_Scenes/LobbyList");
        }

        // ── Server (플레이어 없이 서버만) ─────────────────────────────────────
        private void OnServerClicked()
        {
            if (_busy) return;
            _ = StartServerAsync();
        }

        private async Task StartServerAsync()
        {
            _busy = true;
            SetButtonsInteractable(false);

            SetStatus("인증 중...");
            await EnsureLobbyManager();
            await LobbyManager.Instance.AuthenticateAsync();

            string roomName = GetRoomName();
            SetStatus($"'{roomName}' 서버 생성 중...");

            bool ok = await LobbyManager.Instance.CreateServerLobbyAsync(roomName);
            if (!ok)
            {
                SetStatus("서버 생성 실패.");
                SetButtonsInteractable(true);
                _busy = false;
                return;
            }

            HideStartSceneUI();
            NetworkManager.Singleton.SceneManager.LoadScene(
                "01_Scenes/Basic", UnityEngine.SceneManagement.LoadSceneMode.Single);
            SetStatus("서버 준비 완료!");
        }

        // ── 공통 헬퍼 ─────────────────────────────────────────────────────────
        private string GetRoomName()
        {
            return (roomNameInput != null && !string.IsNullOrWhiteSpace(roomNameInput.text))
                ? roomNameInput.text.Trim()
                : $"Room_{Random.Range(1000, 9999)}";
        }

        private Task EnsureLobbyManager()
        {
            if (LobbyManager.Instance == null)
                new GameObject("LobbyManager").AddComponent<LobbyManager>();
            return Task.CompletedTask;
        }

        private void SetButtonsInteractable(bool v)
        {
            if (hostButton) hostButton.interactable = v;
            if (clientButton) clientButton.interactable = v;
            if (serverButton) serverButton.interactable = v;
        }

        private void SetStatus(string msg)
        {
            if (statusText) statusText.text = msg;
        }

        // Start 씬의 Canvas 루트를 비활성화 → 새 씬 로드 중 이전 UI가 보이지 않도록 보장
        private void HideStartSceneUI()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }
    }
}