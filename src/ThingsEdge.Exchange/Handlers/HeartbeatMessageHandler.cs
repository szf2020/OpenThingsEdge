using ThingsEdge.Exchange.Forwarders;
using ThingsEdge.Exchange.Messages;
using ThingsEdge.Exchange.Snapshot;

namespace ThingsEdge.Exchange.Handlers;

/// <summary>
/// 心跳消息处理器。
/// </summary>
internal sealed class HeartbeatMessageHandler(
    IHeartbeatForwarderProxy forwarderProxy,
    ITagDataSnapshot tagDataSnapshot) : IMessageHandler<HeartbeatMessage>
{
    public async Task HandleAsync(HeartbeatMessage message, CancellationToken cancellationToken = default)
    {
        // 设置标记值快照。
        tagDataSnapshot.Change(message.Self);

        // 若是信号值，则中断处理。
        if (message.IsOnlySign)
        {
            return;
        }

        await forwarderProxy.ChangeAsync(new(message.ChannelName, message.Device, message.Tag, message.IsConnected), cancellationToken).ConfigureAwait(false);
    }
}
