using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Content.Shared.Radio;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController
{
    private static readonly string[] OrbitraStationRadioChannelOrder =
    {
        "Common", "Command", "Engineering", "Medical", "Science", "Security", "Service", "Supply"
    };

    private RadioChannelPrototype? _orbitraSelectedRadioChannel;
    private bool _orbitraManualRadioChannel;

    /// <summary>
    /// Радиоканал, выбранный в обычном поле чата и используемый радио-PTT.
    /// </summary>
    public RadioChannelPrototype? SelectedRadioChannel => _orbitraSelectedRadioChannel;

    public event Action<RadioChannelPrototype?>? SelectedRadioChannelChanged;

    public RadioChannelPrototype? ResolveSelectedRadioChannel()
    {
        ChatBox? box = UIManager.ActiveScreen?.GetWidget<ChatBox>();
        box ??= UIManager.ActiveScreen?.GetWidget<ResizableChatBox>();
        if (box == null)
            return null;

        var (_, _, explicitChannel) = SplitInputContents(box.ChatInput.Input.Text.ToLowerInvariant());
        if (explicitChannel != null)
            return explicitChannel;

        if (box.SelectedChannel != ChatSelectChannel.Radio)
            return null;

        if (_orbitraManualRadioChannel && _orbitraSelectedRadioChannel != null)
            return _orbitraSelectedRadioChannel;

        var defaultEvent = new GetDefaultRadioChannelEvent();
        if (_player.LocalEntity is EntityUid entity && entity.Valid)
            _ent.EventBus.RaiseLocalEvent(entity, defaultEvent);

        if (defaultEvent.Channel != null && _prototypeManager.TryIndex<RadioChannelPrototype>(defaultEvent.Channel, out var channel))
            return channel;

        return null;
    }

    public void SetSelectedRadioChannel(RadioChannelPrototype channel)
    {
        _orbitraSelectedRadioChannel = channel;
        _orbitraManualRadioChannel = true;
        SelectedRadioChannelChanged?.Invoke(channel);
    }

    public IReadOnlyList<RadioChannelPrototype> GetAvailableRadioChannels()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (_player.LocalEntity is not { Valid: true } entity)
            return Array.Empty<RadioChannelPrototype>();

        if (_ent.TryGetComponent(entity, out ActiveRadioComponent? activeRadio))
        {
            foreach (var channel in activeRadio.Channels)
                ids.Add(channel);
        }

        // Серверная маршрутизация проверяет объединённые каналы ключей на
        // EncryptionKeyHolderComponent. ActiveRadioComponent может обновиться
        // на клиенте позже или содержать только часть состояния гарнитуры.
        if (_ent.TryGetComponent(entity, out EncryptionKeyHolderComponent? keyHolder))
            AddEncryptionChannels(ids, keyHolder);

        if (_ent.TryGetComponent(entity, out IntrinsicRadioTransmitterComponent? intrinsicRadio))
        {
            foreach (var channel in intrinsicRadio.Channels)
                ids.Add(channel);
        }

        if (_ent.TryGetComponent(entity, out InventoryComponent? inventory))
        {
            var inventorySystem = _ent.System<InventorySystem>();
            var slots = inventorySystem.GetSlotEnumerator((entity, inventory));
            while (slots.NextItem(out var item))
            {
                if (_ent.TryGetComponent(item, out ActiveRadioComponent? itemRadio))
                {
                    foreach (var channel in itemRadio.Channels)
                        ids.Add(channel);
                }

                if (_ent.TryGetComponent(item, out EncryptionKeyHolderComponent? itemKeyHolder))
                    AddEncryptionChannels(ids, itemKeyHolder);

                if (_ent.TryGetComponent(item, out IntrinsicRadioTransmitterComponent? itemTransmitter))
                {
                    foreach (var channel in itemTransmitter.Channels)
                        ids.Add(channel);
                }
            }
        }

        var defaultEvent = new GetDefaultRadioChannelEvent();
        _ent.EventBus.RaiseLocalEvent(entity, defaultEvent);
        if (defaultEvent.Channel != null)
            ids.Add(defaultEvent.Channel);

        return _prototypeManager.EnumeratePrototypes<RadioChannelPrototype>()
            .Where(channel => ids.Contains(channel.ID))
            .OrderBy(channel => channel.ID, StringComparer.Ordinal)
            .ToList();
    }

    private void AddEncryptionChannels(HashSet<string> ids, EncryptionKeyHolderComponent holder)
    {
        foreach (var channel in holder.Channels)
            ids.Add(channel);

        // На клиенте Channels может ещё не содержать результат пересчёта
        // после синхронизации содержимого гарнитуры. Повторяем штатный
        // пересчёт по ключам из key_slots, чтобы не терять второй и следующие
        // ключи, установленные в одну гарнитуру.
        if (!holder.Initialized)
            return;

        foreach (var keyEntity in holder.KeyContainer.ContainedEntities)
        {
            if (!_ent.TryGetComponent(keyEntity, out EncryptionKeyComponent? key))
                continue;

            foreach (var channel in key.Channels)
                ids.Add(channel);
        }
    }

    public IReadOnlyList<OrbitraRadioChannelOption> GetRadioChannelOptions()
    {
        var available = new HashSet<string>(StringComparer.Ordinal);
        foreach (var channel in GetAvailableRadioChannels())
            available.Add(channel.ID);

        var result = new List<OrbitraRadioChannelOption>();
        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (var channelId in OrbitraStationRadioChannelOrder)
        {
            if (!_prototypeManager.TryIndex<RadioChannelPrototype>(channelId, out var channel))
                continue;

            result.Add(new OrbitraRadioChannelOption(channel, available.Contains(channel.ID)));
            added.Add(channel.ID);
        }

        // Особые каналы (например, синдикатный) остаются доступны тем, у кого есть нужный ключ.
        foreach (var channel in GetAvailableRadioChannels())
        {
            if (added.Add(channel.ID))
                result.Add(new OrbitraRadioChannelOption(channel, true));
        }

        return result;
    }

    private void OrbitraUpdateSelectedRadioChannel(ChatBox box, ChatSelectChannel prefixChannel, RadioChannelPrototype? explicitChannel)
    {
        if (box.SelectedChannel != ChatSelectChannel.Radio && prefixChannel != ChatSelectChannel.Radio)
        {
            if (_orbitraSelectedRadioChannel != null)
            {
                _orbitraSelectedRadioChannel = null;
                _orbitraManualRadioChannel = false;
                SelectedRadioChannelChanged?.Invoke(null);
            }

            return;
        }

        var channel = explicitChannel;
        if (channel != null)
            _orbitraManualRadioChannel = false;

        if (channel == null && _orbitraManualRadioChannel && _orbitraSelectedRadioChannel != null)
            return;
        if (channel == null && box.SelectedChannel == ChatSelectChannel.Radio)
        {
            var defaultEvent = new GetDefaultRadioChannelEvent();
            if (_player.LocalEntity is EntityUid entity && entity.Valid)
                _ent.EventBus.RaiseLocalEvent(entity, defaultEvent);

            if (defaultEvent.Channel != null)
                _prototypeManager.TryIndex(defaultEvent.Channel, out channel);
        }

        if (ReferenceEquals(_orbitraSelectedRadioChannel, channel) ||
            string.Equals(_orbitraSelectedRadioChannel?.ID, channel?.ID, StringComparison.Ordinal))
        {
            return;
        }

        _orbitraSelectedRadioChannel = channel;
        SelectedRadioChannelChanged?.Invoke(channel);
    }
}

public readonly record struct OrbitraRadioChannelOption(RadioChannelPrototype Channel, bool Available);
