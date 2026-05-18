namespace LittleSword.Enemy.FSM
{
    public class StateMachine
    {
        // 상태 머신이 동작할 컨텍스트 ( Enemy 인스턴스 )
        private Enemy enemy;

        public StateMachine(Enemy enemy)
        {
            this.enemy = enemy;
        }

        // 현재 활성화된 상태를 가리키는 변수 ( null일수도 있음 )
        public IState currentState { get; private set; }

        public void ChangeState(IState newState)
        {
            currentState?.Exit();   // ← enemy 제거
            currentState = newState;
            currentState.Enter();   // ← enemy 제거
        }

        public void Update()
        {
            currentState?.Update(); // ← enemy 제거
        }
    }
}