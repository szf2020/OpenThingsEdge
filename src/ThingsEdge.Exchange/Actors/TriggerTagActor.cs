using Proto;
using ThingsEdge.Exchange.Configuration;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Infrastructure.Actors;
using ThingsEdge.Exchange.Messages;
using ThingsEdge.Exchange.Utils;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 监控触发的 Actor。
/// </summary>
internal sealed class TriggerTagActor(
    string channelName,
    Device device,
    SignalTag signalTag,
    IDriverConnector connector,
    IOptions<ExchangeOptions> options,
    ILogger<TriggerTagActor> logger) : IActor
{
    private int _state = int.MinValue;
    private readonly CancellationTokenSource _cts = new();

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                context.Send(context.Self, new TriggerTagPollTickMessage());
                break;

            case TriggerTagPollTickMessage:
                if (_cts.IsCancellationRequested)
                {
                    return;
                }

                if (!connector.CanConnect)
                {
                    MoveNext();
                    return;
                }

                // 若读取失败，该信号点不会复位，下次会继续读取执行。
                var (ok, data, err) = await connector.ReadAsync(signalTag).ConfigureAwait(false); // short 类型
                if (!ok)
                {
                    logger.LogError("[TriggerActor] Trigger 数据读取异常，设备：{DeviceName}，标记：{TagName}, 地址：{Address}，错误：{err}",
                        device.Name, signalTag.Name, signalTag.Address, err);

                    MoveNext();
                    return;
                }

                // 获取触发标记值。
                var state = WorkerHelper.GetTriggerState(data!);
                var hasChanged = state != _state;
                _state = state;

                // 必须先检测并更新标记状态值（开启回执校验），若值有变动且达到触发标记条件时则推送数据。
                if (hasChanged && state == options.Value.TriggerConditionValue)
                {
                    // 发布触发事件
                    context.Publish(new TriggerMessage(connector, channelName, device, signalTag, data!));
                }

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
            if (!_cts.IsCancellationRequested)
            {
                var delayMilliseconds = signalTag.ScanRate > 0 ? signalTag.ScanRate : options.Value.DefaultScanRate;
                context.ReenterAfter(
                    Task.Delay(delayMilliseconds, _cts.Token),
                    task =>
                    {
                        if (task.IsCompletedSuccessfully && !_cts.IsCancellationRequested)
                        {
                            context.Send(context.Self, new TriggerTagPollTickMessage());
                        }
                    });
            }
        }
    }
}

/// <summary>
/// Trigger 节点轮询消息。
/// </summary>
public sealed record TriggerTagPollTickMessage;
