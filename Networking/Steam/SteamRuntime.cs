using System;
using Steamworks;
using UnityEngine;

namespace SombraStudios.Shared.Networking.Steam
{
    /// <summary>
    /// Initialises and shuts down the Steam client exactly once, no matter how many systems ask.
    /// </summary>
    /// <remarks>
    /// <c>FacepunchTransport</c> calls <see cref="SteamClient.Init"/> in its own
    /// <c>Initialize()</c> and <see cref="SteamClient.Shutdown"/> in its <c>Shutdown()</c>. A second
    /// caller doing the same is the most common cause of "Steamworks is not initialized" and of a
    /// client that silently stops receiving callbacks, so ownership is tracked here: whoever
    /// initialised Steam is the only one allowed to shut it down.
    /// <para>
    /// Steam must also be initialised <em>before</em> the transport, because a lobby has to exist
    /// before there is anything to connect to. See
    /// https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown
    /// </para>
    /// </remarks>
    public static class SteamRuntime
    {
        private static bool _isOwnedHere;

        /// <summary>Whether Steam is initialised and usable right now.</summary>
        public static bool IsAvailable => SteamClient.IsValid;

        /// <summary>The local user's Steam ID, or zero when Steam is unavailable.</summary>
        public static ulong LocalUserId => SteamClient.IsValid ? SteamClient.SteamId.Value : 0UL;

        /// <summary>The local user's persona name, or an empty string when Steam is unavailable.</summary>
        public static string LocalUserName => SteamClient.IsValid ? SteamClient.Name : string.Empty;

        /// <summary>
        /// Initialises Steam if nothing else has yet, and opens access to the relay network.
        /// </summary>
        /// <param name="appId">
        /// The Steam App ID. Use <see cref="Sessions.MultiplayerSessionConfig.SpacewarAppId"/>
        /// during development.
        /// </param>
        /// <param name="error">Why initialisation failed, otherwise null.</param>
        /// <returns>True when Steam is usable after this call, including when it already was.</returns>
        public static bool EnsureInitialized(uint appId, out string error)
        {
            if (SteamClient.IsValid)
            {
                error = null;
                return true;
            }

            try
            {
                SteamClient.Init(appId, false);
                _isOwnedHere = true;
            }
            catch (Exception exception)
            {
                error = $"Steam could not be initialised for App ID {appId}. " +
                        $"Is the Steam client running and is steam_appid.txt present? ({exception.Message})";
                return false;
            }

            if (!SteamClient.IsValid)
            {
                error = $"Steam reported an invalid client for App ID {appId}.";
                return false;
            }

            // Warms the relay so the first connection does not pay for route discovery.
            SteamNetworkingUtils.InitRelayNetworkAccess();

            error = null;
            return true;
        }

        /// <summary>
        /// Pumps Steam callbacks.
        /// </summary>
        /// <remarks>
        /// The transport pumps these itself, but only while a network session is running. Lobby
        /// creation happens before that, so something has to drive callbacks in the meantime or
        /// the create and join results never arrive.
        /// </remarks>
        public static void RunCallbacks()
        {
            if (!SteamClient.IsValid)
                return;

            try
            {
                SteamClient.RunCallbacks();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(SteamRuntime)}] Failed to run Steam callbacks: {exception}");
            }
        }

        /// <summary>
        /// Shuts Steam down only if this class was the one that started it.
        /// </summary>
        /// <remarks>
        /// Shutting down a client the transport owns would break the running session.
        /// </remarks>
        public static void ShutdownIfOwned()
        {
            if (!_isOwnedHere || !SteamClient.IsValid)
                return;

            _isOwnedHere = false;
            SteamClient.Shutdown();
        }
    }
}
