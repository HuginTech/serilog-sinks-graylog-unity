using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Graylog.Core;
using Serilog.Sinks.Graylog.Core.Transport;
using System;
using System.Threading.Tasks;

namespace Serilog.Sinks.Graylog
{
    public sealed class GraylogSink : ILogEventSink, IDisposable
    {
        private readonly Lazy<IGelfConverter> _converter;
        private readonly Lazy<ITransport> _transport;
        private readonly bool _jsonprettyprint;

        public GraylogSink(GraylogSinkOptions options)
        {
            ISinkComponentsBuilder sinkComponentsBuilder = new SinkComponentsBuilder(options);

            _transport = new Lazy<ITransport>(sinkComponentsBuilder.MakeTransport);
            _converter = new Lazy<IGelfConverter>(() => sinkComponentsBuilder.MakeGelfConverter());
            _jsonprettyprint = options.JsonPrettyPrint;
        }

        public void Emit(LogEvent logEvent)
        {
            EmitAsync(logEvent).ContinueWith(
                task =>
                {
                    SelfLog.WriteLine("Oops something going wrong {0}", task.Exception);
                },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private Task EmitAsync(LogEvent logEvent)
        {
            var json = _converter.Value.GetGelfJson(logEvent);
            var payload = json.Print(_jsonprettyprint);

            return _transport.Value.Send(payload);
        }

        public void Dispose()
        {
            _transport.Value.Dispose();
        }
    }
}
