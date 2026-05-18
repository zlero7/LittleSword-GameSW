using UnityEngine;

[CreateAssetMenu(fileName = "ArcherStatsS0", menuName = "LittleSword/ArcherStats", order = 1)]
public class ArcherStats : PlayerStats
{
    // PlayerStats를 상속하므로 아처 전용 추가 스탯이 필요하면 여기에 작성
    // 현재는 PlayerStats의 차징 샷 설정을 공유 사용
}
