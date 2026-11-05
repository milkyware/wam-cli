using FluentAssertions;
using MilkyWare.Wam.Cli.Services;
using Xunit;

namespace MilkyWare.Wam.Cli.Services.Tests
{
    public sealed class HostOsTests
    {
        private readonly ITestContextAccessor _contextAccessor;
        private readonly HostOs _sut;

        public HostOsTests(ITestContextAccessor contextAccessor)
        {
            _contextAccessor = contextAccessor;
            _sut = new HostOs();
        }

        private CancellationToken CancellationToken => _contextAccessor.Current.CancellationToken;

        [Fact]
        public void Current_ShouldReturnDefinedPlatform()
        {
            // Arrange
            var cancellationToken = CancellationToken;

            // Act
            var platform = _sut.Current;

            // Assert
            cancellationToken.Should().NotBe(CancellationToken.None);
            Enum.IsDefined(platform).Should().BeTrue();
        }
    }
}
