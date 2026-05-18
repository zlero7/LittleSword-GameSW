using UnityEngine;

namespace LittleSword.Player.Controller
{
    public class AnimationController : MonoBehaviour
    {
        private Animator animator;

        private static readonly int hashIsRun = Animator.StringToHash("IsRun");
        private static readonly int hashAttack = Animator.StringToHash("Attack");
        private static readonly int hashDie = Animator.StringToHash("Die");
        private static readonly int hashHit = Animator.StringToHash("Hit");

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void Move(bool isMoving)
        {
            animator.SetBool(hashIsRun, isMoving);
        }

        public void Attack()
        {
            animator.SetTrigger(hashAttack);
        }

        public void Die()
        {
            animator.SetTrigger(hashDie);
        }

        public void Hit()
        {
            animator.SetTrigger(hashHit);
        }

        public void Revive()
        {
            animator.ResetTrigger(hashDie);
            animator.ResetTrigger(hashHit);
            animator.SetBool(hashIsRun, false);
            animator.Play("Idle");
        }
    }
}
