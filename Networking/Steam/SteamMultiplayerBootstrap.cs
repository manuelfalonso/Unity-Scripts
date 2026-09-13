using System;
using System.Collections.Generic;
using SombraStudios.Shared.Networking.Sessions;
using Unity.Netcode;
using UnityEngine;

namespace SombraStudios.Shared.Networking.Steam
{
    /// <summary>
    /// Drop-in entry point for Steam multiplayer: builds the session controller, keeps Steam
    /// callbacks pumped, and exposes Host, Join, Invite and Leave to the rest of the game.
    /// </summary>
    /// <remarks>
    /// A thin adapter over <see cref="MultiplayerSessionController"/> — it owns the POCOs and
    /// forwards Unity messages, and holds no session logic of its own.
    /// <para>
    /// Put it on the same GameObject as the <see cref="NetworkManager"/>, and assign that
    /// manager a <c>FacepunchTransport</c> for Steam play or a local transport for solo
    /// iteration with <see cref="LocalLobbyBackend"/>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class SteamMultiplayerBootstrap : MonoBehaviour
    {
        [Header("Netcode")]
        [Tooltip("The NetworkManager to drive. Falls back to the one on this GameObject, then to the singleton.")]
        [SerializeField] private NetworkManager _networkManager;

        [Header("Steam")]
        [Tooltip("Use the in-memory lobby backend instead of Steam, to iterate with one machine.")]
        [SerializeField] private bool _useLocalBackend;

        [Tooltip("Steam App ID. 480 is Valve's public test app, fine for development.")]
        [SerializeField] private uint _steamAppId = MultiplayerSessionConfig.SpacewarAppId;

        [Header("Session")]
        [Tooltip("Lobby capacity, including the host.")]
        [SerializeField, Range(2, 32)] private int _maxPlayers = 4;

        [Tooltip("Who is allowed to find and enter the lobby.")]
        [SerializeField] private LobbyVisibility _visibility = LobbyVisibility.FriendsOnly;

        [Tooltip("Identifies this game's lobbies. Essential on the shared App ID 480 — change it per project.")]
        [SerializeField] private string _gameSignature = "sombra.shared";

        [Tooltip("Bump this whenever the wire format changes, to keep incompatible builds apart.")]
        [SerializeField] private int _protocolVersion = 1;

        [Tooltip("Human-readable name written into the lobby metadata.")]
        [SerializeField] private string _lobbyName = "Lobby";

        [Header("Invites")]
        [Tooltip("Join the lobby Steam passes on the command line when the game is launched from an invite.")]
        [SerializeField] private bool _joinFromCommandLine = true;

        private MultiplayerSessionController _controller;
        private FacepunchLobbyBackend _steamBackend;
        private NetcodeSessionDriver _driver;

        /// <summary>The session controller, or null before <c>Awake</c> has run.</summary>
        public MultiplayerSessionController Controller => _controller;

        /// <summary>The current session state.</summary>
        public SessionState State => _controller?.State ?? SessionState.Offline;

        /// <summary>The current lobby members, empty when not in a lobby.</summary>
        public IReadOnlyList<LobbyMember> Members => _controller?.Members ?? Array.Empty<LobbyMember>();

        /// <summary>Whether Steam is initialised. Always false while the local backend is in use.</summary>
        public bool IsSteamAvailable => !_useLocalBackend && SteamRuntime.IsAvailable;

        /// <summary>Raised after every session state change, with the previous and the new state.</summary>
        public event Action<SessionState, SessionState> StateChanged;

        /// <summary>Raised with a human-readable reason when the session drops offline unexpectedly.</summary>
        public event Action<string> Failed;

        /// <summary>Creates a lobby and starts hosting it.</summary>
        /// <returns>False when a session is already running or starting.</returns>
        public bool Host()
        {
            return _controller != null && _controller.Host();
        }

        /// <summary>Enters a lobby and connects to its host.</summary>
        /// <param name="lobbyId">The lobby to enter.</param>
        /// <returns>False when a session is already running or starting.</returns>
        public bool Join(ulong lobbyId)
        {
            return _controller != null && _controller.Join(lobbyId);
        }

        /// <summary>Opens the Steam invite overlay for the current lobby.</summary>
        /// <returns>False when there is no lobby, or when the local backend is in use.</returns>
        public bool Invite()
        {
            return _controller != null && _controller.Invite();
        }

        /// <summary>Shuts the session down and leaves the lobby.</summary>
        public void Leave()
        {
            _controller?.Leave();
        }

        private void Awake()
        {
            if (_networkManager == null && !TryGetComponent(out _networkManager))
                _networkManager = NetworkManager.Singleton;

            if (_networkManager == null)
            {
                Debug.LogError($"[{nameof(SteamMultiplayerBootstrap)}] No NetworkManager found. " +
                               "Assign one in the Inspector.");
                enabled = false;
                return;
            }

            var config = BuildConfig();
            _driver = new NetcodeSessionDriver(_networkManager);
            _controller = new MultiplayerSessionController(CreateBackend(config), _driver, config);

            _controller.StateChanged += HandleStateChanged;
            _controller.Failed += HandleFailed;
        }

        private void Start()
        {
            if (!_joinFromCommandLine || _controller == null)
                return;

            if (ConnectLobbyArguments.TryParseFromProcess(out var lobbyId))
                _controller.Join(lobbyId);
        }

        private void Update()
        {
            // The transport pumps Steam only while a session runs, and lobby callbacks arrive
            // before that. Without this, Host() would never get its answer.
            if (_steamBackend != null && (_networkManager == null || !_networkManager.IsListening))
                SteamRuntime.RunCallbacks();
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.StateChanged -= HandleStateChanged;
                _controller.Failed -= HandleFailed;
                _controller.Dispose();
                _controller = null;
            }

            _driver?.Dispose();
            _driver = null;

            _steamBackend?.Dispose();
            _steamBackend = null;

            SteamRuntime.ShutdownIfOwned();
        }

        private MultiplayerSessionConfig BuildConfig()
        {
            return new MultiplayerSessionConfig
            {
                SteamAppId = _steamAppId,
                MaxPlayers = _maxPlayers,
                Visibility = _visibility,
                GameSignature = _gameSignature,
                ProtocolVersion = _protocolVersion,
                LobbyName = _lobbyName,
            };
        }

        private ILobbyBackend CreateBackend(MultiplayerSessionConfig config)
        {
            if (_useLocalBackend)
                return new LocalLobbyBackend(config);

            // Steam has to come up before the transport does, because a lobby must exist before
            // there is any host address to connect to.
            if (!SteamRuntime.EnsureInitialized(config.SteamAppId, out var error))
            {
                Debug.LogError($"[{nameof(SteamMultiplayerBootstrap)}] {error}");
                return new LocalLobbyBackend(config);
            }

            _steamBackend = new FacepunchLobbyBackend();
            return _steamBackend;
        }

        private void HandleStateChanged(SessionState previous, SessionState next)
        {
            StateChanged?.Invoke(previous, next);
        }

        private void HandleFailed(string message)
        {
            Debug.LogWarning($"[{nameof(SteamMultiplayerBootstrap)}] {message}");
            Failed?.Invoke(message);
        }
    }
}
