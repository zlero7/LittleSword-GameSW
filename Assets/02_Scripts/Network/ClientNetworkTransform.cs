using Unity.Netcode.Components;

namespace LittleSword.Network
{
    // Owner-authoritative NetworkTransform:
    // 소유자(Owner)가 자신의 위치를 직접 서버로 전송 → 모든 클라이언트에게 동기화됨
    // 기본 NetworkTransform(서버 권한)을 사용하면 클라이언트 Rigidbody 이동이 서버에 반영되지 않아
    // 다른 플레이어에게 캐릭터가 스폰 위치에 고정되어 보이는 문제가 발생한다.
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
