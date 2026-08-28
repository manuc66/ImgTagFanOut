using ImgTagFanOut.Models;

namespace ImgTagFanOut.Tests;

public class ErrorTrackingPolicyTests
{
    [Fact]
    public void ShouldEnable_WhenNullAndNotYetConsented_ReturnsFalse()
    {
        Assert.False(ErrorTrackingPolicy.ShouldEnable(null));
    }

    [Fact]
    public void ShouldEnable_WhenExplicitlyAllowed_ReturnsTrue()
    {
        Assert.True(ErrorTrackingPolicy.ShouldEnable(true));
    }

    [Fact]
    public void ShouldEnable_WhenExplicitlyDeclined_ReturnsFalse()
    {
        Assert.False(ErrorTrackingPolicy.ShouldEnable(false));
    }
}