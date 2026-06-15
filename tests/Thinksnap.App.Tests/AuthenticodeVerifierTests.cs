using Thinksnap.App.Services;
using Thinksnap.App.Models;
using Xunit;

namespace Thinksnap.App.Tests;

public sealed class AuthenticodeVerifierTests
{
    [Fact]
    public void UnsignedPortableExecutableIsReportedWithoutCrashing()
    {
        var result = AuthenticodeVerifier.Verify(typeof(AuthenticodeVerifierTests).Assembly.Location);

        Assert.Equal(UpdateSignatureStatus.Unsigned, result.Status);
        Assert.Null(result.Signer);
        Assert.Null(result.Error);
    }
}
