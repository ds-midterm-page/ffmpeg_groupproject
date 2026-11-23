namespace MediaTool.Core.Download;

public static class AudioQuality
{
    /// <summary>MP3 bitrates offered for audio downloads, best first.</summary>
    public static readonly IReadOnlyList<int> Mp3BitratesKbps = [320, 192, 128];

    public static bool IsOfferedMp3Bitrate(int kbps) => Mp3BitratesKbps.Contains(kbps);
}
