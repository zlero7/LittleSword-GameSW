using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LittleSword.UI
{
    public class CharacterSelectUI : MonoBehaviour
    {
        [SerializeField] private Button warriorButton;
        [SerializeField] private Button archerButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI descriptionText; // 선택사항
        [SerializeField] private GameObject networkUIPanel; // Host/Join 버튼 패널

        private CharacterType currentSelection = CharacterType.Warrior;

        private void Awake()
        {
            if (CharacterSelectData.Instance == null)
                new GameObject("CharacterSelectData").AddComponent<CharacterSelectData>();
        }

        private void Start()
        {
            warriorButton?.onClick.AddListener(() => Select(CharacterType.Warrior));
            archerButton?.onClick.AddListener(() => Select(CharacterType.Archer));
            confirmButton?.onClick.AddListener(OnConfirm);
            Select(CharacterType.Warrior);
        }

        private void Select(CharacterType type)
        {
            currentSelection = type;
            if (descriptionText != null)
                descriptionText.text = type == CharacterType.Warrior
                    ? "워리어: 근접 콤보 공격"
                    : "아처: 원거리 화살 공격";
        }

        private void OnConfirm()
        {
            CharacterSelectData.Instance.SetCharacter(currentSelection);
            gameObject.SetActive(false);
            networkUIPanel?.SetActive(true);
        }
    }
}