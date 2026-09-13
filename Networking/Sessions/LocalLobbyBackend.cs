using System;
using System.Collections.Generic;

namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// An in-memory <see cref="ILobbyBackend"/> that answers immediately and needs no platform
    /// service, for iterating on a multiplayer flow with a single machine.
    /// </summary>
    /// <remarks>
    /// Steam allows one signed-in client per machine, so two real Steam peers need two machines.
    /// This backend removes matchmaking from the loop: pair it with a driver pointed at a local
    /// address and two Editor instances or builds can host and join each other, exercising the
    /// same <see cref="MultiplayerSessionController"/> code path that will later run over Steam.
    /// <para>
    /// It fakes matchmaking only. The lobby is per-process and is never shared, so the metadata a
    /// joining client reads is fabricated from its own configuration rather than received from
    /// the host. That makes it useless for testing the metadata handshake itself — verify that on
    /// two machines against <c>FacepunchLobbyBackend</c>.
    /// </para>
    /// <para>
    /// For the same reason <see cref="Members"/> is a fixed, made-up pair and never reflects who
    /// is actually connected: no <see cref="MemberJoined"/> or <see cref="MemberLeft"/> is raised
    /// for real peers, because this backend cannot see them. Read
    /// <c>NetworkManager.ConnectedClientsIds</c> for the truth while running locally.
    /// </para>
    /// </remarks>
    public sealed class LocalLobbyBackend : ILobbyBackend
    {
        private const ulong LocalHostId = 1;
        private const ulong LocalGuestId = 2;
        private const ulong LocalLobbyId = 1000;

        private readonly Dictionary<string, string> _data = new Dictionary<string, string>();
        private readonly List<LobbyMember> _members = new List<LobbyMember>();

        private MultiplayerSessionConfig _config;

        /// <summary>
        /// Creates a local backend that fabricates metadata matching <paramref name="config"/>.
        /// </summary>
        /// <param name="config">
        /// The same configuration the session runs with. Required for joining: an instance that
        /// only ever joins never sees a <see cref="CreateLobby"/> call, so without it the
        /// fabricated metadata would carry default values and the compatibility check would
        /// reject the join against any customised signature or protocol version.
        /// </param>
        public LocalLobbyBackend(MultiplayerSessionConfig config = null)
        {
            _config = config;
        }

        /// <inheritdoc />
        public bool IsAvailable => true;

        /// <inheritdoc />
        public bool IsInLobby { get; private set; }

        /// <inheritdoc />
        public ulong LocalUserId { get; private set; } = LocalHostId;

        /// <inheritdoc />
        public ulong CurrentLobbyId { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<LobbyMember> Members => _members;

        /// <inheritdoc />
        public event Action<LobbyInfo> LobbyCreated;

        /// <inheritdoc />
        public event Action<LobbyInfo> LobbyEntered;

        /// <inheritdoc />
        public event Action LobbyLeft;

        /// <inheritdoc />
        public event Action<LobbyMember> MemberJoined;

        /// <inheritdoc />
        public event Action<LobbyMember> MemberLeft;

        /// <inheritdoc />
        public event Action<ulong> JoinRequested;

        /// <inheritdoc />
        public event Action<string> OperationFailed;

        /// <inheritdoc />
        public void CreateLobby(MultiplayerSessionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            LocalUserId = LocalHostId;
            CurrentLobbyId = LocalLobbyId;
            IsInLobby = true;
            _data.Clear();
            _members.Clear();
            _members.Add(new LobbyMember(LocalHostId, "Local Host"));

            var info = new LobbyInfo(LocalLobbyId, LocalHostId, _members.Count, config.MaxPlayers);
            LobbyCreated?.Invoke(info);
        }

        /// <inheritdoc />
        public void JoinLobby(ulong lobbyId)
        {
            if (_config == null)
                _config = new MultiplayerSessionConfig();

            LocalUserId = LocalGuestId;
            CurrentLobbyId = lobbyId == 0 ? LocalLobbyId : lobbyId;
            IsInLobby = true;
            _members.Clear();
            _members.Add(new LobbyMember(LocalHostId, "Local Host"));
            _members.Add(new LobbyMember(LocalGuestId, "Local Guest"));

            // Stands in for the metadata a real host would have published, so the controller
            // takes exactly the same code path it takes against Steam.
            foreach (var entry in LobbyMetadata.ForHost(_config, LocalHostId).ToDictionary())
                _data[entry.Key] = entry.Value;

            LobbyEntered?.Invoke(new LobbyInfo(CurrentLobbyId, LocalHostId, _members.Count, _config.MaxPlayers));
        }

        /// <inheritdoc />
        public void LeaveLobby()
        {
            if (!IsInLobby)
                return;

            IsInLobby = false;
            CurrentLobbyId = 0;
            _members.Clear();
            _data.Clear();
            LobbyLeft?.Invoke();
        }

        /// <summary>
        /// Always fails: there is no overlay to open without a platform service.
        /// </summary>
        /// <returns>Always false.</returns>
        public bool InviteFriend()
        {
            OperationFailed?.Invoke("Invites need a platform backend. Switch to Steam to send one.");
            return false;
        }

        /// <inheritdoc />
        public void SetLobbyData(string key, string value)
        {
            if (IsInLobby)
                _data[key] = value;
        }

        /// <inheritdoc />
        public string GetLobbyData(string key)
        {
            return _data.TryGetValue(key, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Raises <see cref="JoinRequested"/>, standing in for an accepted invite.
        /// </summary>
        /// <param name="lobbyId">The lobby the request points at.</param>
        public void SimulateJoinRequest(ulong lobbyId)
        {
            JoinRequested?.Invoke(lobbyId);
        }

        /// <summary>
        /// Raises <see cref="MemberJoined"/>, standing in for another player arriving.
        /// </summary>
        /// <param name="member">The member that arrived.</param>
        public void SimulateMemberJoined(LobbyMember member)
        {
            _members.Add(member);
            MemberJoined?.Invoke(member);
        }

        /// <summary>
        /// Raises <see cref="MemberLeft"/>, standing in for another player leaving.
        /// </summary>
        /// <param name="member">The member that left.</param>
        public void SimulateMemberLeft(LobbyMember member)
        {
            _members.RemoveAll(existing => existing.SteamId == member.SteamId);
            MemberLeft?.Invoke(member);
        }
    }
}
