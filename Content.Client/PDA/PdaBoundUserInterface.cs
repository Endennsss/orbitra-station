using Content.Client.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.PDA;
using JetBrains.Annotations;
using Robust.Client.GameObjects; // Orbitra-Edit
using Robust.Client.UserInterface;

namespace Content.Client.PDA
{
    [UsedImplicitly]
    public sealed partial class PdaBoundUserInterface : CartridgeLoaderBoundUserInterface // Orbitra-Edit - личные данные банковского приложения
    {
        private readonly PdaSystem _pdaSystem;

        [ViewVariables]
        private PdaMenu? _menu;

        public PdaBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
            _pdaSystem = EntMan.System<PdaSystem>();
        }

        protected override void Open()
        {
            base.Open();

            if (_menu == null)
                CreateMenu();
        }

        private void CreateMenu()
        {
            _menu = this.CreateWindowCenteredLeft<PdaMenu>();

            _menu.FlashLightToggleButton.OnToggled += _ =>
            {
                SendMessage(new PdaToggleFlashlightMessage());
            };

            _menu.EjectIdButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaIdSlotId));
            };

            _menu.EjectPenButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPenSlotId));
            };

            _menu.EjectPaiButton.OnPressed += _ =>
            {
                SendPredictedMessage(new ItemSlotButtonPressedEvent(PdaComponent.PdaPaiSlotId));
            };

            _menu.ActivateMusicButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowMusicMessage());
            };

            _menu.AccessRingtoneButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowRingtoneMessage());
            };

            _menu.ShowUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaShowUplinkMessage());
            };

            _menu.LockUplinkButton.OnPressed += _ =>
            {
                SendMessage(new PdaLockUplinkMessage());
            };

            _menu.OnProgramItemPressed += ActivateCartridge;
            _menu.OnInstallButtonPressed += InstallCartridge;
            _menu.OnUninstallButtonPressed += UninstallCartridge;
            _menu.ProgramCloseButton.OnPressed += _ => DeactivateActiveCartridge();

            // Orbitra edit start - полосы берём с корпуса открытого КПК.
            _menu.BorderColor = GetBorderColorComponent()?.BorderColor;
            var sprites = EntMan.System<SpriteSystem>();
            if (sprites.TryGetLayer(Owner, PdaVisualizerSystem.PdaVisualLayers.Base, out var layer, logMissing: false))
                _menu.CaseTrim.SetSprite(layer);
            // Orbitra edit end
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not PdaUpdateState updateState)
                return;

            if (_menu == null)
            {
                _pdaSystem.Log.Error("PDA state received before menu was created.");
                return;
            }

            _menu.UpdateState(updateState);
        }

        protected override void AttachCartridgeUI(Control cartridgeUIFragment, string? title)
        {
            _menu?.ProgramView.AddChild(cartridgeUIFragment);
            _menu?.ToProgramView(title ?? Loc.GetString("comp-pda-io-program-fallback-title"));
            RequestBankSnapshot(cartridgeUIFragment); // Orbitra-Edit
        }

        protected override void DetachCartridgeUI(Control cartridgeUIFragment)
        {
            if (_menu is null)
                return;

            _menu.HideProgramHeader();
            _menu.ProgramView.RemoveChild(cartridgeUIFragment);
        }

        protected override void UpdateAvailablePrograms(List<(EntityUid, CartridgeComponent)> programs)
        {
            _menu?.UpdateAvailablePrograms(programs);
        }

        private PdaBorderColorComponent? GetBorderColorComponent()
        {
            return EntMan.GetComponentOrNull<PdaBorderColorComponent>(Owner);
        }
    }
}
