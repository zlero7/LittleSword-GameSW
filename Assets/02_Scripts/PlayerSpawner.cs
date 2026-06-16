using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace LittleSword
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject warriorPrefab;
        [SerializeField] private GameObject archerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        private readonly HashSet<ulong> spawnedClients = new HashSet<ulong>();

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayer;

            if (!NetworkManager.Singleton.IsHost)
            {
                SpawnServerCamera();
                return;
            }

            // Spawn every already-connected client (includes host + clients that joined before scene load)
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                SpawnPlayer(clientId);
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
            if (!spawnedClients.Add(clientId))
            {
                Debug.Log($"[PlayerSpawner] SKIP duplicate spawn for clientId={clientId}");
                return;
            }
            Debug.Log($"[PlayerSpawner] AskCharacterType → clientId={clientId}");
            AskCharacterTypeClientRpc(clientId);
        }

        [ClientRpc]
        private void AskCharacterTypeClientRpc(ulong targetId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetId) return;
            var type = CharacterSelectData.Instance != null
                ? (int)CharacterSelectData.Instance.SelectedCharacter : 0;
            Debug.Log($"[PlayerSpawner] Reply clientId={targetId} typeIndex={type}");
            ReplyServerRpc(targetId, type);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ReplyServerRpc(ulong clientId, int typeIndex)
        {
            Debug.Log($"[PlayerSpawner] ReplyServerRpc clientId={clientId} typeIndex={typeIndex}");
            var prefab = typeIndex == (int)CharacterType.Archer ? archerPrefab : warriorPrefab;
            if (prefab == null) { Debug.LogError($"[PlayerSpawner] prefab null! typeIndex={typeIndex}"); return; }

            Vector3 basePos;
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                // 스폰 포인트가 여러 개이면 순환 배정, 하나뿐이면 clientId 순서로 옆으로 오프셋
                if (spawnPoints.Length > 1)
                    basePos = spawnPoints[clientId % (ulong)spawnPoints.Length].position;
                else
                    basePos = spawnPoints[0].position + new Vector3((int)clientId * 2f, 0f, 0f);
            }
            else
            {
                basePos = new Vector3((int)clientId * 2f, 0f, 0f);
            }

            var go = Instantiate(prefab, basePos, Quaternion.identity);
            // destroyWithScene는 반드시 false. true로 두면, 클라이언트가 아직 Basic 씬 로딩을
            // 끝내기 전(Start 씬)에 이 플레이어 스폰을 받았을 때 Start 씬에 생성되고,
            // 직후 LoadSceneMode.Single로 Basic을 로드하면 Start 언로드와 함께 파괴되어
            // 다른 플레이어가 클라이언트에서 사라진다(이후 그 NetworkObject로 오는 메시지가
            // [Deferred OnSpawn] 타임아웃 → 연결 해제까지 유발). false면 씬 전환에도 유지된다.
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, false);
            Debug.Log($"[PlayerSpawner] Spawned {prefab.name} for clientId={clientId} at {basePos}");
        }
    }
}