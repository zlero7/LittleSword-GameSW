using UnityEngine;

namespace LittleSword.Equipment
{
    // 장비 등급
    public enum EquipmentGrade
    {
        Common,     // 일반 (흰색)
        Rare,       // 희귀 (파란색)
        Epic,       // 영웅 (보라색)
        Legendary   // 전설 (노란색)
    }

    // 장비 종류
    public enum EquipmentType
    {
        Weapon,     // 무기
        Armor       // 갑옷
    }

    // EquipmentData: 장비 ScriptableObject
    // Create > LittleSword > Equipment 으로 새 장비 아이템 생성
    [CreateAssetMenu(fileName = "NewEquipment", menuName = "LittleSword/Equipment", order = 2)]
    public class EquipmentData : ScriptableObject
    {
        [Header("기본 정보")]
        public string itemName = "장비 이름";
        [TextArea] public string description = "장비 설명";
        public Sprite icon;                     // 인벤토리 아이콘

        [Header("장비 정보")]
        public EquipmentType equipmentType;
        public EquipmentGrade grade;

        [Header("스탯 보너스")]
        public int bonusHP = 0;                 // 최대 HP 증가
        public int bonusAttack = 0;             // 공격력 증가
        public float bonusSpeed = 0f;           // 이동속도 증가
        public float bonusFireForce = 0f;       // 화살 속도 증가 (아처 전용)

        // 장비 등급에 따른 색상 반환 (UI 표시용)
        public Color GetGradeColor()
        {
            return grade switch
            {
                EquipmentGrade.Common => Color.white,
                EquipmentGrade.Rare => new Color(0.3f, 0.5f, 1f),      // 파란색
                EquipmentGrade.Epic => new Color(0.7f, 0.3f, 1f),      // 보라색
                EquipmentGrade.Legendary => new Color(1f, 0.8f, 0.1f), // 노란색
                _ => Color.white
            };
        }
    }
}
