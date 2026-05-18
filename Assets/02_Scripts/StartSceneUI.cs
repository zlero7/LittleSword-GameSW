using UnityEngine;
using UnityEngine.UI;

namespace LittleSword.UI
{
    public class StartSceneUI : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject characterSelectPanel; // Ãß°¡

        private void Start()
        {
            startButton?.onClick.AddListener(OnStartClicked);
            quitButton?.onClick.AddListener(OnQuitClicked);
            characterSelectPanel?.SetActive(false); // Ã³À½¿£ ¼û±è
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