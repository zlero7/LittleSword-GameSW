using System;
using UnityEngine;

namespace LittleSword.InputSystem
{
    // 플레이어 입력 이벤트 인터페이스
    // 기존: OnMove, OnAttack
    // 추가: OnDash (대시/구르기)
    public interface IInputEvents
    {
        event Action<Vector2> OnMove;
        event Action OnAttack;
        event Action OnDash;        // 대시 입력 이벤트 추가
        event Action OnAttackHold;  // 공격 버튼 누르는 중 (아처 차징용)
        event Action OnAttackRelease; // 공격 버튼 뗌 (아처 차징 발사)
    }
}
