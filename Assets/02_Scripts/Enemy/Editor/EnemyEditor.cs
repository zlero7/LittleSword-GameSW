using UnityEditor;
using LittleSword.Enemy;
using LittleSword.Enemy.FSM;
using UnityEngine;

[CustomEditor(typeof(Enemy))]
public class EnemyEditor : Editor
{
    public override void OnInspectorGUI()
    {
        Enemy enemy = (Enemy)target;

        // 기본 인스펙터 내용 그리기 ( serialized 필드 등 기본 UI 유지 )
        DrawDefaultInspector();

        EditorGUILayout.Space(10); // 섹션 간 여백 추가

        // 아래 GUI 는 플레이 모드에서만 활성화되도록 설정
        GUI.enabled = Application.isPlaying;

        // 현재 상태 이름을 표시함 ( 읽기 전용 )
        EditorGUILayout.LabelField("현재 상태", enemy.CurrentStateName);

        EditorGUILayout.BeginHorizontal(); // 버튼을 한줄에 배치
        // Idle 상태 버튼 : 클릭시 Enemy의 제네릭 상태 전환 호출

        if (GUILayout.Button("Idle 상태"))
        {
            enemy.ChangeState<IdleState>();
        }

        // Chase 상태 버튼
        if (GUILayout.Button("Chase 상태"))
        {
            enemy.ChangeState<ChaseState>();
        }

        // Attack 버튼
        if (GUILayout.Button("Attack 상태"))
        {
            enemy.ChangeState<AttackState>();
        }

        EditorGUILayout.EndHorizontal();

        // GUI 상태 복원 : 버튼 비활성화 해제
        GUI.enabled = true;
    }
}
