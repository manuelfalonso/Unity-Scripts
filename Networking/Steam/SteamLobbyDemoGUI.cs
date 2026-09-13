using SombraStudios.Shared.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;

namespace SombraStudios.Shared.Networking.Steam
{
    /// <summary>
    /// A runtime IMGUI panel for driving and inspecting a <see cref="SteamMultiplayerBootstrap"/>
    /// without building any UI.
    /// </summary>
    /// <remarks>
    /// Deliberately IMGUI: it needs no Canvas, no prefab, no TextMeshPro and no scene setup, so it
    /// drops into a fresh project and works. Replace it with real UI once the flow is proven.
    /// </remarks>
    [RequireComponent(typeof(SteamMultiplayerBootstrap))]
    public class SteamLobbyDemoGUI : MonoBehaviour
    {
        [Tooltip("Where the panel is drawn, in screen pixels.")]
        [SerializeField] private Rect _area = new Rect(10f, 10f, 320f, 300f);

        private SteamMultiplayerBootstrap _bootstrap;
        private string _lobbyIdInput = string.Empty;
        private string _lastMessage = string.Empty;
        private Vector2 _memberScroll;

        private void Awake()
        {
            _bootstrap = GetComponent<SteamMultiplayerBootstrap>();
        }

        private void OnEnable()
        {
            _bootstrap.Failed += HandleFailed;
        }

        private void OnDisable()
        {
            _bootstrap.Failed -= HandleFailed;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(_area, GUI.skin.box);

            GUILayout.Label($"State: {_bootstrap.State}");
            GUILayout.Label($"Steam: {(_bootstrap.IsSteamAvailable ? "connected" : "unavailable")}");

            DrawLobbyId();

            DrawNetcodeStatus();
            DrawActions();
            DrawMembers();

            if (!string.IsNullOrEmpty(_lastMessage))
                GUILayout.Label(_lastMessage);

            GUILayout.EndArea();
        }

        private void DrawActions()
        {
            var isOffline = _bootstrap.State == SessionState.Offline;

            GUI.enabled = isOffline;
            if (GUILayout.Button("Host"))
            {
                _lastMessage = string.Empty;
                _bootstrap.Host();
            }

            GUILayout.BeginHorizontal();
            _lobbyIdInput = GUILayout.TextField(_lobbyIdInput);
            if (GUILayout.Button("Join", GUILayout.Width(60f)) && ulong.TryParse(_lobbyIdInput, out var parsed))
            {
                _lastMessage = string.Empty;
                _bootstrap.Join(parsed);
            }

            GUILayout.EndHorizontal();

            GUI.enabled = !isOffline;
            if (GUILayout.Button("Invite a friend") && !_bootstrap.Invite())
                _lastMessage = "No lobby to invite to.";

            if (GUILayout.Button("Leave"))
            {
                _lastMessage = string.Empty;
                _bootstrap.Leave();
            }

            GUI.enabled = true;
        }

        private void DrawLobbyId()
        {
            var lobbyId = _bootstrap.Controller?.CurrentLobbyId ?? 0UL;
            if (lobbyId == 0)
                return;

            GUILayout.Label("Lobby ID (share it to let someone join):");

            GUILayout.BeginHorizontal();

            // Shown disabled because editing it does nothing — it reports the lobby we are in,
            // it does not choose one. A real Steam ID is 17 digits, hence the Copy button.
            GUI.enabled = false;
            GUILayout.TextField(lobbyId.ToString());
            GUI.enabled = true;

            if (GUILayout.Button("Copy", GUILayout.Width(50f)))
                GUIUtility.systemCopyBuffer = lobbyId.ToString();

            GUILayout.EndHorizontal();

            if (!_bootstrap.IsSteamAvailable)
                GUILayout.Label("Local mode: any ID connects, this one is cosmetic.");
        }

        private void DrawNetcodeStatus()
        {
            // The lobby list and the real connection are different things, and with the local
            // backend the lobby list is fabricated. This line is the one that never lies.
            var networkManager = NetworkManager.Singleton;

            if (networkManager == null || !networkManager.IsListening)
            {
                GUILayout.Label("Netcode: not running");
                return;
            }

            if (networkManager.IsServer)
            {
                GUILayout.Label($"Netcode: {(networkManager.IsHost ? "host" : "server")}, " +
                                $"{networkManager.ConnectedClientsIds.Count} client(s) connected");
                return;
            }

            GUILayout.Label($"Netcode: client, connected={networkManager.IsConnectedClient}");
        }

        private void DrawMembers()
        {
            var members = _bootstrap.Members;
            var label = _bootstrap.IsSteamAvailable ? "Lobby members" : "Lobby members (simulated)";
            GUILayout.Label($"{label} ({members.Count}):");

            _memberScroll = GUILayout.BeginScrollView(_memberScroll);

            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var name = string.IsNullOrEmpty(member.DisplayName) ? member.SteamId.ToString() : member.DisplayName;
                GUILayout.Label($"- {name}");
            }

            GUILayout.EndScrollView();
        }

        private void HandleFailed(string message)
        {
            _lastMessage = message;
        }
    }
}
