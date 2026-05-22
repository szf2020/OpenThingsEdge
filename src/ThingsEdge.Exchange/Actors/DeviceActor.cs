using Proto;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Infrastructure.Actors;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 设备 Actor
/// </summary>
internal sealed class DeviceActor(string channelName, Device device) : IActor
{
    private IDriverConnector? _connector;

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case DeviceStartMessage:
                var actor = context.SpawnFor<ConnectorActor>([device]);
                // 连接设备
                _connector = await context.RequestAsync<IDriverConnector>(actor, new ConnectorCreateAndConnectMessage()).ConfigureAwait(false);

                // 心跳点位监控
                foreach (var tag in device.GetAllSignalTags(TagFlag.Heartbeat))
                {
                    context.SpawnFor<HearbeatTagActor>([channelName, device, tag, _connector]);
                }

                // 通知点位监控
                foreach (var tag in device.GetAllSignalTags(TagFlag.Notice))
                {
                    context.SpawnFor<NoticeTagActor>([channelName, device, tag, _connector]);
                }

                // 触发点位监控
                foreach (var tag in device.GetAllSignalTags(TagFlag.Trigger))
                {
                    context.SpawnFor<TriggerTagActor>([channelName, device, tag, _connector]);
                }

                break;
        }
    }
}

/// <summary>
/// 设备开始运行消息。
/// </summary>
public sealed record DeviceStartMessage;
