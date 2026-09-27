using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

[Serializable, NetSerializable]
public enum OrbitraRatvarCacheUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class OrbitraRatvarCacheMessage(int choice) : BoundUserInterfaceMessage
{
    public readonly int Choice = choice;
}

[Serializable, NetSerializable]
public sealed class OrbitraRatvarCacheUiState(string reason, int seconds, string[] choices) : BoundUserInterfaceState
{
    public readonly string Reason = reason;
    public readonly int Seconds = seconds;
    public readonly string[] Choices = choices;
}
