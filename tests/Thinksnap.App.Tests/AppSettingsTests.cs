using Thinksnap.App.Models;
using Xunit;

namespace Thinksnap.App.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void NewSettingsUseWhiteTextColorByDefault()
    {
        var settings = new AppSettings();

        Assert.Equal("#ffffff", settings.DefaultTextColor);
    }
}
