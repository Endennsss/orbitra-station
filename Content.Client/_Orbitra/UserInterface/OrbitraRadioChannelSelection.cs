namespace Content.Client._Orbitra.UserInterface;

/// <summary>
/// Resolves the channel used by the separate radio push-to-talk input.
/// </summary>
internal static class OrbitraRadioChannelSelection
{
    public static string? Resolve(string? explicitChannel, string? defaultChannel, bool radioSelected)
    {
        if (!radioSelected)
            return null;

        return string.IsNullOrWhiteSpace(explicitChannel)
            ? defaultChannel
            : explicitChannel;
    }
}
