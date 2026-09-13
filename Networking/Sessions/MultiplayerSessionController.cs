using System;
using System.Collections.Generic;

namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// Drives a multiplayer session end to end: creates or enters a lobby, publishes and reads
    /// the host address through lobby metadata, and starts the netcode layer against it.
    /// </summary>
    /// <remarks>
    /// This is the piece worth testing. It has no Unity and no Steamworks dependency — it talks
    /// only to <see cref="ILobbyBackend"/> and <see cref="INetworkSessionDriver"/>, so the whole
    /// connection flow can be exercised in Edit Mode with fakes.
    /// <para>
    /// The two-step connect it implements is the part Steam tutorials usually skip: a lobby is a
    /// metadata board, not a connection, so the host writes its own Steam ID into the lobby and
    /// each client reads it back before opening the relay connection.
    /// </para>
    /// </remarks>
    public sealed class MultiplayerSessionController : IDisposable
    {
        private static readonly IReadOnlyList<LobbyMember> NoMembers = new LobbyMember[0];

        private readonly ILobbyBackend _backend;
        private readonly INetworkSessionDriver _driver;
        private readonly SessionStateMachine _stateMachine = new SessionStateMachine();

        private bool _isTearingDown;
        private bool _isDisposed;

        /// <summary>
        /// Creates a controller over a lobby backend and a netcode driver.
        /// </summary>
        /// <param name="backend">The matchmaking implementation, Steam or local.</param>
        /// <param name="driver">The netcode implementation.</param>
        /// <param name="config">Session tunables. Defaults are used when null.</param>
        public MultiplayerSessionController(
            ILobbyBackend backend,
            INetworkSessionDriver driver,
            MultiplayerSessionConfig config = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            Config = config ?? new MultiplayerSessionConfig();

            _stateMachine.StateChanged += HandleStateChanged;

            _backend.LobbyCreated += HandleLobbyCreated;
            _backend.LobbyEntered += HandleLobbyEntered;
            _backend.LobbyLeft += HandleLobbyLeft;
            _backend.MemberJoined += HandleMemberJoined;
            _backend.MemberLeft += HandleMemberLeft;
            _backend.JoinRequested += HandleJoinRequested;
            _backend.OperationFailed += HandleOperationFailed;

            _driver.Connected += HandleDriverConnected;
            _driver.Disconnected += HandleDriverDisconnected;
        }

        /// <summary>The tunables this session runs with.</summary>
        public MultiplayerSessionConfig Config { get; }

        /// <summary>The current lifecycle state.</summary>
        public SessionState State => _stateMachine.State;

        /// <summary>True once the session carries traffic, as host or as client.</summary>
        public bool IsActive => _stateMachine.IsActive;

        /// <summary>True while a create or join is still in flight.</summary>
        public bool IsBusy => _stateMachine.IsBusy;

        /// <summary>The current lobby members, empty when not in a lobby.</summary>
        public IReadOnlyList<LobbyMember> Members => _backend.Members ?? NoMembers;

        /// <summary>The current lobby ID, or zero when not in a lobby.</summary>
        public ulong CurrentLobbyId => _backend.CurrentLobbyId;

        /// <summary>Raised after every state change, with the previous and the new state.</summary>
        public event Action<SessionState, SessionState> StateChanged;

        /// <summary>
        /// Raised with a human-readable reason whenever the session drops back offline unexpectedly.
        /// </summary>
        public event Action<string> Failed;

        /// <summary>Raised when another player enters the lobby.</summary>
        public event Action<LobbyMember> MemberJoined;

        /// <summary>Raised when another player leaves the lobby.</summary>
        public event Action<LobbyMember> MemberLeft;

        /// <summary>
        /// Creates a lobby and starts hosting it.
        /// </summary>
        /// <returns>False when the session is busy, already active, or the backend is unavailable.</returns>
        public bool Host()
        {
            if (!Config.Validate(out var configError))
            {
                Failed?.Invoke(configError);
                return false;
            }

            if (!_stateMachine.CanTransitionTo(SessionState.Creating))
                return false;

            if (!_backend.IsAvailable)
            {
                Failed?.Invoke("The lobby backend is not available. Is the Steam client running?");
                return false;
            }

            _stateMachine.TryTransitionTo(SessionState.Creating);
            _backend.CreateLobby(Config);
            return true;
        }

        /// <summary>
        /// Enters an existing lobby and connects to whoever hosts it.
        /// </summary>
        /// <param name="lobbyId">The lobby to enter.</param>
        /// <returns>False when the session is busy, already active, or the backend is unavailable.</returns>
        public bool Join(ulong lobbyId)
        {
            if (lobbyId == 0)
            {
                Failed?.Invoke("Cannot join lobby zero.");
                return false;
            }

            if (!_stateMachine.CanTransitionTo(SessionState.Joining))
                return false;

            if (!_backend.IsAvailable)
            {
                Failed?.Invoke("The lobby backend is not available. Is the Steam client running?");
                return false;
            }

            _stateMachine.TryTransitionTo(SessionState.Joining);
            _backend.JoinLobby(lobbyId);
            return true;
        }

        /// <summary>
        /// Opens the platform invite dialog for the current lobby.
        /// </summary>
        /// <returns>False when there is no lobby to invite anyone to.</returns>
        public bool Invite()
        {
            return _backend.IsInLobby && _backend.InviteFriend();
        }

        /// <summary>
        /// Shuts the netcode layer down and leaves the lobby. Safe to call when already offline.
        /// </summary>
        public void Leave()
        {
            Teardown();
        }

        /// <summary>
        /// Unsubscribes from both collaborators and leaves any active session.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            _backend.LobbyCreated -= HandleLobbyCreated;
            _backend.LobbyEntered -= HandleLobbyEntered;
            _backend.LobbyLeft -= HandleLobbyLeft;
            _backend.MemberJoined -= HandleMemberJoined;
            _backend.MemberLeft -= HandleMemberLeft;
            _backend.JoinRequested -= HandleJoinRequested;
            _backend.OperationFailed -= HandleOperationFailed;

            _driver.Connected -= HandleDriverConnected;
            _driver.Disconnected -= HandleDriverDisconnected;

            Teardown();

            _stateMachine.StateChanged -= HandleStateChanged;
        }

        private void HandleLobbyCreated(LobbyInfo info)
        {
            if (State != SessionState.Creating)
                return;

            var metadata = LobbyMetadata.ForHost(Config, _backend.LocalUserId);
            foreach (var entry in metadata.ToDictionary())
                _backend.SetLobbyData(entry.Key, entry.Value);

            if (!_driver.StartHost())
            {
                Fail("The netcode layer refused to start as host.");
                return;
            }

            _stateMachine.TryTransitionTo(SessionState.Hosting);
        }

        private void HandleLobbyEntered(LobbyInfo info)
        {
            // The owner also receives this for its own lobby; only a joining client acts on it.
            if (State != SessionState.Joining)
                return;

            if (!LobbyMetadata.TryParse(_backend.GetLobbyData, out var metadata, out var parseError))
            {
                Fail(parseError);
                return;
            }

            if (!metadata.IsCompatibleWith(Config, out var incompatibleReason))
            {
                Fail(incompatibleReason);
                return;
            }

            if (!_driver.StartClient(metadata.HostSteamId))
                Fail("The netcode layer refused to start as client.");
        }

        private void HandleLobbyLeft()
        {
            // A deliberate leave already tears down; this path is the host closing the lobby.
            if (_isTearingDown || State == SessionState.Offline)
                return;

            Fail("The lobby was closed.");
        }

        private void HandleMemberJoined(LobbyMember member)
        {
            MemberJoined?.Invoke(member);
        }

        private void HandleMemberLeft(LobbyMember member)
        {
            MemberLeft?.Invoke(member);
        }

        private void HandleJoinRequested(ulong lobbyId)
        {
            // Accepting an invite while already in a session replaces that session.
            if (State != SessionState.Offline)
                Teardown();

            Join(lobbyId);
        }

        private void HandleOperationFailed(string message)
        {
            Fail(message);
        }

        private void HandleDriverConnected()
        {
            if (State == SessionState.Joining)
                _stateMachine.TryTransitionTo(SessionState.Connected);
        }

        private void HandleDriverDisconnected()
        {
            if (_isTearingDown || State == SessionState.Offline)
                return;

            Fail("The connection to the host was lost.");
        }

        private void HandleStateChanged(SessionState previous, SessionState next)
        {
            StateChanged?.Invoke(previous, next);
        }

        private void Fail(string message)
        {
            Teardown();
            Failed?.Invoke(message);
        }

        private void Teardown()
        {
            if (_isTearingDown)
                return;

            _isTearingDown = true;

            try
            {
                _driver.Shutdown();
                _backend.LeaveLobby();
            }
            finally
            {
                _isTearingDown = false;
            }

            _stateMachine.Reset();
        }
    }
}
