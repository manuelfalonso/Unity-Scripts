using System;
using System.Collections.Generic;
using SombraStudios.Shared.Networking.Sessions;

namespace SombraStudios.Shared.Tests.Networking
{
    /// <summary>
    /// An <see cref="ILobbyBackend"/> whose callbacks are raised by the test rather than by Steam,
    /// so the asynchronous connection flow can be stepped through deterministically.
    /// </summary>
    internal sealed class FakeLobbyBackend : ILobbyBackend
    {
        private readonly List<LobbyMember> _members = new List<LobbyMember>();
        private readonly Dictionary<string, string> _data = new Dictionary<string, string>();

        public bool IsAvailable { get; set; } = true;

        public bool IsInLobby { get; private set; }

        public ulong LocalUserId { get; set; } = 1001;

        public ulong CurrentLobbyId { get; private set; }

        public IReadOnlyList<LobbyMember> Members => _members;

        public bool InviteResult { get; set; } = true;

        public int CreateLobbyCalls { get; private set; }

        public int JoinLobbyCalls { get; private set; }

        public int LeaveLobbyCalls { get; private set; }

        public int InviteCalls { get; private set; }

        public ulong LastRequestedLobbyId { get; private set; }

        public IReadOnlyDictionary<string, string> PublishedData => _data;

        public event Action<LobbyInfo> LobbyCreated;
        public event Action<LobbyInfo> LobbyEntered;
        public event Action LobbyLeft;
        public event Action<LobbyMember> MemberJoined;
        public event Action<LobbyMember> MemberLeft;
        public event Action<ulong> JoinRequested;
        public event Action<string> OperationFailed;

        public void CreateLobby(MultiplayerSessionConfig config)
        {
            CreateLobbyCalls++;
        }

        public void JoinLobby(ulong lobbyId)
        {
            JoinLobbyCalls++;
            LastRequestedLobbyId = lobbyId;
        }

        public void LeaveLobby()
        {
            LeaveLobbyCalls++;

            if (!IsInLobby)
                return;

            IsInLobby = false;
            CurrentLobbyId = 0;
            _members.Clear();
            _data.Clear();
            LobbyLeft?.Invoke();
        }

        public bool InviteFriend()
        {
            InviteCalls++;
            return InviteResult;
        }

        public void SetLobbyData(string key, string value)
        {
            _data[key] = value;
        }

        public string GetLobbyData(string key)
        {
            return _data.TryGetValue(key, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Mimics Steam answering a create request. Steam raises LobbyEntered for the owner too,
        /// which is exactly the duplicate the controller has to ignore.
        /// </summary>
        public void CompleteCreate(ulong lobbyId = 500)
        {
            IsInLobby = true;
            CurrentLobbyId = lobbyId;
            _members.Add(new LobbyMember(LocalUserId, "Host"));

            var info = new LobbyInfo(lobbyId, LocalUserId, _members.Count, 4);
            LobbyCreated?.Invoke(info);
            LobbyEntered?.Invoke(info);
        }

        /// <summary>
        /// Mimics Steam answering a join request, with the host metadata already published.
        /// </summary>
        public void CompleteJoin(ulong lobbyId, ulong ownerId)
        {
            IsInLobby = true;
            CurrentLobbyId = lobbyId;
            _members.Add(new LobbyMember(ownerId, "Host"));
            _members.Add(new LobbyMember(LocalUserId, "Client"));

            LobbyEntered?.Invoke(new LobbyInfo(lobbyId, ownerId, _members.Count, 4));
        }

        /// <summary>Publishes the metadata a host would have written, for join-side tests.</summary>
        public void SeedHostMetadata(MultiplayerSessionConfig config, ulong hostSteamId)
        {
            foreach (var entry in LobbyMetadata.ForHost(config, hostSteamId).ToDictionary())
                _data[entry.Key] = entry.Value;
        }

        public void RaiseOperationFailed(string message)
        {
            OperationFailed?.Invoke(message);
        }

        public void RaiseJoinRequested(ulong lobbyId)
        {
            JoinRequested?.Invoke(lobbyId);
        }

        public void RaiseMemberJoined(LobbyMember member)
        {
            _members.Add(member);
            MemberJoined?.Invoke(member);
        }

        public void RaiseMemberLeft(LobbyMember member)
        {
            _members.Remove(member);
            MemberLeft?.Invoke(member);
        }

        /// <summary>Mimics the host closing the lobby out from under a client.</summary>
        public void RaiseLobbyClosedRemotely()
        {
            IsInLobby = false;
            CurrentLobbyId = 0;
            _members.Clear();
            LobbyLeft?.Invoke();
        }
    }
}
