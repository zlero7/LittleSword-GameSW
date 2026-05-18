using UnityEngine;
using Unity.Netcode;

namespace LittleSword
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject warriorPrefab;
        [SerializeField] private GameObject archerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // Basic 씬 로드 완료 — JoinCode를 로비에 공개해 클라이언트 접속 허용
            _ = PublishJoinCodeAndListenAsync();

            // 순수 서버(Host 아님)면 플레이어 없이 카메라만 생성
            if (!NetworkManager.Singleton.IsHost)
            {
                SpawnServerCamera();
                return;
            }

        }

        private async System.Threading.Tasks.Task PublishJoinCodeAndListenAsync()
        {
            if (LittleSword.Network.LobbyManager.Instance != null)
                await LittleSword.Network.LobbyManager.Instance.PublishJoinCodeAsync();

            // JoinCode 공개 후 신규 클라이언트 접속 콜백 등록
            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;

            // Host면 자기 자신도 스폰
            if (NetworkManager.Singleton.IsHost)
                SpawnPlayer(NetworkManager.Singleton.LocalClientId);
        }

        private void SpawnServerCamera()
        {
            var go = new GameObject("ServerCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;

            Vector3 center = Vector3.zero;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                foreach (var sp in spawnPoints) center += sp.position;
                center /= spawnPoints.Length;
            }
            go.transform.position = new Vector3(center.x, center.y, -15f);

            go.AddComponent<ServerFreeCamera>();
            Debug.Log("[PlayerSpawner] 서버 전용 카메라 생성");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayer;
        }

        private void SpawnPlayer(ulong clientId)
        {
            AskCharacterTypeClientRpc(clientId);
        }

        [ClientRpc]
        private void AskCharacterTypeClientRpc(ulong targetId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetId) return;
            var type = CharacterSelectData.Instance != null
                ? (int)CharacterSelectData.Instance.SelectedCharacter : 0;
            ReplyServerRpc(targetId, type);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReplyServerRpc(ulong clientId, int typeIndex)
        {
            var prefab = typeIndex == (int)CharacterType.Archer ? archerPrefab : warriorPrefab;
            if (prefab == null) return;

            Vector3 pos = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[clientId % (ulong)spawnPoints.Length].position
                : Vector3.zero;

            var go = Instantiate(prefab, pos, Quaternion.identity);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }
    }
}