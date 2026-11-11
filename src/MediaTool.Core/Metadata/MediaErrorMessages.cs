namespace MediaTool.Core.Metadata;

/// <summary>Plain-language text shown to the user for each <see cref="MediaError"/>.</summary>
public static class MediaErrorMessages
{
    public static string For(MediaError error) => error switch
    {
        MediaError.None => string.Empty,
        MediaError.InvalidLink => "That doesn't look like a valid link. Copy the full address of a video and paste it here.",
        MediaError.UnsupportedSite => "This site isn't supported. Paste a link from YouTube, TikTok, Instagram or X (Twitter).",
        MediaError.UnsupportedLink => "This link doesn't point to a single video. Open the video itself and copy its address.",
        MediaError.PrivateVideo => "This video is private, so it can't be downloaded.",
        MediaError.Removed => "This video is no longer available. It may have been removed or deleted.",
        MediaError.LoginRequired => "This video needs you to be signed in. Add a cookies file in Settings and try again.",
        MediaError.NoVideo => "There's no video at this link.",
        MediaError.LiveStream => "Live streams can't be downloaded. Try again once the stream has ended.",
        MediaError.Network => "Couldn't reach the site. Check your internet connection and try again.",
        MediaError.ToolMissing => "The downloader component is missing. Please reinstall MediaTool.",
        MediaError.Unknown => "Something went wrong while reading this video. Please try again later.",
        _ => "Something went wrong. Please try again.",
    };
}
