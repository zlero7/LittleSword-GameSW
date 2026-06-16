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
        private NetworkVariable<bool> networkIsRun = new NetworkVariable<bool>(
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
            Debug.Log($"[BasePlayer] OnNetworkSpawn {name} OwnerClientId={OwnerClientId} IsOwner={IsOwner} instanceId={GetInstanceID()}");
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
            networkIsRun.OnValueChanged += OnIsRunChanged;

            if (spriteRenderer != null)
                spriteRenderer.flipX = networkFlipX.Value;

            if (IsOwner)
            {
                // 오너만 카메라를 활성화. CinemachineBrain은 비활성화해 멀티플레이어 Brain 간섭 완전 차단.
                // 카메라가 플레이어 자식이므로 Brain 없이도 플레이어를 따라온다.
                var cam = GetComponentInChildren<Camera>(true);
                if (cam != null)
                {
                    cam.gameObject.SetActive(true);
                    var brain = cam.GetComponent<CinemachineBrain>();
                    if (brain != null) brain.enabled = false;
                }
                // VCam(CinemachineCamera)은 Brain이 꺼져 있으므로 활성화하지 않는다.

                inputHandler.enabled = true;
                inputHandler.OnMove += Move;
                inputHandler.OnAttack += Attack;
                inputHandler.OnDash += Dash;
            }
            else
            {
                inputHandler.enabled = false;
                // Dynamic Rigidbody2D가 NetworkTransform의 위치 업데이트를 방해하지 않도록 Kinematic으로 전환.
                // 오너가 아닌 인스턴스는 물리 시뮬레이션이 아닌 NetworkTransform이 위치를 담당한다.
                rigidBody.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        public override void OnNetworkDespawn()
        {
            networkMaxHP.OnValueChanged -= OnMaxHPChanged;
            networkAttack.OnValueChanged -= OnAttackChanged;
            networkMoveSpeed.OnValueChanged -= OnMoveSpeedChanged;
            networkFlipX.OnValueChanged -= OnFlipXChanged;
            networkIsRun.OnValueChanged -= OnIsRunChanged;

            if (IsOwner)
            {
                inputHandler.OnMove -= Move;
                inputHandler.OnAttack -= Attack;
                inputHandler.OnDash -= Dash;
            }
        }

        private float _diagTimer = 3f;

        protected void Update()
        {
            // 진단: 스폰 후 3초간 모든 플레이어 상태를 로컬 콘솔에 출력 (클라이언트 Player.log 확인용)
            if (_diagTimer > 0f)
            {
                _diagTimer -= Time.deltaTime;
                if (_diagTimer < 2.9f && _diagTimer > 2.8f)
                {
                    var active = FindObjectsByType<BasePlayer>(FindObjectsSortMode.None);
                    var all = FindObjectsByType<BasePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    Debug.Log($"[PlayerDiag] 이 머신: IsHost={NetworkManager.Singleton?.IsHost} IsServer={NetworkManager.Singleton?.IsServer} LocalClientId={NetworkManager.Singleton?.LocalClientId}");
                    Debug.Log($"[PlayerDiag] 씬 내 BasePlayer 수: 활성={active.Length} / 전체(비활성포함)={all.Length}");
                    foreach (var p in all)
                    {
                        var no = p.GetComponent<Unity.Netcode.NetworkObject>();
                        var sr = p.GetComponent<SpriteRenderer>();
                        var cam = p.GetComponentInChildren<Camera>(true);
                        Debug.Log($"[PlayerDiag]  - {p.name} | OwnerClientId={no?.OwnerClientId} | IsOwner={no?.IsOwner} | pos={p.transform.position} | spriteEnabled={sr?.enabled} | objActive={p.gameObject.activeInHierarchy} | camActive={cam?.gameObject.activeInHierarchy} | instanceId={p.GetInstanceID()}");
                    }
                }
            }

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
            cmCamera = GetComponentInChildren<CinemachineCamera>(true);

            // 모든 카메라를 즉시 비활성화. 오너 여부는 OnNetworkSpawn에서만 알 수 있으므로
            // 그 전까지 활성 상태인 채로 두면 Cinemachine Brain이 여러 VCam을 동시에 인식해
            // 다른 플레이어의 카메라를 따라가는 간섭이 발생한다.
            var camChild = GetComponentInChildren<Camera>(true);
            if (camChild != null) camChild.gameObject.SetActive(false);
            if (cmCamera != null) cmCamera.gameObject.SetActive(false);
        }

        #endregion

        #region 3. 이동 / 공격 / 대시

        protected virtual void Move(Vector2 direction)
        {
            if (!IsOwner) return;
            if (isDashing) return;
            movementController.Move(direction, playerStats.moveSpeed);
            bool isMoving = direction != Vector2.zero;
            animationController.Move(isMoving);
            if (networkIsRun.Value != isMoving)
                networkIsRun.Value = isMoving;
            if (isMoving)
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
        private void OnIsRunChanged(bool prev, bool next)
        {
            if (!IsOwner) animationController?.Move(next);
        }

        #endregion
    }
}