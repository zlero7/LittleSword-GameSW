using UnityEngine;
using UnityEngine.UI;

namespace LittleSword.UI
{
    public class StartSceneUI : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject characterSelectPanel; // �߰�

        private void Start()
        {
            startButton?.onClick.AddListener(OnStartClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);
            characterSelectPanel?.SetActive(false); // ó���� ����

            if (LittleSword.Network.LobbyManager.Instance != null &&
                LittleSword.Network.LobbyManager.Instance.CurrentLobby != null)
            {
                SkipToLobbyPanel();
            }
        }

        private void SkipToLobbyPanel()
        {
            gameObject.SetActive(false);
            characterSelectPanel?.SetActive(false);

            var canvas = GetComponentInParent<Canvas>();
            var lobbyPanel = canvas != null ? canvas.transform.Find("LobbyPanel") : null;
            lobbyPanel?.gameObject.SetActive(true);
        }

        private void OnStartClicked()
        {
            startButton.gameObject.SetActive(false);
            quitButton.gameObject.SetActive(false);
            characterSelectPanel?.SetActive(true);
        }

        private void OnQuitClicked()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}