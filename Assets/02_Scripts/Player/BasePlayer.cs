using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using LittleSword.InputSystem;
using LittleSword.Player.Controller;
using LittleSword.Interfaces;
using LittleSword.UI;
using Unity.Cinemachine;

namespace LittleSword.Player
{
    [RequireComponent(typeof(InputHandler))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    public class BasePlayer : NetworkBehaviour, IDamageable
    {
        private InputHandler inputHandler;
        protected MovementController movementController;
        private AnimationController animationController;

        protected Rigidbody2D rigidBody;
        protected SpriteRenderer spriteRenderer;
        protected Animator animator;
        protected Collider2D col;
        private CinemachineCamera cmCamera;

        public PlayerStats playerStats;

        // ✅ public으로 변경 → GameHUD에서 직접 구독 가능
        //    playerStats.maxHP 타이밍 문제 없이 NetworkVariable 값을 바로 읽을 수 있음
        public NetworkVariable<int> networkHP = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        public NetworkVariable<int> networkMaxHP = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        private NetworkVariable<int> networkAttack = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        private NetworkVariable<float> networkMoveSpeed = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        private NetworkVariable<bool> networkFlipX = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        public bool IsDead => networkHP.Value <= 0;

        public int CurrentHP
        {
            get => networkHP.Value;
            set { if (IsServer) networkHP.Value = value; }
        }

        // ✅ GameHUD에서 playerStats.maxHP 대신 이 값을 사용 → 동기화 타이밍 문제 없음
        public int NetworkMaxHP => networkMaxHP.Value;

        private bool isDashing = false;
        private bool isInvincible = false;
        private float dashCooldownTimer = 0f;
        protected Vector2 lastMoveDirection = Vector2.right;

        #region 1. 생명주기

        protected void Awake()
        {
            InitComponents();
        }

        public override void OnNetworkSpawn()
        {
            InitControllers();

            if (IsServer)
            {
                networkMaxHP.Value = playerStats.maxHP;
                networkAttack.Value = playerStats.attackDamage;
                networkMoveSpeed.Value = playerStats.moveSpeed;
                networkHP.Value = playerStats.maxHP;
            }
            else
            {
                // 클라이언트: OnNetworkSpawn 시점에 이미 동기화된 경우 반영
                if (networkMaxHP.Value > 0) playerStats.maxHP = networkMaxHP.Value;
                if (networkAttack.Value > 0) playerStats.attackDamage = networkAttack.Value;
                if (networkMoveSpeed.Value > 0f) playerStats.moveSpeed = networkMoveSpeed.Value;
            }

            networkMaxHP.OnValueChanged += OnMaxHPChanged;
            networkAttack.OnValueChanged += OnAttackChanged;
            networkMoveSpeed.OnValueChanged += OnMoveSpeedChanged;
            networkFlipX.OnValueChanged += OnFlipXChanged;

            if (spriteRenderer != null)
                spriteRenderer.flipX = networkFlipX.Value;

            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null && !IsOwner)
                cam.gameObject.SetActive(false);

            if (IsOwner)
            {
                if (cmCamera != null) cmCamera.Follow = transform; // 로컬 플레이어의 카메라가 자신을 따라가도록 설정합니다.
                inputHandler.enabled = true;
                inputHandler.OnMove += Move;
                inputHandler.OnAttack += Attack;
                inputHandler.OnDash += Dash;
            }
            else
            {
                if (cmCamera != null) cmCamera.gameObject.SetActive(false);
                inputHandler.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            networkMaxHP.OnValueChanged -= OnMaxHPChanged;
            networkAttack.OnValueChanged -= OnAttackChanged;
            networkMoveSpeed.OnValueChanged -= OnMoveSpeedChanged;
            networkFlipX.OnValueChanged -= OnFlipXChanged;

            if (IsOwner)
            {
                inputHandler.OnMove -= Move;
                inputHandler.OnAttack -= Attack;
                inputHandler.OnDash -= Dash;
            }
        }

        protected void Update()
        {
            if (!IsOwner) return;
            if (dashCooldownTimer > 0f)
                dashCooldownTimer -= Time.deltaTime;
        }

        #endregion

        #region 2. 초기화

        private void InitControllers()
        {
            inputHandler = GetComponent<InputHandler>();
            movementController = new MovementController(rigidBody, spriteRenderer);
            animationController = GetComponent<AnimationController>();
        }

        private void InitComponents()
        {
            rigidBody = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            col = GetComponent<Collider2D>();
            rigidBody.gravityScale = 0;
            rigidBody.freezeRotation = true;
            playerStats = Instantiate(playerStats);
            cmCamera = GetComponentInChildren<CinemachineCamera>(true); // 자식에서 CinemachineCamera를 찾아 카메라 제어에 사용하기 위해 캐시
        }

        #endregion

        #region 3. 이동 / 공격 / 대시

        protected virtual void Move(Vector2 direction)
        {
            if (!IsOwner) return;
            if (isDashing) return;
            movementController.Move(direction, playerStats.moveSpeed);
            animationController.Move(direction != Vector2.zero);
            if (direction != Vector2.zero)
            {
                lastMoveDirection = direction.normalized;
                bool shouldFlip = direction.x < 0;
                if (networkFlipX.Value != shouldFlip)
                    networkFlipX.Value = shouldFlip;
            }
        }

        protected virtual void Attack()
        {
            if (!IsOwner) return;
            animationController.Attack();
        }

        private void Dash()
        {
            if (!IsOwner) return;
            if (IsDead || isDashing || dashCooldownTimer > 0f) return;
            StartCoroutine(DashCoroutine());
        }

        private System.Collections.IEnumerator DashCoroutine()
        {
            isDashing = true;
            dashCooldownTimer = playerStats.dashCooldown;
            StartCoroutine(InvincibleCoroutine());
            rigidBody.linearVelocity = lastMoveDirection * playerStats.dashForce;
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.5f);
            yield return new WaitForSeconds(playerStats.dashDuration);
            rigidBody.linearVelocity = Vector2.zero;
            spriteRenderer.color = Color.white;
            isDashing = false;
        }

        private System.Collections.IEnumerator InvincibleCoroutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(playerStats.dashInvincibleDuration);
            isInvincible = false;
        }

        #endregion

        #region 4. 피격 / 사망 / 부활

        public void TakeDamage(int damage)
        {
            if (!IsServer) return;
            if (IsDead || isInvincible) return;

            networkHP.Value = Mathf.Max(0, networkHP.Value - damage);

            if (networkHP.Value <= 0)
                DieClientRpc();
            else
                HitClientRpc();
        }

        [ClientRpc]
        private void HitClientRpc()
        {
            animationController.Hit();
        }

        [ClientRpc]
        private void DieClientRpc()
        {
            animationController.Die();
            if (inputHandler != null) inputHandler.enabled = false;
            if (col != null) col.enabled = false;
            rigidBody.linearVelocity = Vector2.zero;

            if (IsOwner)
            {
                var coopManager = FindFirstObjectByType<CoopManager>();
                if (coopManager != null)
                    coopManager.StartSpectating();
            }
        }

        public void Die()
        {
            if (!IsServer) return;
            DieClientRpc();
        }

        public void Revive(Vector3 position)
        {
            if (!IsServer) return;
            networkHP.Value = playerStats.maxHP;
            ReviveClientRpc(position);
        }

        [ClientRpc]
        private void ReviveClientRpc(Vector3 position)
        {
            transform.position = position;
            animationController.Revive();
            if (col != null) col.enabled = true;
            if (IsOwner && inputHandler != null)
                inputHandler.enabled = true;

            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null && IsOwner)
                cam.gameObject.SetActive(true);
        }

        // ✅ 서버에서 직접 호출 (LevelSystem이 IsServer 보장 후 호출)
        public void ApplyLevelUpStats(int hpBonus, int attackBonus, float speedBonus)
        {
            if (!IsServer) return;
            networkMaxHP.Value += hpBonus;
            networkAttack.Value += attackBonus;
            networkMoveSpeed.Value += speedBonus;
            networkHP.Value = networkMaxHP.Value;
        }

        // 하위 호환용 ServerRpc (Owner가 외부에서 직접 요청할 경우)
        [ServerRpc(RequireOwnership = true)]
        public void RequestLevelUpServerRpc(int hpBonus, int attackBonus, float speedBonus)
        {
            ApplyLevelUpStats(hpBonus, attackBonus, speedBonus);
        }

        #endregion

        #region 5. 헬퍼

        private void OnMaxHPChanged(int prev, int next) => playerStats.maxHP = next;
        private void OnAttackChanged(int prev, int next) => playerStats.attackDamage = next;
        private void OnMoveSpeedChanged(float prev, float next) => playerStats.moveSpeed = next;
        private void OnFlipXChanged(bool prev, bool next)
        {
            if (spriteRenderer != null)
                spriteRenderer.flipX = next;
        }

        #endregion
    }
}