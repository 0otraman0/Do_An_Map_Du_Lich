using Android.OS;
using Java.IO;
using MauiAppMain.Services;
public class DeviceInfoService : IDeviceInfoService
{
    public (long total, long free) GetStorageInfo()
    {
        var path = Android.OS.Environment.DataDirectory.AbsolutePath;
        var stat = new StatFs(path);

        long blockSize = stat.BlockSizeLong;
        long totalBlocks = stat.BlockCountLong;
        long availableBlocks = stat.AvailableBlocksLong;

        long totalBytes = totalBlocks * blockSize;
        long freeBytes = availableBlocks * blockSize;

        return (totalBytes, freeBytes);
    }
}