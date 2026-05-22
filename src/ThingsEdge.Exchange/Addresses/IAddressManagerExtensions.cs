using ThingsEdge.Exchange.Contracts.Variables;

namespace ThingsEdge.Exchange.Addresses;

public static class IAddressManagerExtensions
{
    extension(IAddressFactory addressFactory)
    {
        /// <summary>
        /// 重新加载数据，会清空缓存重新加载数据。
        /// </summary>
        /// <returns></returns>
        public IReadOnlyCollection<Device> ReloadAddress()
        {
            addressFactory.Refresh();
            return addressFactory.GetDevices();
        }

        /// <summary>
        /// 获取所有的设备。
        /// </summary>
        /// <param name="channelName">通道名称</param>
        /// <returns></returns>
        public IReadOnlyCollection<Device> GetDevices(string? channelName = null)
        {
            var channels = addressFactory.GetChannels();
            if (channelName != null)
            {
                return channels.FirstOrDefault(s => s.Name == channelName)?.Devices ?? [];
            }

            return [.. channels.SelectMany(s => s.Devices)];
        }

        /// <summary>
        /// 获取指定通道下指定名称的设备。
        /// </summary>
        /// <param name="channelName">通道名称</param>
        /// <param name="deviceName">设备名称</param>
        /// <returns></returns>
        public Device? GetDevice(string channelName, string deviceName)
        {
            var channels = addressFactory.GetChannels();
            var channel = channels.FirstOrDefault(s => s.Name == channelName);
            if (channel == null)
            {
                return default;
            }

            return channel.Devices.FirstOrDefault(s => s.Name == deviceName);
        }

        /// <summary>
        /// 获取指定的设备。
        /// </summary>
        /// <param name="deviceId">设备Id。</param>
        /// <returns></returns>
        public Device? GetDevice(string deviceId)
        {
            var devices = addressFactory.GetDevices();
            return devices.FirstOrDefault(s => s.DeviceId == deviceId);
        }

        /// <summary>
        /// 获取指定的设备。
        /// </summary>
        /// <param name="deviceId">设备Id。</param>
        /// <returns></returns>
        public (string? channelName, Device? device) GetDevice2(string deviceId)
        {
            var channels = addressFactory.GetChannels();
            foreach (var channel in channels)
            {
                var device = channel.Devices.FirstOrDefault(s => s.DeviceId == deviceId);
                if (device != null)
                {
                    return (channel.Name, device);
                }
            }

            return (default, default);
        }
    }
}
