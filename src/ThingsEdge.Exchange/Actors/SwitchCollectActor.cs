using Proto;
using ThingsEdge.Exchange.Configuration;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Infrastructure.Actors;
using ThingsEdge.Exchange.Messages;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 监控开关采集的 Actor。
/// </summary>
internal sealed class SwitchCollectActor(
    string channelName,
    Device device,
    SignalTag signalTag,
    IDriverConnector connector,
    IOptions<ExchangeOptions> options) : IActor
{
    private bool _stopping;
    private readonly CancellationTokenSource _cts = new();

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case SwitchCollectStartMessage:
                _stopping = false; // 启动运行
                context.Send(context.Self, new SwitchCollectPollTickMessage());

                break;

            case SwitchCollectPollTickMessage:
                if (_cts.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                if (!connector.CanConnect)
                {
                    MoveNext();
                    return Task.CompletedTask;
                }

                // 发布触发事件
                context.Publish(new SwitchMessage(connector, channelName, device, signalTag, null));

                break;

            case SwitchCollectStopMessage:
                _stopping = true; // 停止运行

                break;

            case Stopping:
                _cts.Cancel();

                break;
        }

        return Task.CompletedTask;

        // 执行下一次操作
        void MoveNext()
        {
            // 下一次执行任务
            if (!_cts.IsCancellationRequested && !_stopping)
            {
                var delayMilliseconds = options.Value.SwitchScanRate > 0 ? options.Value.SwitchScanRate : 100;
                context.ReenterAfter(
                    Task.Delay(delayMilliseconds, _cts.Token),
                    task =>
                    {
                        if (task.IsCompletedSuccessfully && !_cts.IsCancellationRequested && !_stopping)
                        {
                            context.Send(context.Self, new SwitchCollectPollTickMessage());
                        }
                    });
            }
        }
    }
}

/// <summary>
/// Switch 采集开始消息。
/// </summary>
public sealed record SwitchCollectStartMessage;

/// <summary>
/// Switch 采集轮询消息。
/// </summary>
public sealed record SwitchCollectPollTickMessage;

/// <summary>
/// Switch 采集停止消息。
/// </summary>
public sealed record SwitchCollectStopMessage;
