namespace MediaTool.Core.Metadata;

/// <summary>yt-dlp's output could not be read as video metadata.</summary>
public sealed class MetadataParseException(string message, Exception? inner = null) : Exception(message, inner);
