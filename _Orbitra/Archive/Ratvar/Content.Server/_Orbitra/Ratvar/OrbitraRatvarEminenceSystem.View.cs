using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Bed.Sleep;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Flash.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Player;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarEminenceSystem
{
    // Штатная модель PVS согласована пользователем; стены и свет остаются ограничениями рендера.
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private BlurryVisionSystem _blurryVision = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    private void InitializeView()
    {
        SubscribeLocalEvent<OrbitraRatvarEminenceAvatarComponent, GetBlurEvent>(OnViewBlur);
    }

    private void OnViewBlur(Entity<OrbitraRatvarEminenceAvatarComponent> ent, ref GetBlurEvent args)
    {
        args.Blur = 0;
        if (!_mind.TryGetMind(ent, out var mind, out _) ||
            !TryComp<OrbitraRatvarEminenceComponent>(mind, out var observer) || observer.ViewSession == null ||
            !TryComp<BlurryVisionComponent>(observer.Target, out var blur)) return;
        args.Blur = blur.Magnitude;
        args.CorrectionPower = blur.CorrectionPower;
    }

    private bool HasUsableView(EntityUid target) => TryComp<EyeComponent>(target, out var eye) &&
        eye.Target == null && eye.VisibilityMask == EyeComponent.DefaultVisibilityMask &&
        !HasComp<SleepingComponent>(target) &&
        !_statusEffects.HasEffectComp<FlashedStatusEffectComponent>(target) &&
        !(TryComp<EyeClosingComponent>(target, out var closing) && closing.EyesClosed);

    private void StartView(Entity<OrbitraRatvarEminenceComponent> observer)
    {
        if (observer.Comp.ObserverBody is not { } body || observer.Comp.Target is not { } target ||
            !HasComp<OrbitraRatvarEminenceAvatarComponent>(body) || !TryComp<ActorComponent>(body, out var actor)) return;
        observer.Comp.ViewSession = actor.PlayerSession;
        EnsureComp<OrbitraRatvarEminenceViewComponent>(body);
        EnsureComp<BlindableComponent>(body);
        _eye.SetDrawFov(body, true);
        _eye.SetDrawLight(body, true);
        _eye.SetVisibilityMask(body, EyeComponent.DefaultVisibilityMask);
        _eye.SetTarget(body, target);
        UpdateView(observer);
    }

    private void StopView(Entity<OrbitraRatvarEminenceComponent> observer)
    {
        // Actor мог уже исчезнуть, поэтому подписку снимаем с сохранённой сессии.
        if (observer.Comp.ViewSession is not { } session) return;
        if (observer.Comp.Target is { } target) _viewSubscriber.RemoveViewSubscriber(target, session);
        if (observer.Comp.ObserverBody is { } body && !TerminatingOrDeleted(body))
        {
            _eye.SetTarget(body, null);
            RemComp<OrbitraRatvarEminenceViewComponent>(body);
            RemComp<BlurryVisionComponent>(body);
        }
        observer.Comp.ViewSession = null;
    }

    private void UpdateView(Entity<OrbitraRatvarEminenceComponent> observer)
    {
        if (observer.Comp.ViewSession == null || observer.Comp.ObserverBody is not { } body || observer.Comp.Target is not { } target)
            return;
        // Не выдаём ночное зрение и не наследуем отключённые администратором FOV/свет.
        _eye.SetDrawFov(body, true);
        _eye.SetDrawLight(body, true);
        var magnitude = TryComp<BlurryVisionComponent>(target, out var targetBlur) ? targetBlur.Magnitude : 0;
        var correction = targetBlur?.CorrectionPower ?? BlurryVisionComponent.DefaultCorrectionPower;
        var current = TryComp<BlurryVisionComponent>(body, out var ownBlur) ? ownBlur.Magnitude : 0;
        if (magnitude != current || ownBlur != null && ownBlur.CorrectionPower != correction)
            _blurryVision.UpdateBlurMagnitude(body);
    }
}
