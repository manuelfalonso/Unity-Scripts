namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// Tunables for a multiplayer session. Constructible with <c>new</c> and usable as-is —
    /// every value has a working default, so no asset or Inspector setup is required.
    /// </summary>
    public sealed class MultiplayerSessionConfig
    {
        /// <summary>
        /// Valve's public test app, usable by any Steam account during development.
        /// </summary>
        /// <remarks>
        /// Every developer testing without their own app shares this ID, so its lobby list is
        /// full of strangers. <see cref="GameSignature"/> is what keeps us out of them.
        /// See https://partner.steamgames.com/doc/sdk/api#steam_appid
        /// </remarks>
        public const uint SpacewarAppId = 480;

        /// <summary>Steam's own ceiling on lobby members.</summary>
        public const int MaxSupportedPlayers = 250;

        /// <summary>A session needs a host and at least one other player to be multiplayer.</summary>
        public const int MinSupportedPlayers = 2;

        /// <summary>The Steam App ID to initialise with. Defaults to <see cref="SpacewarAppId"/>.</summary>
        public uint SteamAppId { get; set; } = SpacewarAppId;

        /// <summary>Lobby capacity, including the host.</summary>
        public int MaxPlayers { get; set; } = 4;

        /// <summary>Who may find and enter the lobby.</summary>
        public LobbyVisibility Visibility { get; set; } = LobbyVisibility.FriendsOnly;

        /// <summary>
        /// Identifies lobbies belonging to this game, so a shared App ID does not mean a shared
        /// lobby list. Override it per project.
        /// </summary>
        public string GameSignature { get; set; } = "sombra.shared";

        /// <summary>
        /// Bumped whenever the wire format changes, so an old build cannot join a new one.
        /// </summary>
        public int ProtocolVersion { get; set; } = 1;

        /// <summary>Human-readable name written into the lobby metadata.</summary>
        public string LobbyName { get; set; } = "Lobby";

        /// <summary>
        /// Checks the configuration before it reaches Steam, where a bad value surfaces as a
        /// silent failure rather than an error.
        /// </summary>
        /// <param name="error">A description of the first problem found, otherwise null.</param>
        /// <returns>True when the configuration can be used to create a lobby.</returns>
        public bool Validate(out string error)
        {
            if (SteamAppId == 0)
            {
                error = "SteamAppId must not be zero.";
                return false;
            }

            if (MaxPlayers < MinSupportedPlayers || MaxPlayers > MaxSupportedPlayers)
            {
                error = $"MaxPlayers must be between {MinSupportedPlayers} and {MaxSupportedPlayers}, " +
                        $"but was {MaxPlayers}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(GameSignature))
            {
                error = "GameSignature must not be empty, otherwise foreign lobbies cannot be filtered out.";
                return false;
            }

            if (ProtocolVersion < 0)
            {
                error = $"ProtocolVersion must not be negative, but was {ProtocolVersion}.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
