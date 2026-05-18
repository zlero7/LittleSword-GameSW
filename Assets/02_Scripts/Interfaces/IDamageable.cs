namespace LittleSword.Interfaces
{
    // 생명(체력) 관련 동작을 표준화하는 인터페이스
    // - TakeDamage 로 피해를 받고, 필요 시 Die를 호출하여 사망 처리를 수행
    public interface IDamageable
    {
        // 객체가 사망 상태인지 여부를 반환 ( 읽기 전용 )
        bool IsDead { get; }

        // 현재 HP를 반환함 ( 읽기 전용 ). HP증감 관리는 구현체에서 수행
        int CurrentHP { get; }

        // 지정된 피해량만큼 체력을 감소
        // 구현체는 피해 적용 후 CurrentHP 가 0 이하일 경우 Die() 를 호출하도록 권장
        void TakeDamage(int damage);

        // 사망 처리 로직을 수행함 ( 애니메이션, 이펙트, 상태 전환 등 )
        // 중복 호출에 대해 방어적 처리 ( 예 : IsDead 체크 ) 를 구현하는 것이 안전
        void Die();
    }
}