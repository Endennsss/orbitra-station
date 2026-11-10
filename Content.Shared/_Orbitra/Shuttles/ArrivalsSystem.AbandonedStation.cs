// ReSharper disable CheckNamespace
namespace Content.Shared.Shuttles.Systems;

public abstract partial class ArrivalsSystem
{
    /// <summary>
    /// Whether the communications outage blocks arrivals on the server.
    /// </summary>
    public virtual bool OrbitraArrivalsSuspended => false;
}
