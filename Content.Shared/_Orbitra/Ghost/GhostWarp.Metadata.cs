using Content.Shared.Roles;
using System.Runtime.InteropServices;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ghost.Systems;

// Сетевой сериализатор работает с полями, а не с их расположением в памяти между partial-файлами.
[StructLayout(LayoutKind.Auto)]
public partial struct GhostWarp
{
    /// <summary>Имя персонажа без форматирования, отдельно от локализованной подписи.</summary>
    public string? CharacterName { get; init; }

    /// <summary>Должность из состояния разума для определения иконки и отдела вне клиентского PVS.</summary>
    public ProtoId<JobPrototype>? Job { get; init; }

    /// <summary>Статус антагониста с сервера только для разрешённого ответа меню телепортации призрака.</summary>
    public bool IsAntagonist { get; init; }

    /// <summary>Признак того, что цель является наблюдателем.</summary>
    public bool IsGhost { get; init; }
}
