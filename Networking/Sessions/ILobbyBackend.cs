using System;
using System.Collections.Generic;

namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// The matchmaking half of a session: finding other players and agreeing who hosts.
    /// It carries no game traffic — that is <see cref="INetworkSessionDriver"/>'s job.
    /// </summary>
    /// <remarks>
    /// Implemented over Steam lobbies for real play and in memory for solo iteration, so the
    /// session logic above it never needs to know which one is running.
    /// </remarks>
    public interface ILobbyBackend
    {
        /// <summary>Whether the underlying service is running and usable right now.</summary>
        bool IsAvailable { get; }

        /// <summary>Whether a lobby is currently entered.</summary>
        bool IsInLobby { get; }

        /// <summary>The local user's ID, or zero when the backend is unavailable.</summary>
        ulong LocalUserId { get; }

        /// <summary>The current lobby's ID, or zero when not in a lobby.</summary>
        ulong CurrentLobbyId { get; }

        /// <summary>The current members, empty when not in a lobby.</summary>
        IReadOnlyList<LobbyMember> Members { get; }

        /// <summary>Raised on the owner once its own lobby exists.</summary>
        event Action<LobbyInfo> LobbyCreated;

        /// <summary>Raised on every member, owner included, once the lobby has been entered.</summary>
        event Action<LobbyInfo> LobbyEntered;

        /// <summary>Raised after leaving a lobby, whether deliberately or by disconnection.</summary>
        event Action LobbyLeft;

        /// <summary>Raised when another player enters the lobby.</summary>
        event Action<LobbyMember> MemberJoined;

        /// <summary>Raised when another player leaves the lobby.</summary>
        event Action<LobbyMember> MemberLeft;

        /// <summary>
        /// Raised with a lobby ID when the player accepts an invite or presses "Join game"
        /// while this build is already running.
        /// </summary>
        event Action<ulong> JoinRequested;

        /// <summary>Raised with a human-readable reason when a create or join attempt fails.</summary>
        event Action<string> OperationFailed;

        /// <summary>
        /// Requests a new lobby. <see cref="LobbyCreated"/> or <see cref="OperationFailed"/>
        /// answers it.
        /// </summary>
        void CreateLobby(MultiplayerSessionConfig config);

        /// <summary>
        /// Requests entry into an existing lobby. <see cref="LobbyEntered"/> or
        /// <see cref="OperationFailed"/> answers it.
        /// </summary>
        void JoinLobby(ulong lobbyId);

        /// <summary>Leaves the current lobby. Safe to call when not in one.</summary>
        void LeaveLobby();

        /// <summary>
        /// Opens the platform's invite dialog for the current lobby.
        /// </summary>
        /// <returns>False when there is no lobby to invite anyone to.</returns>
        bool InviteFriend();

        /// <summary>Writes a metadata key onto the current lobby. Owner only.</summary>
        void SetLobbyData(string key, string value);

        /// <summary>
        /// Reads a metadata key from the current lobby.
        /// </summary>
        /// <returns>An empty string when the key was never set.</returns>
        string GetLobbyData(string key);
    }
}
