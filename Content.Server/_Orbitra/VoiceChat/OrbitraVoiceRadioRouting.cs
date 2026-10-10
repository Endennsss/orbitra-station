using System.Collections.Generic;

namespace Content.Server._Orbitra.VoiceChat;

/// <summary>
/// Shared channel checks used by the server voice radio route.
/// </summary>
public static class OrbitraVoiceRadioRouting
{
    public static bool CanTransmit(string channelId, IEnumerable<string>? headsetChannels, IEnumerable<string>? intrinsicChannels)
    {
        foreach (var channel in headsetChannels ?? [])
        {
            if (channel == channelId)
                return true;
        }

        foreach (var channel in intrinsicChannels ?? [])
        {
            if (channel == channelId)
                return true;
        }

        return false;
    }

    public static bool CanReceive(string channelId, bool receiveAllChannels, IEnumerable<string> channels)
    {
        if (receiveAllChannels)
            return true;

        foreach (var channel in channels)
        {
            if (channel == channelId)
                return true;
        }

        return false;
    }
}
