using System;
using System.Collections.Generic;
using System.Globalization;

namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// The key-value payload a host writes into its Steam lobby so clients can find the
    /// server and confirm they are compatible with it.
    /// </summary>
    /// <remarks>
    /// A Steam lobby carries no game traffic — it is a shared metadata board. Connecting is
    /// therefore two steps: enter the lobby, read <see cref="HostSteamId"/> from here, and
    /// only then open the relay connection to that user.
    /// See https://partner.steamgames.com/doc/api/ISteamMatchmaking#SetLobbyData
    /// </remarks>
    public readonly struct LobbyMetadata
    {
        /// <summary>Metadata key holding the game signature.</summary>
        public const string SignatureKey = "sombra.signature";

        /// <summary>Metadata key holding the protocol version.</summary>
        public const string ProtocolVersionKey = "sombra.protocol";

        /// <summary>Metadata key holding the host's Steam ID.</summary>
        public const string HostSteamIdKey = "sombra.host";

        /// <summary>Metadata key holding the display name of the lobby.</summary>
        public const string LobbyNameKey = "sombra.name";

        /// <summary>Identifies lobbies belonging to this game.</summary>
        public string Signature { get; }

        /// <summary>The wire-format version the host is running.</summary>
        public int ProtocolVersion { get; }

        /// <summary>The Steam ID to open the relay connection to.</summary>
        public ulong HostSteamId { get; }

        /// <summary>Human-readable lobby name, may be empty.</summary>
        public string LobbyName { get; }

        /// <summary>
        /// Creates a metadata payload.
        /// </summary>
        public LobbyMetadata(string signature, int protocolVersion, ulong hostSteamId, string lobbyName)
        {
            Signature = signature;
            ProtocolVersion = protocolVersion;
            HostSteamId = hostSteamId;
            LobbyName = lobbyName;
        }

        /// <summary>
        /// Builds the payload a host publishes, taking the signature and version from
        /// <paramref name="config"/>.
        /// </summary>
        public static LobbyMetadata ForHost(MultiplayerSessionConfig config, ulong hostSteamId)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return new LobbyMetadata(config.GameSignature, config.ProtocolVersion, hostSteamId, config.LobbyName);
        }

        /// <summary>
        /// Flattens the payload into the keys to write onto the lobby.
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>
            {
                { SignatureKey, Signature ?? string.Empty },
                { ProtocolVersionKey, ProtocolVersion.ToString(CultureInfo.InvariantCulture) },
                { HostSteamIdKey, HostSteamId.ToString(CultureInfo.InvariantCulture) },
                { LobbyNameKey, LobbyName ?? string.Empty },
            };
        }

        /// <summary>
        /// Reads a payload back out of a lobby.
        /// </summary>
        /// <param name="readValue">
        /// Reads one metadata key. Steam returns an empty string for keys that were never set,
        /// so this may return null or empty and must not throw.
        /// </param>
        /// <param name="metadata">The parsed payload when this returns true.</param>
        /// <param name="error">Why parsing failed, otherwise null.</param>
        /// <returns>True when every required key was present and well-formed.</returns>
        public static bool TryParse(Func<string, string> readValue, out LobbyMetadata metadata, out string error)
        {
            metadata = default;

            if (readValue == null)
            {
                error = "No metadata reader was supplied.";
                return false;
            }

            var signature = readValue(SignatureKey);
            if (string.IsNullOrWhiteSpace(signature))
            {
                error = "Lobby has no game signature, so it belongs to another game sharing the App ID.";
                return false;
            }

            var rawProtocol = readValue(ProtocolVersionKey);
            if (!int.TryParse(rawProtocol, NumberStyles.Integer, CultureInfo.InvariantCulture, out var protocolVersion))
            {
                error = $"Lobby protocol version '{rawProtocol}' is not a number.";
                return false;
            }

            var rawHost = readValue(HostSteamIdKey);
            if (!ulong.TryParse(rawHost, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hostSteamId)
                || hostSteamId == 0)
            {
                error = $"Lobby host Steam ID '{rawHost}' is missing or invalid.";
                return false;
            }

            metadata = new LobbyMetadata(signature, protocolVersion, hostSteamId, readValue(LobbyNameKey) ?? string.Empty);
            error = null;
            return true;
        }

        /// <summary>
        /// Whether a local build using <paramref name="config"/> may join this lobby.
        /// </summary>
        /// <param name="config">The local session configuration.</param>
        /// <param name="reason">Why the lobby was rejected, otherwise null.</param>
        /// <returns>True when signature and protocol version both match.</returns>
        public bool IsCompatibleWith(MultiplayerSessionConfig config, out string reason)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!string.Equals(Signature, config.GameSignature, StringComparison.Ordinal))
            {
                reason = $"Lobby belongs to '{Signature}', not '{config.GameSignature}'.";
                return false;
            }

            if (ProtocolVersion != config.ProtocolVersion)
            {
                reason = $"Lobby runs protocol {ProtocolVersion}, this build runs {config.ProtocolVersion}.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
