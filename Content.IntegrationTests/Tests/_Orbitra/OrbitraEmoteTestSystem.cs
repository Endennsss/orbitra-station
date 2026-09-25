using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Orbitra;

/// <summary>Counts actual emote events from the target selected by a regression test.</summary>
public sealed class OrbitraEmoteTestSystem : EntitySystem
{
    public NetEntity? Target;
    public int EmoteCount;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpeechComponent, EmoteEvent>(OnEmote);
    }

    private void OnEmote(Entity<SpeechComponent> ent, ref EmoteEvent args)
    {
        if (args.Source == Target)
            EmoteCount++;
    }
}
