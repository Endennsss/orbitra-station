// ReSharper disable CheckNamespace
namespace Content.Shared.Shuttles.Systems;

public abstract partial class ArrivalsSystem
{
    /// <summary>
    /// Блокирует ли потеря связи прибытие на сервере.
    /// </summary>
    public virtual bool OrbitraArrivalsSuspended => false;
}
