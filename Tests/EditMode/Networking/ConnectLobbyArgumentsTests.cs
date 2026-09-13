using NUnit.Framework;
using SombraStudios.Shared.Networking.Sessions;

namespace SombraStudios.Shared.Tests.Networking
{
    public class ConnectLobbyArgumentsTests
    {
        private const ulong LobbyId = 109775241234567890;

        [Test]
        public void TryParse_ReadsTheLobbyIdSteamAppends()
        {
            var arguments = new[] { "MyGame.exe", "+connect_lobby", "109775241234567890" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out var lobbyId), Is.True);
            Assert.That(lobbyId, Is.EqualTo(LobbyId));
        }

        [Test]
        public void TryParse_IgnoresSurroundingArguments()
        {
            var arguments = new[]
            {
                "MyGame.exe", "-screen-fullscreen", "0", "+connect_lobby", "109775241234567890", "-logFile", "out.log",
            };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out var lobbyId), Is.True);
            Assert.That(lobbyId, Is.EqualTo(LobbyId));
        }

        [Test]
        public void TryParse_AcceptsDifferentCasing()
        {
            var arguments = new[] { "MyGame.exe", "+Connect_Lobby", "42" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out var lobbyId), Is.True);
            Assert.That(lobbyId, Is.EqualTo(42UL));
        }

        [Test]
        public void TryParse_ReturnsFalseWhenTheFlagIsAbsent()
        {
            var arguments = new[] { "MyGame.exe", "-batchmode" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out var lobbyId), Is.False);
            Assert.That(lobbyId, Is.Zero);
        }

        [Test]
        public void TryParse_ReturnsFalseWhenTheFlagIsTheLastArgument()
        {
            var arguments = new[] { "MyGame.exe", "+connect_lobby" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out var lobbyId), Is.False);
            Assert.That(lobbyId, Is.Zero);
        }

        [Test]
        public void TryParse_ReturnsFalseForANonNumericValue()
        {
            var arguments = new[] { "MyGame.exe", "+connect_lobby", "not-a-lobby" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out var lobbyId), Is.False);
            Assert.That(lobbyId, Is.Zero);
        }

        [Test]
        public void TryParse_ReturnsFalseForAZeroLobbyId()
        {
            var arguments = new[] { "MyGame.exe", "+connect_lobby", "0" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out var lobbyId), Is.False);
        }

        [Test]
        public void TryParse_ReturnsFalseForANegativeValue()
        {
            var arguments = new[] { "MyGame.exe", "+connect_lobby", "-1" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out _), Is.False);
        }

        [Test]
        public void TryParse_HandlesNullAndEmptyArgumentLists()
        {
            Assert.That(ConnectLobbyArguments.TryParse(null, out var fromNull), Is.False);
            Assert.That(fromNull, Is.Zero);

            Assert.That(ConnectLobbyArguments.TryParse(new string[0], out var fromEmpty), Is.False);
            Assert.That(fromEmpty, Is.Zero);
        }

        [Test]
        public void TryParse_DoesNotFallThroughToALaterValidFlag()
        {
            // Steam passes the flag once. A malformed first occurrence is a bug worth surfacing,
            // not something to paper over by scanning for a second one.
            var arguments = new[] { "MyGame.exe", "+connect_lobby", "bad", "+connect_lobby", "42" };

            Assert.That(ConnectLobbyArguments.TryParse(arguments, out _), Is.False);
        }

        [Test]
        public void TryParseFromProcess_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ConnectLobbyArguments.TryParseFromProcess(out _));
        }
    }
}
