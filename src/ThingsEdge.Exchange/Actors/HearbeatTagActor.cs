using Proto;
using ThingsEdge.Exchange.Configuration;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Infrastructure.Actors;
using ThingsEdge.Exchange.Messages;
using ThingsEdge.Exchange.Utils;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 监控心跳 Actor。
/// </summary>
internal sealed class HearbeatTagActor(
    string channelName,
    Device device,
    SignalTag signalTag,
    IDriverConnector connector,
    IOptions<ExchangeOptions> options,
    ILogger<HearbeatTagActor> logger) : IActor
{
    private int _onlineState = -1; // 在线状态，-1 为初始状态，0 表示断开，1 表示连接
    private readonly CancellationTokenSource _cts = new();

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                context.Send(context.Self, new HearbeatTagPollTickMessage());

                break;

            case HearbeatTagPollTickMessage:
                if (_cts.IsCancellationRequested)
                {
                    context.Publish(new HeartbeatMessage(
                        channelName,
                        device,
                        signalTag,
                        WorkerHelper.CreateHeartbeatPayloadOff(signalTag, options.Value.HeartbeatListenUseHighLevel),
                        false,
                        false));
                    return;
                }

                if (!connector.CanConnect)
                {
                    _onlineState = 0;

                    MoveNext();
                    return;
                }

                var (ok, data, err) = await connector.ReadAsync(signalTag).ConfigureAwait(false);
                if (!ok)
                {
                    logger.LogError("[HeartbeatActor] Heartbeat 数据读取异常，设备：{DeviceName}，标记：{TagName}, 地址：{TagAddress}，错误：{Err}",
                        device.Name, signalTag.Name, signalTag.Address, err);

                    MoveNext();
                    return;
                }

                // 心跳标记数据类型必须为 bool 或 int16
                if (WorkerHelper.CheckHeartbeatOn(data!, options.Value.HeartbeatListenUseHighLevel))
                {
                    if (options.Value.HeartbeatShouldAckZero)
                    {
                        // 数据回写失败不影响，下一次轮询继续处理
                        await connector.WriteAsync(signalTag, WorkerHelper.SetHeartbeatOff(signalTag, options.Value.HeartbeatListenUseHighLevel)).ConfigureAwait(false);
                    }

                    // 发布心跳正常事件（初始连接）。
                    if (_onlineState == -1)
                    {
                        context.Publish(new HeartbeatMessage(channelName, device, signalTag, data!, true, false));
                    }

                    _onlineState = 1;
                }

                // 发布心跳信号事件（仅记录值）。
                context.Publish(new HeartbeatMessage(channelName, device, signalTag, data!, true, true));

                // 执行下一次任务
                MoveNext();

                break;

            case Stopping:
                _cts.Cancel();

                break;
        }

        // 执行下一次操作
        void MoveNext()
        {
            // 下一次执行任务
            if (!_cts.IsCancellationRequested)
            {
                var delayMilliseconds = signalTag.ScanRate > 0 ? signalTag.ScanRate : options.Value.DefaultScanRate;
                context.ReenterAfter(
                    Task.Delay(delayMilliseconds, _cts.Token),
                    task =>
                    {
                        if (task.IsCompletedSuccessfully && !_cts.IsCancellationRequested)
                        {
                            context.Send(context.Self, new HearbeatTagPollTickMessage());
                        }
                    });
            }
        }
    }
}

/// <summary>
/// Hearbeat 节点轮询消息。
/// </summary>
public sealed record HearbeatTagPollTickMessage;
