using System;
using System.Collections.Generic;
using SombraStudios.Shared.Networking.Sessions;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace SombraStudios.Shared.Networking.Steam
{
    /// <summary>
    /// An <see cref="ILobbyBackend"/> over Steam lobbies, using Facepunch.Steamworks.
    /// </summary>
    /// <remarks>
    /// Translates Steam's static callbacks into instance events and its <c>Lobby</c> struct into
    /// plain data, so nothing above this class has to reference Steamworks.
    /// See https://partner.steamgames.com/doc/api/ISteamMatchmaking
    /// </remarks>
    public sealed class FacepunchLobbyBackend : ILobbyBackend, IDisposable
    {
        private readonly List<LobbyMember> _members = new List<LobbyMember>();

        private Lobby? _currentLobby;
        private bool _isDisposed;

        /// <summary>
        /// Subscribes to Steam's lobby and friend callbacks.
        /// </summary>
        public FacepunchLobbyBackend()
        {
            SteamMatchmaking.OnLobbyCreated += HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += HandleLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += HandleLobbyMemberLeave;
            SteamMatchmaking.OnLobbyMemberDisconnected += HandleLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested += HandleGameLobbyJoinRequested;
        }

        /// <inheritdoc />
        public bool IsAvailable => SteamRuntime.IsAvailable;

        /// <inheritdoc />
        public bool IsInLobby => _currentLobby.HasValue;

        /// <inheritdoc />
        public ulong LocalUserId => SteamRuntime.LocalUserId;

        /// <inheritdoc />
        public ulong CurrentLobbyId => _currentLobby?.Id.Value ?? 0UL;

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
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!IsAvailable)
            {
                OperationFailed?.Invoke("Steam is not initialised, so no lobby can be created.");
                return;
            }

            CreateLobbyAsync(config);
        }

        /// <inheritdoc />
        public void JoinLobby(ulong lobbyId)
        {
            if (!IsAvailable)
            {
                OperationFailed?.Invoke("Steam is not initialised, so no lobby can be joined.");
                return;
            }

            JoinLobbyAsync(lobbyId);
        }

        /// <inheritdoc />
        public void LeaveLobby()
        {
            if (!_currentLobby.HasValue)
                return;

            var lobby = _currentLobby.Value;
            _currentLobby = null;
            _members.Clear();

            try
            {
                lobby.Leave();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(FacepunchLobbyBackend)}] Failed to leave lobby cleanly: {exception}");
            }

            LobbyLeft?.Invoke();
        }

        /// <inheritdoc />
        public bool InviteFriend()
        {
            if (!_currentLobby.HasValue || !IsAvailable)
                return false;

            // Opens Steam's own friend picker; the invite is sent by Steam, not by us.
            SteamFriends.OpenGameInviteOverlay(_currentLobby.Value.Id);
            return true;
        }

        /// <inheritdoc />
        public void SetLobbyData(string key, string value)
        {
            if (!_currentLobby.HasValue)
                return;

            _currentLobby.Value.SetData(key, value);
        }

        /// <inheritdoc />
        public string GetLobbyData(string key)
        {
            return _currentLobby.HasValue ? _currentLobby.Value.GetData(key) ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Unsubscribes from Steam's static callbacks and leaves any current lobby.
        /// </summary>
        /// <remarks>
        /// Steam's callbacks are static, so a backend that is not disposed keeps a dead instance
        /// alive across domain reloads and double-handles every event.
        /// </remarks>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            SteamMatchmaking.OnLobbyCreated -= HandleLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= HandleLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= HandleLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= HandleLobbyMemberLeave;
            SteamMatchmaking.OnLobbyMemberDisconnected -= HandleLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested -= HandleGameLobbyJoinRequested;

            LeaveLobby();
        }

        private async void CreateLobbyAsync(MultiplayerSessionConfig config)
        {
            try
            {
                var created = await SteamMatchmaking.CreateLobbyAsync(config.MaxPlayers);

                if (!created.HasValue)
                {
                    OperationFailed?.Invoke("Steam refused to create the lobby.");
                    return;
                }

                var lobby = created.Value;
                ApplyVisibility(lobby, config.Visibility);
                lobby.SetJoinable(true);

                // OnLobbyCreated has already fired by the time the task completes, so the
                // controller is notified from here instead.
                AdoptLobby(lobby);
                LobbyCreated?.Invoke(ToLobbyInfo(lobby));
            }
            catch (Exception exception)
            {
                OperationFailed?.Invoke($"Creating the lobby threw: {exception.Message}");
            }
        }

        private async void JoinLobbyAsync(ulong lobbyId)
        {
            try
            {
                var joined = await SteamMatchmaking.JoinLobbyAsync(lobbyId);

                if (!joined.HasValue)
                {
                    OperationFailed?.Invoke($"Steam refused to join lobby {lobbyId}.");
                    return;
                }

                AdoptLobby(joined.Value);
                LobbyEntered?.Invoke(ToLobbyInfo(joined.Value));
            }
            catch (Exception exception)
            {
                OperationFailed?.Invoke($"Joining lobby {lobbyId} threw: {exception.Message}");
            }
        }

        private void AdoptLobby(Lobby lobby)
        {
            _currentLobby = lobby;
            RefreshMembers(lobby);
        }

        private void RefreshMembers(Lobby lobby)
        {
            _members.Clear();

            foreach (var member in lobby.Members)
                _members.Add(new LobbyMember(member.Id.Value, member.Name));
        }

        private static void ApplyVisibility(Lobby lobby, LobbyVisibility visibility)
        {
            switch (visibility)
            {
                case LobbyVisibility.Public:
                    lobby.SetPublic();
                    break;
                case LobbyVisibility.Private:
                    lobby.SetPrivate();
                    break;
                case LobbyVisibility.InviteOnly:
                    lobby.SetInvisible();
                    break;
                default:
                    lobby.SetFriendsOnly();
                    break;
            }
        }

        private static LobbyInfo ToLobbyInfo(Lobby lobby)
        {
            return new LobbyInfo(lobby.Id.Value, lobby.Owner.Id.Value, lobby.MemberCount, lobby.MaxMembers);
        }

        private void HandleLobbyCreated(Result result, Lobby lobby)
        {
            // The async path reports success; this callback only has to surface failures, which
            // the awaited task reports as a null lobby without a reason.
            if (result != Result.OK)
                OperationFailed?.Invoke($"Steam could not create the lobby: {result}.");
        }

        private void HandleLobbyEntered(Lobby lobby)
        {
            // Fires for the owner too, and again for a lobby the async join already reported.
            // Adopting it is harmless; raising the event twice is not, so only the async paths
            // raise LobbyEntered.
            if (_currentLobby.HasValue && _currentLobby.Value.Id.Value == lobby.Id.Value)
                RefreshMembers(lobby);
        }

        private void HandleLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            if (!IsCurrentLobby(lobby))
                return;

            var member = new LobbyMember(friend.Id.Value, friend.Name);
            _members.Add(member);
            MemberJoined?.Invoke(member);
        }

        private void HandleLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            if (!IsCurrentLobby(lobby))
                return;

            var member = new LobbyMember(friend.Id.Value, friend.Name);
            _members.RemoveAll(existing => existing.SteamId == member.SteamId);
            MemberLeft?.Invoke(member);
        }

        private void HandleGameLobbyJoinRequested(Lobby lobby, SteamId inviterId)
        {
            JoinRequested?.Invoke(lobby.Id.Value);
        }

        private bool IsCurrentLobby(Lobby lobby)
        {
            return _currentLobby.HasValue && _currentLobby.Value.Id.Value == lobby.Id.Value;
        }
    }
}
