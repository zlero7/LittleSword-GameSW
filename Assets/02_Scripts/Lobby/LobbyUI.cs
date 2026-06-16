using System.Collections;
using System.Threading.Tasks;
using LittleSword.Network;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LittleSword.UI
{
    /// <summary>
    /// 로비 화면 전체 버튼/입력 처리.
    ///
    /// Canvas 구성 예시:
    ///  [왼쪽 패널]                  [오른쪽 패널]
    ///  LobbyNameInput               CreateLobbyButton
    ///  LobbyCodeInput               JoinLobbyButton
    ///  CreateSessionButton          QuickJoinLobbyButton
    ///  QuickJoinSessionButton       LeaveLobbyButton
    ///  StartSessionButton           StartGameButton
    ///
    /// 버튼 매핑:
    ///  Create Session  = Create Lobby  (방 생성)
    ///  Quick Join Session = Quick Join Lobby (빠른 참여)
    ///  Start Session   = Start Game    (호스트 전용 게임 시작)
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("입력 필드")]
        [SerializeField] private TMP_InputField lobbyNameInput;
        [SerializeField] private TMP_InputField lobbyCodeInput;

        [Header("왼쪽 패널 버튼")]
        [SerializeField] private Button createSessionButton;
        [SerializeField] private Button quickJoinSessionButton;
        [SerializeField] private Button startSessionButton;

        [Header("오른쪽 패널 버튼")]
        [SerializeField] private Button createLobbyButton;
        [SerializeField] private Button joinLobbyButton;
        [SerializeField] private Button quickJoinLobbyButton;
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private Button startGameButton;

        [Header("상태 표시")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private TextMeshProUGUI lobbyCodeDisplayText;

        private bool _busy;
        private bool _inLobby;
        private Coroutine _pollCoroutine;

        private enum LobbyState { Initial, Hosting, Joined, Busy }

        private void Start()
        {
            // 왼쪽 패널 — 호스트 워크플로우
            createSessionButton?.onClick.AddListener(() => { if (!_busy) _ = CreateAsync(); });
            quickJoinSessionButton?.onClick.AddListener(() => { if (!_busy) _ = QuickJoinAsync(); });
            startSessionButton?.onClick.AddListener(() => { if (!_busy) OnStartGame(); });

            // 오른쪽 패널 — 클라이언트 워크플로우
            createLobbyButton?.onClick.AddListener(() => { if (!_busy) _ = CreateAsync(); });
            joinLobbyButton?.onClick.AddListener(() => { if (!_busy) _ = GoToListAsync(); });
            quickJoinLobbyButton?.onClick.AddListener(() => { if (!_busy) _ = QuickJoinAsync(); });
            leaveLobbyButton?.onClick.AddListener(() => { if (!_busy) _ = LeaveAsync(); });
            startGameButton?.onClick.AddListener(() => { if (!_busy) OnStartGame(); });

            EnsureLobbyManager();
            SetUI(LobbyState.Initial);
            SetStatus("방을 만들거나 참여하세요.");

            // 씬 전환 시작 즉시 이 Canvas를 제거 (DontDestroyOnLoad에 잔존 방지)
            if (NetworkManager.Singleton?.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnNGOSceneEvent;
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnError -= OnError;
            StopPoll();
            if (NetworkManager.Singleton?.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnNGOSceneEvent;
        }

        private void OnNGOSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneEventType != SceneEventType.Load) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) Destroy(canvas.gameObject);
        }

        // ── 방 생성 ─────────────────────────────────────────────────────────
        private async Task CreateAsync()
        {
            _busy = true;
            SetUI(LobbyState.Busy);
            await EnsureAuth();

            string name = GetLobbyName();
            SetStatus($"'{name}' 방 생성 중...");

            bool ok = await LobbyManager.Instance.CreateLobbyAsync(name);
            if (!ok)
            {
                SetStatus("방 생성에 실패했습니다.");
                SetUI(LobbyState.Initial);
                _busy = false;
                return;
            }

            _inLobby = true;
            _busy = false;

            string code = LobbyManager.Instance.GetCurrentLobbyCode();
            if (lobbyCodeDisplayText != null)
                lobbyCodeDisplayText.text = $"로비 코드: {code}";

            SetStatus($"'{name}' 생성 완료. 플레이어를 기다리는 중...");
            SetUI(LobbyState.Hosting);
            StartPoll();
        }

        // ── 빠른 참여 / 코드 참여 ────────────────────────────────────────────
        private async Task QuickJoinAsync()
        {
            _busy = true;
            SetUI(LobbyState.Busy);
            await EnsureAuth();

            bool ok;
            string code = lobbyCodeInput != null ? lobbyCodeInput.text.Trim() : "";

            if (!string.IsNullOrEmpty(code))
            {
                SetStatus($"코드 '{code}' 로 방 입장 중...");
                ok = await LobbyManager.Instance.JoinLobbyByCodeAsync(code);
            }
            else
            {
                SetStatus("빈 방을 찾는 중...");
                ok = await LobbyManager.Instance.QuickJoinLobbyAsync();
            }

            if (!ok)
            {
                SetStatus("참여에 실패했습니다. 다시 시도하세요.");
                SetUI(LobbyState.Initial);
                _busy = false;
                return;
            }

            _inLobby = true;
            _busy = false;
            SetStatus("방에 참여했습니다. 호스트가 게임을 시작할 때까지 기다리세요.");
            SetUI(LobbyState.Joined);
        }

        // ── 로비 목록 이동 ───────────────────────────────────────────────────
        private async Task GoToListAsync()
        {
            _busy = true;
            SetUI(LobbyState.Busy);
            SetStatus("로비 목록으로 이동 중...");
            await EnsureAuth();
            SceneManager.LoadScene("01_Scenes/LobbyList");
        }

        // ── 로비 나가기 ──────────────────────────────────────────────────────
        private async Task LeaveAsync()
        {
            _busy = true;
            SetUI(LobbyState.Busy);
            StopPoll();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            if (LobbyManager.Instance != null)
                await LobbyManager.Instance.LeaveLobbyAsync();

            _inLobby = false;
            _busy = false;

            ClearCurrentLobby();

            SetStatus("로비에서 나왔습니다.");
            SetUI(LobbyState.Initial);
        }

        // 로비 정보를 초기화하여 UI를 기본 상태로 되돌린다.
        // (입력 필드 + 로비 코드/플레이어 수 표시 초기화)
        private void ClearCurrentLobby()
        {
            if (lobbyNameInput != null) lobbyNameInput.text = "";
            if (lobbyCodeInput != null) lobbyCodeInput.text = "";
            if (lobbyCodeDisplayText != null) lobbyCodeDisplayText.text = "";
            if (playerCountText != null) playerCountText.text = "";
        }

        // ── 게임 시작 (호스트 전용) ──────────────────────────────────────────
        private void OnStartGame()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            {
                SetStatus("호스트만 게임을 시작할 수 있습니다.");
                return;
            }

            StopPoll();
            HideCanvas();
            NetworkManager.Singleton.SceneManager.LoadScene(
                "01_Scenes/Basic", LoadSceneMode.Single);
        }

        // ── 로비 폴링 (방 인원 수 갱신) ──────────────────────────────────────
        private void StartPoll() =>
            _pollCoroutine = StartCoroutine(PollCoroutine());

        private void StopPoll()
        {
            if (_pollCoroutine != null) { StopCoroutine(_pollCoroutine); _pollCoroutine = null; }
        }

        private IEnumerator PollCoroutine()
        {
            while (_inLobby)
            {
                yield return new WaitForSeconds(2f);
                _ = RefreshPlayerCountAsync();
            }
        }

        private async Task RefreshPlayerCountAsync()
        {
            if (LobbyManager.Instance?.CurrentLobby == null) return;
            try
            {
                var lobby = await LobbyService.Instance
                    .GetLobbyAsync(LobbyManager.Instance.CurrentLobby.Id);
                if (playerCountText != null)
                    playerCountText.text = $"플레이어: {lobby.Players.Count} / {lobby.MaxPlayers}";
            }
            catch { }
        }

        // ── UI 상태 관리 ─────────────────────────────────────────────────────
        private void SetUI(LobbyState state)
        {
            bool initial = state == LobbyState.Initial;
            bool hosting = state == LobbyState.Hosting;
            bool joined  = state == LobbyState.Joined;
            bool isHost  = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

            // 입력 필드
            SetInputInteractable(lobbyNameInput, initial);
            SetInputInteractable(lobbyCodeInput, initial);

            // 왼쪽 패널
            SetBtn(createSessionButton,   initial);
            SetBtn(quickJoinSessionButton, initial);
            SetBtn(startSessionButton,    (hosting || joined) && isHost);

            // 오른쪽 패널
            SetBtn(createLobbyButton,    initial);
            SetBtn(joinLobbyButton,      initial);
            SetBtn(quickJoinLobbyButton, initial);
            SetBtn(leaveLobbyButton,     hosting || joined);
            SetBtn(startGameButton,      (hosting || joined) && isHost);
        }

        private void SetBtn(Button btn, bool interactable)
        {
            if (btn == null) return;
            btn.interactable = interactable;
        }

        private void SetInputInteractable(TMP_InputField field, bool v)
        {
            if (field != null) field.interactable = v;
        }

        private void SetStatus(string msg)
        {
            if (statusText != null) statusText.text = msg;
        }

        private void HideCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────────
        private string GetLobbyName()
        {
            return lobbyNameInput != null && !string.IsNullOrWhiteSpace(lobbyNameInput.text)
                ? lobbyNameInput.text.Trim()
                : $"Room_{Random.Range(1000, 9999)}";
        }

        private async Task EnsureAuth()
        {
            EnsureLobbyManager();
            if (!LobbyManager.Instance.IsAuthenticated)
                await LobbyManager.Instance.AuthenticateAsync();
        }

        private void EnsureLobbyManager()
        {
            if (LobbyManager.Instance == null)
                new GameObject("LobbyManager").AddComponent<LobbyManager>();
            LobbyManager.Instance.OnError -= OnError;
            LobbyManager.Instance.OnError += OnError;
        }

        private void OnError(string message)
        {
            SetStatus($"오류: {message}");
            var s = _inLobby
                ? (NetworkManager.Singleton?.IsHost == true ? LobbyState.Hosting : LobbyState.Joined)
                : LobbyState.Initial;
            SetUI(s);
            _busy = false;
        }
    }
}
