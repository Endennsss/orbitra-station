using Robust.Shared.Configuration;

namespace Content.Shared._Orbitra.VoiceChat;

[CVarDefs]
public static class OrbitraVoiceChatCVars
{
    public static readonly CVarDef<string> InputDevice =
        CVarDef.Create("orbitra.voice_input_device", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> InputVolume =
        CVarDef.Create("orbitra.voice_input_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> VoiceVolume =
        CVarDef.Create("orbitra.voice_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> VoiceEnabled =
        CVarDef.Create("orbitra.voice_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
