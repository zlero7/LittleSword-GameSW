using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace LittleSword.Common
{
    public static class Logger
    {
        [Conditional("DEVELOP_MODE")]
        [Conditional("UNITY_EDITOR")]
        public static void Log(object message)
        {
            // Unity의 Debug.Log 호출. DEVELOP_MOLDE/UNITY_EDITOR 가 없으면 이 호출은 빌드에 포함되지 않습니다.
            Debug.Log(message);
        }

        [Conditional("DEVELOP_MODE")]
        [Conditional("UNITY_EDITOR")]
        public static void LogError(string message)
        {
            // 심각한 문제를 출력할 때 사용
            Debug.LogError(message);
        }

        [Conditional("DEVELOP_MODE")]
        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(string message)
        {
            // 주의가 필요한 상태를 출력할 때 사용
            Debug.LogWarning(message);
        }
    }
}