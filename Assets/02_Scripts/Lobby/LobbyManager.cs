using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace LittleSword.Network
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        private const string KEY_RELAY_JOIN_CODE = "RelayJoinCode";
        private const string KEY_ROOM_NAME = "RoomName";
        private const string KEY_PLAYER_COUNT = "PlayerCount";
        private const int MAX_PLAYERS = 4;
        private string _lobbyUpdateToken;

        public Lobby CurrentLobby { get; private set; }

        public event Action OnAuthenticated;
        public event Action<string> OnError;

        private float _heartbeatTimer;
        private const float HEARTBEAT_INTERVAL = 15f;

        private float _pollTimer;
        private const float POLL_INTERVAL = 2f;
        private bool _waitingForRelay;

        public bool IsAuthenticated { get; private set; }
        private string _pendingJoinCode; // StartHost 후 씬 전환 완료 시 로비에 올릴 JoinCode

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (CurrentLobby != null && IsHost())
            {
                _heartbeatTimer += Time.deltaTime;
                if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
                {
                    _heartbeatTimer = 0f;
                    _ = HeartbeatAsync();
                }
            }
        }

        #region 인증
        public async Task AuthenticateAsync()
        {
            if (IsAuthenticated) return;
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    options.SetEnvironmentName("production");
                    await UnityServices.InitializeAsync(options);
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsAuthenticated = true;
                OnAuthenticated?.Invoke();
                Debug.Log($"[LobbyManager] 인증 완료 – PlayerId: {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] 인증 실패: {e.Message}");
                OnError?.Invoke($"인증 실패: {e.Message}");
            }
        }

        // ✅ 추가: 현재 플레이어가 참여 중인 로비를 모두 조회해서 나감
        private async Task CleanupStaleLobbiesAsync()
        {
            try
            {
                var response = await LobbyService.Instance.GetJoinedLobbiesAsync();
                if (response == null || response.Count == 0) return;

                foreach (var lobbyId in response)
                {
                    try
                    {
                        await LobbyService.Instance.RemovePlayerAsync(
                            lobbyId, AuthenticationService.Instance.PlayerId);
                        Debug.Log($"[LobbyManager] 잔류 로비 정리 완료 – LobbyId: {lobbyId}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[LobbyManager] 잔류 로비 정리 실패 – {lobbyId} / {e.Message}");
                    }
                }

                // ✅ 정리 후 CurrentLobby 초기화 (Heartbeat 오발송 방지)
                CurrentLobby = null;
                _waitingForRelay = false;

                await Task.Delay(1200);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyManager] 잔류 로비 조회 실패: {e.Message}");
            }
        }
        #endregion

        #region 호스트: 로비 생성 + Relay 할당

        private void Start()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            _ = UpdateLobbyPlayerCountAsync();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            _ = UpdateLobbyPlayerCountAsync();
        }

        private async Task UpdateLobbyPlayerCountAsync()
        {
            if (CurrentLobby == null || !IsHost()) return;

            try
            {
                int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
                var updateOptions = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        [KEY_PLAYER_COUNT] = new DataObject(
                            DataObject.VisibilityOptions.Public, playerCount.ToString())
                    }
                };
                CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, updateOptions);
                Debug.Log($"[LobbyManager] 플레이어 수 업데이트: {playerCount}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyManager] 플레이어 수 업데이트 실패: {e.Message}");
            }
        }

        public async Task<bool> CreateLobbyAsync(string roomName)
        {
            try
            {
                // 이전 세션의 잔류 로비를 정리한 뒤 새 방을 생성
                // await CleanupStaleLobbiesAsync();

                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_PLAYERS - 1);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.ConnectionData,
                    allocation.ConnectionData,
                    allocation.Key,
                    false // isSecure: false로 변경 (빌드 안정성)
                ));

                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        [KEY_RELAY_JOIN_CODE] = new DataObject(
                            DataObject.VisibilityOptions.Public, joinCode),
                        [KEY_ROOM_NAME] = new DataObject(
                            DataObject.VisibilityOptions.Public, roomName),
                        [KEY_PLAYER_COUNT] = new DataObject(
                            DataObject.VisibilityOptions.Public, "1")
                    }
                };
                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(
                    roomName, MAX_PLAYERS, options);

                // NetworkManager가 실행 중이면 완전히 종료될 때까지 대기
                if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.ShutdownInProgress)
                {
                    NetworkManager.Singleton.Shutdown();
                    while (NetworkManager.Singleton.ShutdownInProgress) await Task.Yield();
                    await Task.Delay(500);
                }

                if (!NetworkManager.Singleton.StartHost())
                {
                    throw new Exception("NetworkManager.StartHost() 실패");
                }

                // Transport 실패 시 로비 삭제 (JoinCode 무효화 방지)
                NetworkManager.Singleton.OnTransportFailure += OnHostTransportFailure;

                Debug.Log($"[LobbyManager] 로비 생성 완료 – Id:{CurrentLobby.Id}  JoinCode:{joinCode}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] 로비 생성 실패: {e.Message}");
                OnError?.Invoke($"방 생성 실패: {e.Message}");
                return false;
            }
        }
        #endregion

        #region 클라이언트: 로비 입장 + Relay 참여
        public async Task<List<Lobby>> FetchLobbiesAsync()
        {
            try
            {
                var options = new QueryLobbiesOptions { Count = 20 };
                var result = await LobbyService.Instance.QueryLobbiesAsync(options);
                if (result.Results.Count == 0) return new List<Lobby>();

                var freshLobbies = new List<Lobby>();
                foreach (var lobby in result.Results)
                {
                    try
                    {
                        var fresh = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
                        Debug.Log($"[FetchLobbies] {fresh.Name} - Players.Count: {fresh.Players.Count}");
                        freshLobbies.Add(fresh);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[FetchLobbies] GetLobbyAsync 실패: {ex.Message}");
                        freshLobbies.Add(lobby);
                    }
                }
                return freshLobbies;
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.RateLimited)
            {
                // ✅ Rate Limit 걸렸을 때 1.2초 후 1회 재시도
                Debug.LogWarning("[LobbyManager] QueryLobbies Rate Limit — 재시도 중...");
                await Task.Delay(1200);
                try
                {
                    var options = new QueryLobbiesOptions { Count = 20 };
                    return (await LobbyService.Instance.QueryLobbiesAsync(options)).Results;
                }
                catch (Exception retryEx)
                {
                    Debug.LogError($"[LobbyManager] 로비 목록 재시도 실패: {retryEx.Message}");
                    OnError?.Invoke($"방 목록 불러오기 실패: {retryEx.Message}");
                    return new List<Lobby>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] 로비 목록 조회 실패: {e.Message}");
                OnError?.Invoke($"방 목록 불러오기 실패: {e.Message}");
                return new List<Lobby>();
            }
        }


        /// <summary>서버 전용 로비 생성. Relay 세팅 후 StartServer() 호출 (플레이어 없음)</summary>
        public async Task<bool> CreateServerLobbyAsync(string roomName)
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_PLAYERS);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.ConnectionData,
                    allocation.ConnectionData,
                    allocation.Key,
                    false
                ));

                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        [KEY_RELAY_JOIN_CODE] = new DataObject(DataObject.VisibilityOptions.Public, joinCode),
                        [KEY_ROOM_NAME] = new DataObject(DataObject.VisibilityOptions.Public, roomName),
                        [KEY_PLAYER_COUNT] = new DataObject(DataObject.VisibilityOptions.Public, "0")
                    }
                };

                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, MAX_PLAYERS, options);

                if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.ShutdownInProgress)
                {
                    NetworkManager.Singleton.Shutdown();
                    while (NetworkManager.Singleton.ShutdownInProgress) await Task.Yield();
                    await Task.Delay(500);
                }

                if (!NetworkManager.Singleton.StartServer())
                    throw new Exception("NetworkManager.StartServer() 실패");

                NetworkManager.Singleton.OnTransportFailure += OnHostTransportFailure;
                Debug.Log($"[LobbyManager] 서버 전용 시작 Id:{CurrentLobby.Id}  JoinCode:{joinCode}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] CreateServerLobbyAsync 실패: {e.Message}");
                OnError?.Invoke($"서버 생성 실패: {e.Message}");
                return false;
            }
        }

        public async Task<bool> JoinLobbyAsync(Lobby lobby)
        {
            try
            {
                // 이미 동일한 로비에 입장되어 있으면 JoinLobbyByIdAsync를 재호출하지 않음
                if (CurrentLobby != null && CurrentLobby.Id == lobby.Id)
                {
                    Debug.Log("[LobbyManager] 이미 입장된 로비입니다. 로비 정보를 재조회합니다.");
                    CurrentLobby = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
                }
                else
                {
                    try
                    {
                        CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);
                    }
                    catch (LobbyServiceException e) when (e.ApiError.Status == 409)
                    {
                        // 이전 세션에서 이미 입장된 경우(409 Conflict) — 재입장 없이 데이터 조회
                        Debug.LogWarning("[LobbyManager] 이미 로비 멤버 — GetLobbyAsync로 재조회");
                        CurrentLobby = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
                    }
                }

                // RelayJoinCode가 나타날 때까지 최대 10초간 대기 (폴링 루프)
                string joinCode = null;
                int retryCount = 0;
                const int MAX_RETRIES = 5;

                while (retryCount < MAX_RETRIES)
                {
                    if (CurrentLobby.Data != null &&
                        CurrentLobby.Data.TryGetValue(KEY_RELAY_JOIN_CODE, out var codeEntry) &&
                        !string.IsNullOrEmpty(codeEntry.Value))
                    {
                        joinCode = codeEntry.Value;
                        break;
                    }

                    Debug.Log($"[LobbyManager] RelayJoinCode 대기 중... ({retryCount + 1}/{MAX_RETRIES})");
                    await Task.Delay(2000); // 2초 간격으로 재조회
                    CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                    retryCount++;
                }

                Debug.Log($"[LobbyManager] 로비 입장 – 읽은 JoinCode: {joinCode ?? "없음"}");

                if (!string.IsNullOrEmpty(joinCode))
                {
                    bool connected = await ConnectRelayAsClientAsync(joinCode);
                    if (!connected)
                    {
                        // Relay 연결 실패 — 로비에서 나가 재시도 가능하게 함
                        await LeaveLobbyAsync();
                        return false;
                    }
                    return true;
                }

                Debug.LogError("[LobbyManager] RelayJoinCode를 찾을 수 없습니다 (타임아웃).");
                OnError?.Invoke("호스트로부터 연결 코드를 받지 못했습니다.");
                await LeaveLobbyAsync();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] 로비 입장 실패: {e.Message}");
                OnError?.Invoke($"방 입장 실패: {e.Message}");
                return false;
            }
        }

        private async Task<bool> ConnectRelayAsClientAsync(string joinCode)
        {
            try
            {
                Debug.Log($"[LobbyManager] Relay 연결 시도 – JoinCode: {joinCode}");
                JoinAllocation join = await RelayService.Instance.JoinAllocationAsync(joinCode);

                // NetworkManager가 실행 중이면 완전히 종료될 때까지 대기
                if (NetworkManager.Singleton.IsListening || NetworkManager.Singleton.ShutdownInProgress)
                {
                    NetworkManager.Singleton.Shutdown();
                    while (NetworkManager.Singleton.ShutdownInProgress) await Task.Yield();
                    await Task.Delay(500);
                }

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(
                    join.RelayServer.IpV4,
                    (ushort)join.RelayServer.Port,
                    join.AllocationIdBytes,
                    join.ConnectionData,
                    join.HostConnectionData,
                    join.Key,
                    false // isSecure: false로 변경 (호스트와 일치)
                ));

                // 연결 실패/끊김 시 OnError를 발행해 UI 잠금 해제
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientForcedDisconnect;

                if (!NetworkManager.Singleton.StartClient())
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientForcedDisconnect;
                    throw new Exception("NetworkManager.StartClient() 실패");
                }

                _waitingForRelay = false;
                Debug.Log($"[LobbyManager] Relay 클라이언트 연결 시작 – JoinCode: {joinCode}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] Relay 연결 실패: {e.Message}");
                OnError?.Invoke($"방 입장 실패: {e.Message}");
                _waitingForRelay = false;
                return false;
            }
        }

        private void OnClientForcedDisconnect(ulong clientId)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientForcedDisconnect;
            // 서버(호스트) 인스턴스는 이 콜백을 무시
            if (NetworkManager.Singleton.IsServer) return;
            Debug.LogWarning("[LobbyManager] 호스트와의 연결이 끊어졌습니다.");
            _ = LeaveLobbyAsync();
            OnError?.Invoke("호스트와의 연결이 끊어졌습니다.\n다시 시도하세요.");
        }

        private void OnHostTransportFailure()
        {
            NetworkManager.Singleton.OnTransportFailure -= OnHostTransportFailure;
            Debug.LogWarning("[LobbyManager] Host Transport 실패 — 로비 삭제");
            _ = LeaveLobbyAsync();
        }
        #endregion

        #region 로비 나가기 / 삭제
        public async Task LeaveLobbyAsync()
        {
            if (CurrentLobby == null) return;
            try
            {
                if (IsHost())
                {
                    // 이 인스턴스가 실제 NetworkManager 서버(호스트)인 경우만 로비 삭제
                    await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                }
                else if (CurrentLobby.HostId != AuthenticationService.Instance.PlayerId)
                {
                    // 로비 호스트가 아닌 일반 클라이언트만 RemovePlayer 호출
                    // (같은 머신 테스트 시 Player ID가 같더라도 HOST 로비를 삭제하지 않음)
                    await LobbyService.Instance.RemovePlayerAsync(
                        CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
                }
                // else: Player ID는 호스트와 같지만 이 인스턴스는 서버가 아님 → 로컬 상태만 초기화
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyManager] 로비 나가기 실패: {e.Message}");
            }
            finally
            {
                CurrentLobby = null;
                _waitingForRelay = false;
            }
        }
        #endregion

        #region 빠른 참여 / 코드 입장

        public async Task<bool> QuickJoinLobbyAsync()
        {
            try
            {
                var lobbies = await FetchLobbiesAsync();
                var available = lobbies.Find(l => l.Players.Count < l.MaxPlayers);
                if (available == null)
                {
                    OnError?.Invoke("참여 가능한 방이 없습니다.");
                    return false;
                }
                return await JoinLobbyAsync(available);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] 빠른 참여 실패: {e.Message}");
                OnError?.Invoke($"빠른 참여 실패: {e.Message}");
                return false;
            }
        }

        public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
        {
            try
            {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim());

                string joinCode = null;
                int retryCount = 0;
                const int MAX_RETRIES = 5;

                while (retryCount < MAX_RETRIES)
                {
                    if (CurrentLobby.Data != null &&
                        CurrentLobby.Data.TryGetValue(KEY_RELAY_JOIN_CODE, out var entry) &&
                        !string.IsNullOrEmpty(entry.Value))
                    {
                        joinCode = entry.Value;
                        break;
                    }
                    Debug.Log($"[LobbyManager] 코드 입장 대기 중... ({retryCount + 1}/{MAX_RETRIES})");
                    await Task.Delay(2000);
                    CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                    retryCount++;
                }

                if (!string.IsNullOrEmpty(joinCode))
                    return await ConnectRelayAsClientAsync(joinCode);

                OnError?.Invoke("호스트로부터 연결 코드를 받지 못했습니다.");
                await LeaveLobbyAsync();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] 코드 입장 실패: {e.Message}");
                OnError?.Invoke($"코드 입장 실패: {e.Message}");
                return false;
            }
        }

        public string GetCurrentLobbyCode() => CurrentLobby?.LobbyCode;

        #endregion

        #region 헬퍼
        private bool IsHost() =>
            CurrentLobby != null &&
            AuthenticationService.Instance.IsSignedIn &&
            CurrentLobby.HostId == AuthenticationService.Instance.PlayerId &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer;

        /// <summary>
        /// 서버가 Basic 씬 로드를 완료했을 때 호출.
        /// 로비에 JoinCode를 공개해 클라이언트가 접속할 수 있게 함.
        /// </summary>
        public async Task PublishJoinCodeAsync()
        {
            if (CurrentLobby == null || string.IsNullOrEmpty(_pendingJoinCode)) return;
            try
            {
                var updateOptions = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        [KEY_RELAY_JOIN_CODE] = new DataObject(
                            DataObject.VisibilityOptions.Public, _pendingJoinCode)
                    }
                };
                CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, updateOptions);
                Debug.Log($"[LobbyManager] JoinCode 공개 완료: {_pendingJoinCode}");
                _pendingJoinCode = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] JoinCode 공개 실패: {e.Message}");
            }
        }

        private async Task HeartbeatAsync()
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyManager] Heartbeat 실패: {e.Message}");
            }
        }
        #endregion

        private async void OnApplicationQuit()
        {
            await LeaveLobbyAsync();
        }
    }
}