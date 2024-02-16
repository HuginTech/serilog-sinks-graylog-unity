using Serilog.Events;
using Serilog.Sinks.Graylog.Core.MessageBuilders;
using System;
using System.Collections.Generic;
using Defective.JSON;

namespace Serilog.Sinks.Graylog.Core
{
    public interface IGelfConverter
    {
        JSONObject GetGelfJson(LogEvent logEvent);
    }

    public class GelfConverter : IGelfConverter
    {
        private readonly IDictionary<BuilderType, Lazy<IMessageBuilder>> _messageBuilders;

        public GelfConverter(IDictionary<BuilderType, Lazy<IMessageBuilder>> messageBuilders)
        {
            _messageBuilders = messageBuilders;
        }

        public JSONObject GetGelfJson(LogEvent logEvent)
        {
            IMessageBuilder builder = logEvent.Exception != null
                ? _messageBuilders[BuilderType.Exception].Value
                : _messageBuilders[BuilderType.Message].Value;

            return builder.Build(logEvent);
        }
    }
}
