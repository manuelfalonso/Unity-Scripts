using System;
using System.Globalization;

namespace SombraStudios.Shared.Networking.Sessions
{
    /// <summary>
    /// Reads the lobby ID Steam appends to the command line when a player accepts an invite
    /// or presses "Join game" while the game is not already running.
    /// </summary>
    /// <remarks>
    /// Steam launches the game as <c>MyGame.exe +connect_lobby 109775241234567890</c>. When the
    /// game <em>is</em> already running, Steam raises a callback instead and this is not used.
    /// Both paths have to be handled for invites to work reliably.
    /// See https://partner.steamgames.com/doc/api/ISteamFriends#GameLobbyJoinRequested_t
    /// </remarks>
    public static class ConnectLobbyArguments
    {
        /// <summary>The command line flag Steam uses to pass the lobby ID.</summary>
        public const string Flag = "+connect_lobby";

        /// <summary>
        /// Finds the lobby ID in a command line.
        /// </summary>
        /// <param name="arguments">
        /// The full argument list, typically <see cref="Environment.GetCommandLineArgs"/>.
        /// </param>
        /// <param name="lobbyId">The parsed lobby ID when this returns true, otherwise zero.</param>
        /// <returns>True when the flag was present and followed by a valid, non-zero lobby ID.</returns>
        public static bool TryParse(string[] arguments, out ulong lobbyId)
        {
            lobbyId = 0;

            if (arguments == null)
                return false;

            // The value is the argument after the flag, so the flag cannot be last.
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (!string.Equals(arguments[i], Flag, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ulong.TryParse(arguments[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    && parsed != 0)
                {
                    lobbyId = parsed;
                    return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Finds the lobby ID in this process's own command line.
        /// </summary>
        /// <param name="lobbyId">The parsed lobby ID when this returns true, otherwise zero.</param>
        /// <returns>True when this process was launched to join a lobby.</returns>
        public static bool TryParseFromProcess(out ulong lobbyId)
        {
            return TryParse(Environment.GetCommandLineArgs(), out lobbyId);
        }
    }
}
