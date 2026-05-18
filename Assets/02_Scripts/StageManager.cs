using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using LittleSword.Enemy.Stats;

namespace LittleSword
{
    public class StageManager : NetworkBehaviour
    {
        public static StageManager Instance { get; private set; }

        [Header("스테이지 설정")]
        [SerializeField] private int currentStage = 1;
        [SerializeField] private int bossStageInterval = 5;

        [Header("보스 프리팹")]
        [SerializeField] private GameObject bossPrefab;
        [SerializeField] private Transform bossSpawnPoint;

        [Header("일반 적 스폰 설정")]
        [SerializeField] private List<GameObject> enemyPrefabs;
        [SerializeField] private List<Transform> spawnPoints;
        [SerializeField] private int baseEnemyCount = 5;

        [Header("클리어 연출")]
        [SerializeField] private float clearDelay = 2f;
        [SerializeField] private GameObject clearEffect;

        public event Action<int> OnStageStart;
        public event Action<int> OnStageClear;
        public event Action OnBossStage;
        public event Action<int> OnBossKill;

        private List<GameObject> activeEnemies = new List<GameObject>();
        private bool isBossStage = false;
        private bool isClearing = false;

        // ✅ 클라이언트에서도 읽을 수 있도록 NetworkVariable로 변경
        private NetworkVariable<int> networkRemainingEnemies = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public bool IsBossStage => isBossStage;
        public int CurrentStage => currentStage;
        public int RemainingEnemies => networkRemainingEnemies.Value; // ✅ NetworkVariable에서 읽기

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[StageManager] OnNetworkSpawn! IsServer: {IsServer}");
            if (IsServer)
                StartStage(currentStage);
        }

        public void StartStage(int stage)
        {
            if (!IsServer) return;

            currentStage = stage;
            isClearing = false;
            activeEnemies.Clear();
            networkRemainingEnemies.Value = 0; // ✅ 초기화

            isBossStage = (stage % bossStageInterval == 0);

            Debug.Log($"[Stage] 스테이지 {stage} 시작! (보스: {isBossStage})");

            NotifyStageStartClientRpc(stage, isBossStage);

            if (isBossStage)
                SpawnBoss();
            else
                SpawnEnemies();
        }

        [ClientRpc]
        private void NotifyStageStartClientRpc(int stage, bool isBoss)
        {
            OnStageStart?.Invoke(stage);
            if (isBoss) OnBossStage?.Invoke();
        }

        private void SpawnEnemies()
        {
            if (!IsServer) return;
            if (enemyPrefabs.Count == 0 || spawnPoints.Count == 0) return;

            int count = baseEnemyCount + (currentStage - 1) * 2;

            for (int i = 0; i < count; i++)
            {
                Transform spawnPoint = spawnPoints[i % spawnPoints.Count];
                GameObject prefab = enemyPrefabs[UnityEngine.Random.Range(0, enemyPrefabs.Count)];
                GameObject enemy = Instantiate(prefab, spawnPoint.position, Quaternion.identity);

                enemy.GetComponent<NetworkObject>().Spawn();
                activeEnemies.Add(enemy);
                ScaleEnemyStats(enemy);
            }

            networkRemainingEnemies.Value = activeEnemies.Count; // ✅ 스폰 후 동기화
            Debug.Log($"[StageManager] 스폰 완료, activeEnemies 수: {activeEnemies.Count}");
        }

        private void SpawnBoss()
        {
            if (!IsServer) return;
            if (bossPrefab == null || bossSpawnPoint == null) return;

            GameObject boss = Instantiate(bossPrefab, bossSpawnPoint.position, Quaternion.identity);
            boss.GetComponent<NetworkObject>().Spawn();
            activeEnemies.Add(boss);
            ScaleEnemyStats(boss, isBoss: true);

            networkRemainingEnemies.Value = activeEnemies.Count; // ✅ 스폰 후 동기화
        }

        private void ScaleEnemyStats(GameObject enemy, bool isBoss = false)
        {
            var enemyComp = enemy.GetComponent<LittleSword.Enemy.Enemy>();
            if (enemyComp == null || enemyComp.enemyStats == null) return;

            EnemyStats scaledStats = Instantiate(enemyComp.enemyStats);

            float multiplier = 1f + (currentStage - 1) * 0.15f;
            if (isBoss) multiplier *= 3f;

            scaledStats.maxHP = Mathf.RoundToInt(scaledStats.maxHP * multiplier);
            scaledStats.attackDamage = Mathf.RoundToInt(scaledStats.attackDamage * multiplier);

            enemyComp.enemyStats = scaledStats;
        }

        public void OnEnemyDead(GameObject enemy)
        {
            if (!IsServer) return;

            bool wasBossStage = isBossStage;
            activeEnemies.Remove(enemy);
            activeEnemies.RemoveAll(e => e == null);

            networkRemainingEnemies.Value = activeEnemies.Count; // ✅ 적 사망 시 동기화

            if (activeEnemies.Count == 0 && !isClearing)
            {
                if (wasBossStage)
                    NotifyBossKillClientRpc(currentStage);
                StartCoroutine(StageClearRoutine());
            }
        }

        [ClientRpc]
        private void NotifyBossKillClientRpc(int stage)
        {
            OnBossKill?.Invoke(stage);
        }

        private IEnumerator StageClearRoutine()
        {
            isClearing = true;
            Debug.Log($"[Stage] 스테이지 {currentStage} 클리어!");

            if (clearEffect != null)
                Instantiate(clearEffect, Camera.main.transform.position + Vector3.forward * 5f, Quaternion.identity);

            NotifyStageClearClientRpc(currentStage);

            yield return new WaitForSeconds(clearDelay);

            StartStage(currentStage + 1);
        }

        [ClientRpc]
        private void NotifyStageClearClientRpc(int stage)
        {
            OnStageClear?.Invoke(stage);
        }
    }
}