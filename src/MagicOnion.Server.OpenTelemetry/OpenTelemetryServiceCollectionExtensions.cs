using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace MagicOnion.Server.OpenTelemetry
{
    /// <summary>MagicOnion extensions for Microsoft.Extensions.Hosting classes</summary>
    public static class OpenTelemetryServiceCollectionExtensions
    {
        /// <summary>add MagicOnion Telemetry.</summary>
        public static IServiceCollection AddMagicOnionOpenTelemetry(this IServiceCollection services,
            string configurationName = "")
        {
            var options = BindMagicOnionOpenTelemetryOptions(services, configurationName);
            return AddMagicOnionOpenTelemetry(services, options);
        }
        /// <summary>add MagicOnion Telemetry.</summary>
        public static IServiceCollection AddMagicOnionOpenTelemetry(this IServiceCollection services,
            MagicOnionOpenTelemetryOptions options)
        {
            return AddMagicOnionOpenTelemetry(services, options, null, null);
        }
        /// <summary>add MagicOnion Telemetry.</summary>
        public static IServiceCollection AddMagicOnionOpenTelemetry(this IServiceCollection services,
            Action<MagicOnionOpenTelemetryOptions, MagicOnionOpenTelemetryMeterFactoryOption> configureMeterFactory,
            Action<MagicOnionOpenTelemetryOptions, IServiceProvider, TracerProviderBuilder> configureTracerFactory,
            string configurationName = "")
        {
            var options = BindMagicOnionOpenTelemetryOptions(services, configurationName);
            return AddMagicOnionOpenTelemetry(services, options, configureMeterFactory, configureTracerFactory);
        }
        /// <summary>add MagicOnion Telemetry.</summary>
        public static IServiceCollection AddMagicOnionOpenTelemetry(this IServiceCollection services,
            MagicOnionOpenTelemetryOptions options,
            Action<MagicOnionOpenTelemetryOptions, MagicOnionOpenTelemetryMeterFactoryOption> configureMeterProvider,
            Action<MagicOnionOpenTelemetryOptions, IServiceProvider, TracerProviderBuilder> configureTracerProvider)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            services.AddSingleton(options);

            // configure MeterFactory
            if (configureMeterProvider != null)
            {
                var meterFactoryOption = new MagicOnionOpenTelemetryMeterFactoryOption();
                configureMeterProvider(options, meterFactoryOption);

                MeterProvider.SetDefault(Sdk.CreateMeterProviderBuilder()
                    .SetProcessor(meterFactoryOption.MetricProcessor)
                    .SetExporter(meterFactoryOption.MetricExporter)
                    .SetPushInterval(meterFactoryOption.MetricPushInterval)
                    .Build());

                services.AddSingleton(meterFactoryOption.MetricExporter);
                if (meterFactoryOption.MeterLogger != null)
                {
                    services.AddSingleton<IMagicOnionLogger>(meterFactoryOption.MeterLogger.Invoke(MeterProvider.Default));
                }
            }

            // configure TracerFactory
            if (configureTracerProvider != null)
            {
                if (string.IsNullOrEmpty(options.MagicOnionActivityName))
                {
                    throw new NullReferenceException(nameof(options.MagicOnionActivityName));
                }

                var tracerFactory = services.AddOpenTelemetryTracing((provider, builder) =>
                {
                    // ActivitySourceName must match to TracerName.
                    builder.AddSource(options.MagicOnionActivityName);
                    configureTracerProvider?.Invoke(options, provider, builder);
                });
                services.AddSingleton(tracerFactory);

                // Avoid directly register ActivitySource to Singleton for easier identification.
                var activitySource = new ActivitySource(options.MagicOnionActivityName);
                var magicOnionActivitySources = new MagicOnionActivitySources(activitySource);
                services.AddSingleton(magicOnionActivitySources);
            }

            return services;
        }

        private static MagicOnionOpenTelemetryOptions BindMagicOnionOpenTelemetryOptions(IServiceCollection services, string configurationName)
        {
            var name = !string.IsNullOrEmpty(configurationName) ? configurationName : "MagicOnion:OpenTelemetry";
            var serviceProvider = services.BuildServiceProvider();
            var config = serviceProvider.GetService<IConfiguration>();
            var options = new MagicOnionOpenTelemetryOptions();
            config.GetSection(name).Bind(options);
            return options;
        }
    }
}