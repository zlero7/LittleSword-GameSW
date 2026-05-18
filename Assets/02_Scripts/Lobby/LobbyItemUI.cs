using System;
using System.IO;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using LittleSword.Network;

namespace LittleSword.UI
{
    /// <summary>
    /// 방 목록의 각 항목(Row) 프리팹에 붙이는 컴포넌트.
    ///
    /// 프리팹 구조 예시:
    ///  LobbyItem (RectTransform, Image 배경)
    ///    ├─ RoomNameText   (TextMeshProUGUI)  방 이름
    ///    ├─ PlayerCountText (TextMeshProUGUI) "2 / 4"
    ///    └─ JoinButton     (Button)
    /// </summary>
    public class LobbyItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button          joinButton;

        private Lobby                _lobby;
        private Action<Lobby>        _onJoin;

        /// <summary>LobbyListUI에서 호출해 데이터와 콜백을 주입합니다.</summary>
        public void Setup(Lobby lobby, Action<Lobby> onJoin)
        {
            _lobby  = lobby;
            _onJoin = onJoin;

            if (roomNameText)
                roomNameText.text = lobby.Name;

            int playerCount = GetPlayerCount(lobby);

            if (playerCountText)
                playerCountText.text = $"{playerCount} / {lobby.MaxPlayers}";

            Debug.Log($"[LobbyItemUI] Setup - {lobby.Name}: {playerCount}/{lobby.MaxPlayers}");

            bool isFull = playerCount >= lobby.MaxPlayers;
            if (joinButton)
            {
                joinButton.interactable = !isFull;
                joinButton.onClick.AddListener(OnJoinClicked);
            }
        }

        private int GetPlayerCount(Lobby lobby)
        {
            var logPath = Application.persistentDataPath + "/debug.log";
            var players = lobby.Players;
            string log = $"[GetPlayerCount] {lobby.Name}: Players.Count={players?.Count ?? 0}\n";
            
            if (lobby.Data != null && lobby.Data.TryGetValue("PlayerCount", out var pc))
            {
                if (int.TryParse(pc.Value, out int count))
                {
                    File.AppendAllText(logPath, log);
                    return count;
                }
            }
            
            File.AppendAllText(logPath, log);
            return players?.Count ?? 0;
        }

        private void OnJoinClicked()
        {
            _onJoin?.Invoke(_lobby);
        }
    }
}
