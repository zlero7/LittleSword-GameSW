using UnityEngine;

namespace LittleSword
{
    public enum CharacterType { Warrior, Archer }

    public class CharacterSelectData : MonoBehaviour
    {
        public static CharacterSelectData Instance { get; private set; }
        public CharacterType SelectedCharacter { get; private set; } = CharacterType.Warrior;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetCharacter(CharacterType type) => SelectedCharacter = type;
    }
}