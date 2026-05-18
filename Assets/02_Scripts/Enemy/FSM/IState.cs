using LittleSword.Common;

namespace LittleSword.Enemy.FSM
{
    // IState.cs
    public interface IState
    {
        void Enter();
        void Update();
        void Exit();
    }
}
