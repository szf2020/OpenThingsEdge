using ThingsEdge.Exchange.Addresses;
using ThingsEdge.Exchange.Configuration;
using ThingsEdge.Exchange.Contracts.Variables;
using ThingsEdge.Exchange.Forwarders;

namespace ThingsEdge.Exchange;

/// <summary>
/// Extensions for <see cref="IExchangeBuilder"/>
/// </summary>
public static class IExchangeBuilderExtensions
{
    extension(IExchangeBuilder builder)
    {
        /// <summary>
        /// 使用设备基于本地JSON文件的提供者，默认目录为 "[执行目录]/config/"，可以使用单一的配置文件 tags.conf，也可以采用文件夹多层级配置，文件优先级大于目录层级。
        /// </summary>
        /// <returns></returns>
        public IExchangeBuilder UseDeviceFileProvider()
        {
            builder.Builder.ConfigureServices((_, services) =>
            {
                services.AddSingleton<IAddressFactory, DefaultAddressFactory>();
                services.AddSingleton<IAddressProvider, FileAddressProvider>();
            });
            return builder;
        }

        /// <summary>
        /// 使用自定义设备提供者，自定义对象需实现 <see cref="IAddressProvider"/> 接口。
        /// </summary>
        /// <typeparam name="TDeviceProvider"></typeparam>
        /// <returns></returns>
        public IExchangeBuilder UseDeviceCustomProvider<TDeviceProvider>()
            where TDeviceProvider : class, IAddressProvider
        {
            builder.Builder.ConfigureServices((_, services) =>
            {
                services.AddSingleton<IAddressFactory, DefaultAddressFactory>();
                services.AddSingleton<IAddressProvider, TDeviceProvider>();
            });
            return builder;
        }

        /// <summary>
        /// 使用设备心跳信息处理服务，其中 <see cref="TagFlag.Heartbeat"/> 会发布此事件。
        /// </summary>
        /// <typeparam name="TForwarder"></typeparam>
        /// <returns></returns>
        public IExchangeBuilder UseDeviceHeartbeatForwarder<TForwarder>()
            where TForwarder : IHeartbeatForwarder
        {
            builder.Builder.ConfigureServices((_, services) =>
            {
                services.AddTransient(typeof(IHeartbeatForwarder), typeof(TForwarder));
            });

            return builder;
        }

        /// <summary>
        /// 使用通知消息处理服务，其中 <see cref="TagFlag.Notice"/> 会发布此事件。
        /// </summary>
        /// <typeparam name="TForwarder"></typeparam>
        /// <returns></returns>
        public IExchangeBuilder UseNativeNoticeForwarder<TForwarder>()
            where TForwarder : INoticeForwarder
        {
            builder.Builder.ConfigureServices((_, services) =>
            {
                services.AddTransient(typeof(INoticeForwarder), typeof(TForwarder));
            });

            return builder;
        }

        /// <summary>
        /// 使用本地的请求处理服务，其中 <see cref="TagFlag.Trigger"/> 会发布此事件。
        /// </summary>
        /// <typeparam name="TForwarder"></typeparam>
        /// <returns></returns>
        public IExchangeBuilder UseNativeTriggerForwarder<TForwarder>()
            where TForwarder : ITriggerForwarder
        {
            builder.Builder.ConfigureServices((_, services) =>
            {
                services.AddTransient(typeof(ITriggerForwarder), typeof(TForwarder));
            });

            return builder;
        }

        /// <summary>
        /// 使用开关消息处理服务，其中 <see cref="TagFlag.Switch"/> 会发布此事件。
        /// </summary>
        /// <typeparam name="TForwarder"></typeparam>
        /// <returns></returns>
        public IExchangeBuilder UseNativeSwitchForwarder<TForwarder>()
            where TForwarder : ISwitchForwarder
        {
            builder.Builder.ConfigureServices((_, services) =>
            {
                services.AddTransient(typeof(ISwitchForwarder), typeof(TForwarder));
            });

            return builder;
        }

        /// <summary>
        /// 设置参数，其中配置的参数会覆盖配置文件 "Exchange" 选项设置。
        /// </summary>
        /// <param name="optionsAction">参数选项设置</param>
        /// <returns></returns>
        public IExchangeBuilder UseOptions(Action<ExchangeOptions>? optionsAction = null)
        {
            if (optionsAction == null)
            {
                return builder;
            }

            builder.Builder.ConfigureServices((hostBuilder, services) =>
            {
                services.PostConfigure(optionsAction);
            });

            return builder;
        }
    }
}
