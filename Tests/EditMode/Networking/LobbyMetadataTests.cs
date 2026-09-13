using System.Collections.Generic;
using NUnit.Framework;
using SombraStudios.Shared.Networking.Sessions;

namespace SombraStudios.Shared.Tests.Networking
{
    public class LobbyMetadataTests
    {
        private const ulong HostSteamId = 76561197960287930;

        private MultiplayerSessionConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new MultiplayerSessionConfig
            {
                GameSignature = "sombra.testgame",
                ProtocolVersion = 3,
                LobbyName = "Test Lobby",
            };
        }

        [Test]
        public void ForHost_ThenTryParse_RoundTripsEveryField()
        {
            var published = LobbyMetadata.ForHost(_config, HostSteamId).ToDictionary();

            Assert.That(LobbyMetadata.TryParse(Reader(published), out var parsed, out var error), Is.True, error);
            Assert.That(parsed.Signature, Is.EqualTo("sombra.testgame"));
            Assert.That(parsed.ProtocolVersion, Is.EqualTo(3));
            Assert.That(parsed.HostSteamId, Is.EqualTo(HostSteamId));
            Assert.That(parsed.LobbyName, Is.EqualTo("Test Lobby"));
        }

        [Test]
        public void TryParse_PreservesLargeSteamIdsWithoutPrecisionLoss()
        {
            // Steam IDs exceed what a double or an int can hold, so the parser must stay on ulong.
            const ulong maximumSteamId = ulong.MaxValue - 1;
            var published = LobbyMetadata.ForHost(_config, maximumSteamId).ToDictionary();

            LobbyMetadata.TryParse(Reader(published), out var parsed, out _);

            Assert.That(parsed.HostSteamId, Is.EqualTo(maximumSteamId));
        }

        [Test]
        public void TryParse_RejectsAForeignLobbyOnTheSharedAppId()
        {
            // App ID 480 is shared with every other developer testing against Spacewar, so an
            // unsigned lobby is somebody else's and must never be entered.
            var foreign = new Dictionary<string, string>();

            Assert.That(LobbyMetadata.TryParse(Reader(foreign), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("signature"));
        }

        [Test]
        public void TryParse_RejectsAWhitespaceSignature()
        {
            var published = LobbyMetadata.ForHost(_config, HostSteamId).ToDictionary();
            published[LobbyMetadata.SignatureKey] = "   ";

            Assert.That(LobbyMetadata.TryParse(Reader(published), out _, out _), Is.False);
        }

        [Test]
        public void TryParse_RejectsANonNumericProtocolVersion()
        {
            var published = LobbyMetadata.ForHost(_config, HostSteamId).ToDictionary();
            published[LobbyMetadata.ProtocolVersionKey] = "three";

            Assert.That(LobbyMetadata.TryParse(Reader(published), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("protocol"));
        }

        [Test]
        public void TryParse_RejectsAMissingHostSteamId()
        {
            var published = LobbyMetadata.ForHost(_config, HostSteamId).ToDictionary();
            published.Remove(LobbyMetadata.HostSteamIdKey);

            Assert.That(LobbyMetadata.TryParse(Reader(published), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("host"));
        }

        [Test]
        public void TryParse_RejectsAZeroHostSteamId()
        {
            // Zero parses fine as a number but is not a connectable identity.
            var published = LobbyMetadata.ForHost(_config, 0).ToDictionary();

            Assert.That(LobbyMetadata.TryParse(Reader(published), out _, out _), Is.False);
        }

        [Test]
        public void TryParse_RejectsANullReader()
        {
            Assert.That(LobbyMetadata.TryParse(null, out _, out var error), Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void TryParse_ToleratesAReaderReturningNullForAbsentKeys()
        {
            var published = LobbyMetadata.ForHost(_config, HostSteamId).ToDictionary();
            published.Remove(LobbyMetadata.LobbyNameKey);

            Assert.That(LobbyMetadata.TryParse(key => published.TryGetValue(key, out var v) ? v : null,
                out var parsed, out var error), Is.True, error);
            Assert.That(parsed.LobbyName, Is.Empty);
        }

        [Test]
        public void IsCompatibleWith_AcceptsAMatchingBuild()
        {
            var metadata = LobbyMetadata.ForHost(_config, HostSteamId);

            Assert.That(metadata.IsCompatibleWith(_config, out var reason), Is.True, reason);
        }

        [Test]
        public void IsCompatibleWith_RejectsADifferentSignature()
        {
            var metadata = LobbyMetadata.ForHost(_config, HostSteamId);
            var otherGame = new MultiplayerSessionConfig
            {
                GameSignature = "someone.else",
                ProtocolVersion = 3,
            };

            Assert.That(metadata.IsCompatibleWith(otherGame, out var reason), Is.False);
            Assert.That(reason, Does.Contain("someone.else"));
        }

        [Test]
        public void IsCompatibleWith_RejectsAnOlderProtocolVersion()
        {
            var metadata = LobbyMetadata.ForHost(_config, HostSteamId);
            var oldBuild = new MultiplayerSessionConfig
            {
                GameSignature = "sombra.testgame",
                ProtocolVersion = 2,
            };

            Assert.That(metadata.IsCompatibleWith(oldBuild, out var reason), Is.False);
            Assert.That(reason, Does.Contain("protocol"));
        }

        [Test]
        public void IsCompatibleWith_IsCaseSensitiveOnTheSignature()
        {
            var metadata = LobbyMetadata.ForHost(_config, HostSteamId);
            var differentCasing = new MultiplayerSessionConfig
            {
                GameSignature = "Sombra.TestGame",
                ProtocolVersion = 3,
            };

            Assert.That(metadata.IsCompatibleWith(differentCasing, out _), Is.False);
        }

        private static System.Func<string, string> Reader(IDictionary<string, string> source)
        {
            return key => source.TryGetValue(key, out var value) ? value : string.Empty;
        }
    }
}
