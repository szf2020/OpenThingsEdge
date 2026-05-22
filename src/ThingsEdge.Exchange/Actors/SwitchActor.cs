using Proto;
using ThingsEdge.Exchange.Configuration;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Infrastructure.Actors;
using ThingsEdge.Exchange.Messages;
using ThingsEdge.Exchange.Utils;

namespace ThingsEdge.Exchange.Actors;

/// <summary>
/// 监控开关的 Actor。
/// </summary>
internal sealed class SwitchActor(
    string channelName,
    Device device,
    SignalTag signalTag,
    IDriverConnector connector,
    IOptions<ExchangeOptions> options,
    ILogger<SwitchActor> logger) : IActor
{
    private bool _isOn; // 开关处于的状态
    private PID? _collectActor;
    private readonly CancellationTokenSource _cts = new();

    public async Task ReceiveAsync(IContext context)
    {
        _collectActor ??= context.Spawn(Props.FromProducer(() => new SwitchCollectActor(channelName, device, signalTag, connector, options)));

        switch (context.Message)
        {
            case Started:
                context.Send(context.Self, new SwitchTagPollTickMessage());

                break;

            case SwitchTagPollTickMessage:
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
                var (ok, data, err) = await connector.ReadAsync(signalTag).ConfigureAwait(false);
                if (!ok)
                {
                    logger.LogError("[SwitchActor] Switch 开关数据处理异常，设备：{DeviceName}，标记：{TagName}, 地址：{TagAddress}，错误：{Err}",
                        device.Name, signalTag.Name, signalTag.Address, err);

                    // 若读取失败，且开关处于 on 状态，则发送关闭动作信号（防止因设备未掉线，而读取失败导致一直发送数据）。
                    // 发送 Off 信号结束标识事件
                    context.Publish(new SwitchMessage(
                        connector,
                        channelName,
                        device,
                        signalTag,
                        WorkerHelper.CreateSwitchPayloadOffOff(signalTag),
                        SwitchState.Off,
                        true));

                    // 读取失败或开关关闭时，重置信号，让子任务停止运行。
                    _isOn = false;
                    context.Send(_collectActor, new SwitchCollectStopMessage());

                    MoveNext(); // 执行下一次任务

                    return;
                }

                if (WorkerHelper.CheckSwitchOn(data!)) // Open 标记
                {
                    // Open 信号，在本身处于关闭状态，才执行开启动作。
                    if (!_isOn)
                    {
                        // 发送 On 信号结束标识
                        context.Publish(new SwitchMessage(connector, channelName, device, signalTag, data, SwitchState.On, true));

                        // 开关开启时，发送信号，让子任务执行。
                        _isOn = true;
                        context.Send(_collectActor, new SwitchCollectStartMessage());
                    }
                }
                else
                {
                    // Close 标记，在本身处于开启状态，才执行关闭动作。
                    if (_isOn)
                    {
                        // 发送 Off 信号结束标识事件
                        context.Publish(new SwitchMessage(connector, channelName, device, signalTag, data, SwitchState.Off, true));

                        // 读取失败或开关关闭时，重置信号，让子任务停止运行。
                        _isOn = false;
                        context.Send(_collectActor, new SwitchCollectStopMessage());
                    }
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
                            context.Send(context.Self, new SwitchTagPollTickMessage());
                        }
                    });
            }
        }
    }
}

/// <summary>
/// Switch 节点轮询消息。
/// </summary>
public sealed record SwitchTagPollTickMessage;
