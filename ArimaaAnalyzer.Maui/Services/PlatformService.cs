public class PlatformService : IPlatformService
{
    public bool IsWindows => DeviceInfo.Platform == DevicePlatform.Windows;
}