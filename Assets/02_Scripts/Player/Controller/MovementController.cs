using UnityEngine;

namespace LittleSword.Player.Controller
{
    public class MovementController
    {
        private readonly Rigidbody2D rigidBody;
        private readonly SpriteRenderer spriteRenderer;

        // 생성자 
        public MovementController(Rigidbody2D rigidBody, SpriteRenderer spriteRenderer)
        {
            this.rigidBody = rigidBody;
            this.spriteRenderer = spriteRenderer;
        }

        // 이동 메서드
        public void Move(Vector2 direction, float moveSpeed)
        {
            rigidBody.linearVelocity = direction * moveSpeed;

            // 스프라이트 방향 전환
            if (direction != Vector2.zero)
            {
                // 현재 오른쪽을 바라보고 잇는데, flipX가 true 면 왼쪽을 바라보는 것으로 간주함
                spriteRenderer.flipX = direction.x < 0;
            }
        }
    }

}