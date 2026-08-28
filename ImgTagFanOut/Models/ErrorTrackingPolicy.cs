namespace ImgTagFanOut.Models;

public static class ErrorTrackingPolicy
{
    public static bool ShouldEnable(bool? errorTrackingAllowed)
    {
        return errorTrackingAllowed == true;
    }
}