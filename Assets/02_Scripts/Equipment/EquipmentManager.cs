using UnityEngine;
using System;
using LittleSword.Player;

namespace LittleSword.Equipment
{
    // EquipmentManager: 장비 장착/해제 및 스탯 반영
    // BasePlayer와 같은 GameObject에 부착
    public class EquipmentManager : MonoBehaviour
    {
        private BasePlayer player;
        private PlayerStats playerStats;

        // 현재 장착 슬롯
        private EquipmentData equippedWeapon;
        private EquipmentData equippedArmor;

        // 이벤트
        public event Action<EquipmentData> OnEquip;
        public event Action<EquipmentData> OnUnequip;

        private void Awake()
        {
            player = GetComponent<BasePlayer>();
            playerStats = player?.playerStats;
        }

        // 장비 장착
        public void Equip(EquipmentData data)
        {
            if (data == null) return;

            // 같은 슬롯에 기존 장비가 있으면 먼저 해제
            if (data.equipmentType == EquipmentType.Weapon && equippedWeapon != null)
                Unequip(equippedWeapon);
            else if (data.equipmentType == EquipmentType.Armor && equippedArmor != null)
                Unequip(equippedArmor);

            // 스탯 적용
            ApplyStats(data, add: true);

            // 슬롯 등록
            if (data.equipmentType == EquipmentType.Weapon) equippedWeapon = data;
            else equippedArmor = data;

            OnEquip?.Invoke(data);
            Debug.Log($"[Equipment] {data.itemName} 장착 완료");
        }

        // 장비 해제
        public void Unequip(EquipmentData data)
        {
            if (data == null) return;

            // 스탯 제거
            ApplyStats(data, add: false);

            // 슬롯 해제
            if (data.equipmentType == EquipmentType.Weapon) equippedWeapon = null;
            else equippedArmor = null;

            OnUnequip?.Invoke(data);
            Debug.Log($"[Equipment] {data.itemName} 해제");
        }

        // 스탯 적용 / 제거 (add=true: 장착, false: 해제)
        private void ApplyStats(EquipmentData data, bool add)
        {
            if (playerStats == null) return;

            int sign = add ? 1 : -1;
            playerStats.maxHP += data.bonusHP * sign;
            playerStats.attackDamage += data.bonusAttack * sign;
            playerStats.moveSpeed += data.bonusSpeed * sign;
            playerStats.fireForce += data.bonusFireForce * sign;

            // HP 보너스 적용 시 현재 HP도 조정
            if (data.bonusHP != 0)
                player.CurrentHP = Mathf.Min(player.CurrentHP + data.bonusHP * sign, playerStats.maxHP);
        }

        public EquipmentData GetEquippedWeapon() => equippedWeapon;
        public EquipmentData GetEquippedArmor() => equippedArmor;
    }
}
