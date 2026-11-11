namespace MediaTool.Core.Metadata;

/// <summary>Problems the user can be told about in plain language.</summary>
public enum MediaError
{
    None,
    InvalidLink,
    UnsupportedSite,
    UnsupportedLink,
    PrivateVideo,
    Removed,
    LoginRequired,
    NoVideo,
    LiveStream,
    Network,
    ToolMissing,
    Unknown,
}
