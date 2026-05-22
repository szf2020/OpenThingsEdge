using Proto;
using Proto.DependencyInjection;
using ThingsEdge.Exchange.Configuration;
using ThingsEdge.Exchange.Connectors;
using ThingsEdge.Exchange.Forwarders;
using ThingsEdge.Exchange.Handlers;
using ThingsEdge.Exchange.Interfaces;
using ThingsEdge.Exchange.Interfaces.Impls;
using ThingsEdge.Exchange.Snapshot;
using ThingsEdge.Exchange.Storages.Curve;

namespace ThingsEdge.Exchange;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/>
/// </summary>
public static class ExchangeServiceCollectionExtensions
{
    extension(IHostBuilder builder)
    {
        /// <summary>
        /// 添加 ThingsEdge 组件。
        /// </summary>
        /// <param name="builderAction">配置</param>
        /// <returns></returns>
        public IHostBuilder AddThingsEdgeExchange(Action<IExchangeBuilder> builderAction)
        {
            // 注册服务
            builder.ConfigureServices((hostBuilder, services) =>
            {
                // 注册配置选项
                services.Configure<ExchangeOptions>(hostBuilder.Configuration.GetSection("Exchange"));

                // 注册缓存
                services.AddMemoryCache();

                // 注册 Actor
                services.AddSingleton(sp => new ActorSystem().WithServiceProvider(sp));

                // 注册消息处理器
                services.AddTransient<HeartbeatMessageHandler>();
                services.AddTransient<NoticeMessageHandler>();
                services.AddTransient<TriggerMessageHandler>();
                services.AddTransient<SwitchMessageHandler>();

                // 注册 Forwarder 代理
                services.AddTransient<IHeartbeatForwarderProxy, HeartbeatForwarderProxy>();
                services.AddTransient<INoticeForwarderProxy, NoticeForwarderProxy>();
                services.AddTransient<ITriggerForwarderProxy, TriggerForwarderProxy>();
                services.AddTransient<ISwitchForwarderProxy, SwitchForwarderProxy>();

                services.AddSingleton<IExchange, EngineExchange>();
                services.AddSingleton<IDriverConnectorManager, DriverConnectorManager>();
                services.AddTransient<ITagReaderWriter, TagReaderWriterImpl>();

                services.AddSingleton<ITagDataSnapshot, TagDataSnapshotImpl>();

                services.AddSingleton<CurveStorage>();
                services.AddSingleton<CurveContainer>();
            });

            var builder2 = new ExchangeBuilder(builder);
            builderAction.Invoke(builder2);

            return builder;
        }
    }
}
