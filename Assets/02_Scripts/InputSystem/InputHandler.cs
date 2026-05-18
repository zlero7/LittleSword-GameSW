using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = LittleSword.Common.Logger;

namespace LittleSword.InputSystem
{
    public class InputHandler : MonoBehaviour, IInputEvents
    {
        public event Action<Vector2> OnMove;
        public event Action OnAttack;
        public event Action OnDash;
        public event Action OnAttackHold;
        public event Action OnAttackRelease;

        private InputSystem_Actions inputActions;
        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction dashAction;

        private void Awake()
        {
            inputActions = new InputSystem_Actions();
            moveAction = inputActions.Player.Move;
            attackAction = inputActions.Player.Attack;
            dashAction = inputActions.Player.Dash;
        }

        private void OnEnable()
        {
            inputActions.Enable();

            moveAction.performed += HandleMove;
            moveAction.canceled += HandleMove;

            attackAction.started += HandleAttackHold;
            attackAction.performed += HandleAttack;
            attackAction.canceled += HandleAttackRelease;

            dashAction.performed += HandleDash;
        }

        private void OnDisable()
        {
            inputActions.Disable();

            moveAction.performed -= HandleMove;
            moveAction.canceled -= HandleMove;

            attackAction.started -= HandleAttackHold;
            attackAction.performed -= HandleAttack;
            attackAction.canceled -= HandleAttackRelease;

            dashAction.performed -= HandleDash;
        }

        private void HandleMove(InputAction.CallbackContext ctx)
        {
            OnMove?.Invoke(ctx.ReadValue<Vector2>());
        }

        private void HandleAttack(InputAction.CallbackContext ctx)
        {
            OnAttack?.Invoke();
        }

        private void HandleAttackHold(InputAction.CallbackContext ctx)
        {
            OnAttackHold?.Invoke();
        }

        private void HandleAttackRelease(InputAction.CallbackContext ctx)
        {
            OnAttackRelease?.Invoke();
        }

        private void HandleDash(InputAction.CallbackContext ctx)
        {
            OnDash?.Invoke();
        }
    }
}