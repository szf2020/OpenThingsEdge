using Proto;
using ThingsEdge.Exchange.Contracts.Variables;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 执行引擎 Actor。
/// </summary>
internal sealed class ChannelActor(Channel channel) : IActor
{
    private readonly Dictionary<string, PID> _actors = [];

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case ChannelStartMessage:
                foreach (var device in channel.Devices)
                {
                    var actor = StartDevcie(channel.Name, device);

                    // 添加到集合
                    _actors.Add(device.DeviceId, actor);
                }

                break;

            case ChannelStopDeviceMessage msg:
                if (_actors.TryGetValue(msg.DeviceId, out var pid))
                {
                    await context.StopAsync(pid).ConfigureAwait(false);
                    _actors.Remove(msg.DeviceId);
                }

                break;

            case ChannelStartDeviceMessage msg:
                if (!_actors.ContainsKey(msg.DeviceId))
                {
                    var device = channel.Devices.FirstOrDefault(s => s.DeviceId == msg.DeviceId);
                    if (device != null)
                    {
                        var actor = StartDevcie(channel.Name, device);

                        // 添加到集合
                        _actors.Add(device.DeviceId, actor);
                    }
                }

                break;
        }

        // 启动设备
        PID StartDevcie(string channelName, Device device)
        {
            var actor = context.SpawnNamed(
                Props.FromProducer(() => new DeviceActor(channel.Name, device)),
                device.DeviceId);
            context.Send(actor, new DeviceStartMessage());

            return actor;
        }
    }
}

/// <summary>
/// 引擎启动消息。
/// </summary>
public sealed record ChannelStartMessage();

/// <summary>
/// 停止指定的设备运行。
/// </summary>
/// <param name="DeviceId">要停止的设备 Id</param>
public sealed record ChannelStopDeviceMessage(string DeviceId);

/// <summary>
/// 启动指定的设备。
/// </summary>
/// <param name="DeviceId">要启动的设备 Id</param>
public sealed record ChannelStartDeviceMessage(string DeviceId);
