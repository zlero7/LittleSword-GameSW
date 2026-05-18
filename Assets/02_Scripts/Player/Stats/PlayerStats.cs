using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStatsS0", menuName = "LittleSword/PlayerStats", order = 0)]
public class PlayerStats : ScriptableObject
{
    [Header("기본 스탯")]
    public int maxHP = 100;
    public float moveSpeed = 5f;
    public int attackDamage = 20;
    public float fireForce = 10f;

    [Header("대시 설정")]
    public float dashForce = 12f;           // 대시 힘
    public float dashDuration = 0.15f;      // 대시 지속 시간 (초)
    public float dashCooldown = 0.8f;       // 대시 쿨다운 (초)
    public float dashInvincibleDuration = 0.2f; // 무적 프레임 지속 시간

    [Header("워리어 콤보 설정")]
    public float comboResetTime = 0.6f;     // 이 시간 안에 다시 공격하면 콤보 이어짐
    public float combo2DamageMultiplier = 1.5f; // 2타 데미지 배율

    [Header("아처 차징 샷 설정")]
    public float maxChargeTime = 1.5f;      // 최대 차징 시간
    public float maxChargeDamageMultiplier = 2.5f; // 최대 차징 시 데미지 배율
    public float maxChargeForceMultiplier = 2.0f;  // 최대 차징 시 속도 배율
}
