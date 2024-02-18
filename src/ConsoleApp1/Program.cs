// See https://aka.ms/new-console-template for more information

using Serilog;
using Serilog.Core;
using Serilog.Sinks.Graylog;
using Serilog.Sinks.Graylog.Core.Transport;


var configuration = new LoggerConfiguration();
GraylogSinkOptions gso = new GraylogSinkOptions
{
    HostnameOrAddress = "localhost",
    Port = 12201,
    TransportType = TransportType.Udp
};
configuration = configuration.WriteTo.Graylog(gso);
var log = configuration.CreateLogger();

for (int i = 1; i <= 10; i++)
{
    log.Information($"Testing log.information {i}");

    try
    {
        throw new Exception();
    }
    catch (Exception ex)
    {
        log.Error(ex, $"Testing log.error {i}: " + ex.Message);
    }

    Thread.Sleep(100);
}
