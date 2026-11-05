using FluentAssertions;
using Microsoft.Extensions.Logging;
using MilkyWare.Wam.Cli.Services;
using NSubstitute;
using Xunit;
using MsalLogLevel = Microsoft.Identity.Client.LogLevel;

namespace MilkyWare.Wam.Cli.Services.Tests
{
    public sealed class MsalTokenServiceTests
    {
        private readonly ITestContextAccessor _contextAccessor;
        private readonly IBrokerPlatform _brokerPlatform;
        private readonly ILogger<MsalTokenService> _logger;
        private readonly MsalTokenService _sut;

        public MsalTokenServiceTests(ITestContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _brokerPlatform = Substitute.For<IBrokerPlatform>();
            _logger = Substitute.For<ILogger<MsalTokenService>>();
            _sut = new MsalTokenService(_logger, _brokerPlatform);
        }

        private CancellationToken CancellationToken => _contextAccessor.Current.CancellationToken;

        [Theory]
        [InlineData(MsalLogLevel.Warning, "wam_no_account_for_id")]
        [InlineData(MsalLogLevel.Error, "AccountNotFound")]
        [InlineData(MsalLogLevel.Error, "trynormalizeRealm")]
        [InlineData(MsalLogLevel.Error, "CalculateSuggestedCacheExpiry")]
        public void LogMsal_WhenMessageIsBenign_ShouldLogSingleDebugEntry(MsalLogLevel level, string message)
        {
            // Arrange
            var cancellationToken = CancellationToken;

            // Act
            _sut.LogMsal(level, message, containsPii: false);

            // Assert
            cancellationToken.Should().NotBe(CancellationToken.None);
            LoggedLevels().Should().ContainSingle().Which.Should().Be(LogLevel.Debug);
        }

        [Theory]
        [InlineData(MsalLogLevel.Error, "An unexpected error occurred while writing the cache")]
        [InlineData(MsalLogLevel.Warning, "Broker could not start the authentication window")]
        [InlineData(MsalLogLevel.Info, "Token acquisition started")]
        public void LogMsal_WhenMessageIsNotBenign_ShouldLogSingleEntryAtMappedLevel(MsalLogLevel level, string message)
        {
            // Arrange
            var cancellationToken = CancellationToken;
            var expectedLevel = level switch
            {
                MsalLogLevel.Error => LogLevel.Error,
                MsalLogLevel.Warning => LogLevel.Warning,
                MsalLogLevel.Info => LogLevel.Information,
                _ => LogLevel.Debug,
            };

            // Act
            _sut.LogMsal(level, message, containsPii: false);

            // Assert
            cancellationToken.Should().NotBe(CancellationToken.None);
            LoggedLevels().Should().ContainSingle().Which.Should().Be(expectedLevel);
        }

        [Fact]
        public void LogMsal_WhenMessageContainsPii_ShouldNotLog()
        {
            // Arrange
            var cancellationToken = CancellationToken;

            // Act
            _sut.LogMsal(MsalLogLevel.Error, "Account with id 'user@contoso.com' not found", containsPii: true);

            // Assert
            cancellationToken.Should().NotBe(CancellationToken.None);
            LoggedLevels().Should().BeEmpty();
        }

        private IEnumerable<LogLevel> LoggedLevels()
        {
            return _logger.ReceivedCalls()
                .Select(call => (LogLevel)call.GetArguments()[0]!);
        }
    }
}
