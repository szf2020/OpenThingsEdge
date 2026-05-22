using ThingsEdge.Exchange.Contracts.Variables;

namespace ThingsEdge.Exchange.Addresses;

internal sealed class DefaultAddressFactory(IAddressProvider deviceSource, IMemoryCache cache) : IAddressFactory
{
    public const string CacheName = "__ThingsEdge.Device.Cache";

    public IReadOnlyCollection<Channel> GetChannels()
    {
        return cache.GetOrCreate(CacheName, _ => deviceSource.GetChannels()) ?? [];
    }

    public void Refresh()
    {
        cache.Remove(CacheName);
    }
}
