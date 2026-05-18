using UnityEngine;
using Unity.Netcode;
using LittleSword.Enemy.FSM;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;
using LittleSword.Enemy.Stats;
using System.Linq;
using LittleSword.Interfaces;
using LittleSword.Player;

namespace LittleSword.Enemy
{
    public class Enemy : NetworkBehaviour, IDamageable
    {
        private StateMachine stateMachine;
        public StateMachine StateMachine => stateMachine;
        public string CurrentStateName => stateMachine?.currentState?.GetType().Name ?? "None";

        private Dictionary<Type, IState> states;

        [NonSerialized] public Rigidbody2D rigidbody;
        [NonSerialized] public SpriteRenderer spriteRenderer;
        [NonSerialized] public Animator animator;

        public static readonly int hashIsRun = Animator.StringToHash("IsRun");
        public static readonly int hashAttack = Animator.StringToHash("Attack");
        public static readonly int hashDie = Animator.StringToHash("Die");
        public static readonly int hsshHit = Animator.StringToHash("Hit");

        [SerializeField] public EnemyStats enemyStats;

        [SerializeField] private Transform target;
        public LayerMask playerLayer;

        public Transform Target => target;
        public bool IsDead => CurrentHP <= 0;

        // HP - 서버 권한, 모두 읽기 가능
        private NetworkVariable<int> networkHP = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        public int CurrentHP => networkHP.Value;

        #region 상태 관련 메서드

        public void ChangeState<T>() where T : IState
        {
            if (IsDead && typeof(T) != typeof(DieState)) return;
            if (states.TryGetValue(typeof(T), out IState newState))
                stateMachine.ChangeState(newState);
        }

        public bool DetectPlayer()
        {
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, enemyStats.chaseDistance, playerLayer);

            if (colliders.Length > 0)
            {
                var filtered = colliders
                    .OrderBy(c => (transform.position - c.transform.position).sqrMagnitude)
                    .Where(c => c.GetComponent<BasePlayer>()?.IsDead == false)
                    .FirstOrDefault();

                if (filtered == null)
                {
                    target = null;
                    return false;
                }

                target = filtered.transform;
                return true;
            }

            target = null;
            return false;
        }

        public void MoveToPlayer()
        {
            if (target == null) return;
            SetFacing();
            Vector2 direction = (target.position - transform.position).normalized;
            rigidbody.linearVelocity = direction * enemyStats.moveSPeed;
        }

        public void SetFacing()
        {
            if (spriteRenderer == null) return;
            Vector2 dir = target.position - transform.position;
            spriteRenderer.flipX = dir.x < 0;
        }

        public void StopMoving()
        {
            rigidbody.linearVelocity = Vector2.zero;
        }

        public bool IsInAttackRange()
        {
            if (target == null) return false;
            float targetDistance = (transform.position - target.position).sqrMagnitude;
            return targetDistance <= enemyStats.attackDistance * enemyStats.attackDistance;
        }

        #endregion

        #region 유니티 이벤트

        private void Awake()
        {
            InitComponents();
            InisState();
            stateMachine = new StateMachine(this);
        }

        public override void OnNetworkSpawn()
        {
            // ✅ [버그2 수정] 중괄호 명시 — ChangeState가 if 블록 밖으로 빠지던 버그 수정
            if (IsServer)
            {
                networkHP.Value = enemyStats.maxHP;
                ChangeState<IdleState>();
            }

            // ✅ [버그2 수정] 클라이언트는 Update(FSM)만 끄고, Animator는 활성 유지
            // 서버가 ClientRpc로 애니메이션 명령을 보내므로 클라이언트 Animator는 켜둬야 함
            if (!IsServer)
                enabled = false;
        }

        private void Update()
        {
            if (!IsServer) return;
            stateMachine.Update();
        }

        #endregion

        #region 초기화

        private void InisState()
        {
            states = new Dictionary<Type, IState>
            {
                [typeof(IdleState)] = new IdleState(this, enemyStats.delecInterval),
                [typeof(ChaseState)] = new ChaseState(this, enemyStats.delecInterval),
                [typeof(AttackState)] = new AttackState(this, enemyStats.attackCooldown),
                [typeof(DieState)] = new DieState(this)
            };
        }

        private void InitComponents()
        {
            rigidbody = GetComponent<Rigidbody2D>();
            rigidbody.gravityScale = 0;
            rigidbody.freezeRotation = true;
            rigidbody.mass = 10000f;
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (enemyStats == null) return;
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, enemyStats.chaseDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, enemyStats.attackDistance);
        }

        #endregion

        #region 애니메이션 동기화 (ClientRpc)

        // ✅ [버그2 수정] 서버에서 상태 전환 시 모든 클라이언트 Animator에 명령 전달

        /// <summary>Idle 진입: IsRun = false</summary>
        [ClientRpc]
        public void SyncIdleAnimClientRpc()
        {
            animator.SetBool(hashIsRun, false);
        }

        /// <summary>Chase 진입: IsRun = true</summary>
        [ClientRpc]
        public void SyncChaseAnimClientRpc()
        {
            animator.SetBool(hashIsRun, true);
        }

        /// <summary>Attack 트리거</summary>
        [ClientRpc]
        public void SyncAttackAnimClientRpc()
        {
            animator.SetBool(hashIsRun, false);
            animator.SetTrigger(hashAttack);
        }

        #endregion

        #region 애니메이션 이벤트

        public void OnAttackAnimationEvent()
        {
            if (!IsServer) return;
            if (target == null) return;
            target.GetComponent<IDamageable>()?.TakeDamage(enemyStats.attackDamage);
        }

        public void OnAttackHit()
        {
            if (!IsServer) return;
            if (Target == null) return;
            Target.GetComponent<IDamageable>()?.TakeDamage(enemyStats.attackDamage);
        }

        // 피격 - 서버에서만 HP 계산
        public void TakeDamage(int damage)
        {
            if (!IsServer) return;
            if (IsDead) return;

            networkHP.Value -= damage;

            if (IsDead)
                Die();
            else
                HitClientRpc();
        }

        // 모든 클라이언트에서 피격 애니메이션 재생
        [ClientRpc]
        private void HitClientRpc()
        {
            animator.SetTrigger(hsshHit);
        }

        // 사망 - 서버에서만 호출
        public void Die()
        {
            if (!IsServer) return;
            ChangeState<DieState>();
        }

        #endregion
    }
}