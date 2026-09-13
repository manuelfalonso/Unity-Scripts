namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// A snapshot of a lobby at the moment it was created or entered.
    /// </summary>
    public readonly struct LobbyInfo
    {
        /// <summary>The lobby's Steam ID, used to join it and to build invite links.</summary>
        public ulong LobbyId { get; }

        /// <summary>The Steam ID of the lobby owner.</summary>
        public ulong OwnerId { get; }

        /// <summary>How many members are currently in the lobby.</summary>
        public int MemberCount { get; }

        /// <summary>The lobby's capacity, including the owner.</summary>
        public int MaxMembers { get; }

        /// <summary>
        /// Creates a lobby snapshot.
        /// </summary>
        public LobbyInfo(ulong lobbyId, ulong ownerId, int memberCount, int maxMembers)
        {
            LobbyId = lobbyId;
            OwnerId = ownerId;
            MemberCount = memberCount;
            MaxMembers = maxMembers;
        }
    }
}
