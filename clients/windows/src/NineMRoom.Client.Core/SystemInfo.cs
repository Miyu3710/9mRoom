namespace NineMRoom.Client.Core;

public static class SystemInfo
{
    public static TimeSpan IdleTime() => Native.IdleTime();

    public static long BootTimeMs() => Native.SystemBootTimeMs();
}
