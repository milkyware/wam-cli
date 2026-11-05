using FluentAssertions;
using Microsoft.Identity.Client;
using MilkyWare.Wam.Cli.Enums;
using MilkyWare.Wam.Cli.Services;
using NSubstitute;
using Xunit;

namespace MilkyWare.Wam.Cli.Services.Tests
{
    public sealed class BrokerPlatformTests
    {
        private readonly ITestContextAccessor _contextAccessor;
        private readonly IOperatingSystem _operatingSystem;
        private readonly BrokerPlatform _sut;

        public BrokerPlatformTests(ITestContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _operatingSystem = Substitute.For<IOperatingSystem>();
            _sut = new BrokerPlatform(_operatingSystem);
        }

        private CancellationToken CancellationToken => _contextAccessor.Current.CancellationToken;

        [Fact]
        public void BrokerOperatingSystem_WhenWindows_ShouldReturnWindows()
        {
            // Arrange
            _operatingSystem.Current.Returns(Platform.Windows);
            var cancellationToken = CancellationToken;

            // Act
            var brokerOperatingSystem = _sut.BrokerOperatingSystem;

            // Assert
            cancellationToken.Should().NotBe(CancellationToken.None);
            brokerOperatingSystem.Should().Be(BrokerOptions.OperatingSystems.Windows);
            _sut.ParentWindowFunc.Should().NotBeNull();
        }

        [Fact]
        public void BrokerOperatingSystem_WhenLinux_ShouldReturnLinuxAndProvideParentWindow()
        {
            // Arrange
            _operatingSystem.Current.Returns(Platform.Linux);
            var cancellationToken = CancellationToken;

            // Act
            var brokerOperatingSystem = _sut.BrokerOperatingSystem;
            var parentWindowFunc = _sut.ParentWindowFunc;

            // Assert
            cancellationToken.Should().NotBe(CancellationToken.None);
            brokerOperatingSystem.Should().Be(BrokerOptions.OperatingSystems.Linux);
            parentWindowFunc.Should().NotBeNull();
            parentWindowFunc.Invoking(func => func!()).Should().NotThrow();
        }

        [Theory]
        [InlineData(Platform.MacOS)]
        [InlineData(Platform.Unknown)]
        public void BrokerOperatingSystem_WhenUnsupportedPlatform_ShouldThrowPlatformNotSupported(Platform platform)
        {
            // Arrange
            _operatingSystem.Current.Returns(platform);
            var cancellationToken = CancellationToken;

            // Act
            var act = () => _sut.BrokerOperatingSystem;

            // Assert
            cancellationToken.Should().NotBe(CancellationToken.None);
            act.Should().Throw<PlatformNotSupportedException>();
        }
    }
}
