using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using LittleSword.Player;

namespace LittleSword.UI
{
    // GameOverUI: 플레이어 사망 시 게임오버 처리
    // Canvas GameObject에 부착
    public class GameOverUI : MonoBehaviour
    {
        [Header("게임오버 UI")]
        [SerializeField] private GameObject gameOverPanel;  // "Game Over" 패널
        [SerializeField] private Button retryButton;        // 재시작 버튼 (선택)
        [SerializeField] private float delayBeforeReturn = 2f; // 시작 씬으로 돌아가기까지 대기 시간

        private BasePlayer player;

        private void Start()
        {
            player = FindFirstObjectByType<Warrior>() as BasePlayer
                  ?? FindFirstObjectByType<Archer>() as BasePlayer;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);

            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetryClicked);
        }

        private void Update()
        {
            if (player != null && player.IsDead)
            {
                player = null; // 중복 호출 방지
                StartCoroutine(GameOverRoutine());
            }
        }

        private IEnumerator GameOverRoutine()
        {
            // 게임오버 패널 표시
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            // 잠시 대기 후 시작 씬으로 이동
            yield return new WaitForSeconds(delayBeforeReturn);

            SceneManager.LoadScene("Start");
        }

        private void OnRetryClicked()
        {
            SceneManager.LoadScene("Basic");
        }
    }
}