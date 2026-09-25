using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Mind;
using Robust.Shared.Utility;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarRuleSystem
{
    /// <summary>Sends thought without speech, radio devices, local listeners or public replay broadcast.</summary>
    public bool TrySendHiveMessage(EntityUid actor, string? text)
    {
        if (!CanSendHiveMessage(actor, text, out var rule, out var role)) return false;
        text = text!.Trim();
        role.NextMessage = Timing.CurTime + rule.Comp.MessageCooldown;
        var message = Loc.GetString("orbitra-ratvar-communication", ("name", Name(actor)), ("message", text));
        var wrapped = FormattedMessage.EscapeText(message);
        foreach (var mind in rule.Comp.Members)
        {
            if (!_roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var member) || member.Value.Comp2.Rule != rule.Owner ||
                !TryComp<MindComponent>(mind, out var data) || data.UserId is not { } user ||
                !_players.TryGetSessionById(user, out var session)) continue;
            _chat.ChatMessageToOne(ChatChannel.Radio, message, wrapped, default, false, session.Channel,
                colorOverride: Color.FromHex("#C9A65C"));
        }
        _adminLog.Add(LogType.Chat, LogImpact.Low, $"Ratvar collective mind {ToPrettyString(rule)}: {ToPrettyString(actor)}: {text}");
        return true;
    }

    /// <summary>Checks living controlled membership and a mind-bound cooldown without modifying state.</summary>
    public bool CanSendHiveMessage(EntityUid actor, string? text, out Entity<OrbitraRatvarRuleComponent> rule,
        out OrbitraRatvarRoleComponent role)
    {
        role = default!;
        if (!TryGetCult(actor, out rule) || !Living(actor) ||
            string.IsNullOrWhiteSpace(text) || text.Length > 300 || text.Contains('\n') || text.Contains('\r') ||
            !_mind.TryGetMind(actor, out var mind, out var data) || data.OwnedEntity != actor ||
            !_roles.MindHasRole<OrbitraRatvarRoleComponent>(mind, out var membership)) return false;
        role = membership.Value.Comp2;
        return role.NextMessage <= Timing.CurTime;
    }
}
