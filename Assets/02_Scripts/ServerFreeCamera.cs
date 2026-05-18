using UnityEngine;
using UnityEngine.InputSystem;

namespace LittleSword
{
    /// <summary>
    /// 서버 전용 자유 이동 카메라.
    /// WASD / 방향키 : 수평 이동
    /// Q / E         : 위 / 아래
    /// 마우스 휠     : 줌 (Z축 이동)
    /// Shift 누르면  : 이동 속도 3배
    /// </summary>
    public class ServerFreeCamera : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float fastMultiplier = 3f;

        [Header("줌")]
        [SerializeField] private float scrollSpeed = 5f;
        [SerializeField] private float minZ = -30f;
        [SerializeField] private float maxZ = -3f;

        [Header("UI 안내 텍스트")]
        [SerializeField] private bool showGuide = true;

        private GUIStyle _guideStyle;

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            // 속도 배율
            float speed = moveSpeed * (kb.leftShiftKey.isPressed ? fastMultiplier : 1f);

            // 수평 이동 (WASD + 방향키)
            float h = 0f, v = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;

            // 수직 이동 (Q / E)
            float up = 0f;
            if (kb.qKey.isPressed) up -= 1f;
            if (kb.eKey.isPressed) up += 1f;

            transform.Translate(
                new Vector3(h, v + up, 0f) * speed * Time.deltaTime,
                Space.World
            );

            // 마우스 휠 줌
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float newZ = Mathf.Clamp(
                        transform.position.z + scroll * scrollSpeed * Time.deltaTime,
                        minZ, maxZ
                    );
                    transform.position = new Vector3(transform.position.x, transform.position.y, newZ);
                }
            }
        }

        private void OnGUI()
        {
            if (!showGuide) return;

            if (_guideStyle == null)
            {
                _guideStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 13,
                    alignment = TextAnchor.UpperLeft
                };
                _guideStyle.normal.textColor = Color.white;
            }

            GUI.Box(new Rect(10, 10, 230, 90),
                "[서버 카메라]\n" +
                "WASD / 방향키 : 이동\n" +
                "Q / E         : 위 / 아래\n" +
                "마우스 휠     : 줌\n" +
                "Shift         : 빠른 이동",
                _guideStyle);
        }
    }
}