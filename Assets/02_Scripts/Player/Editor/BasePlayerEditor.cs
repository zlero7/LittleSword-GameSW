using UnityEngine;
using UnityEditor;
using LittleSword.Player;

[CustomEditor(typeof(Warrior))]
public class BasePlayerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BasePlayer basePlayer = (BasePlayer)target;

        // 기본 인스펙터 그리기
        DrawDefaultInspector();

        // PlayerStats 필드
        basePlayer.playerStats.maxHP = EditorGUILayout.IntField("MaxHP", basePlayer.playerStats.maxHP);

        // 현재 HP 필드
        EditorGUILayout.LabelField("Current HP", basePlayer.CurrentHP.ToString());

        // 버튼 생성
        if (GUILayout.Button("피격"))
        {
            basePlayer.TakeDamage(10); // 10의 데미지 입음
        }

        // 버튼 생성
        if (GUILayout.Button("초기화"))
        {
            // 현재 HP를 최대 HP로 초기화
            basePlayer.CurrentHP = basePlayer.playerStats.maxHP;
        }
    }
}