using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.Graylog.Core.Extensions;
using Serilog.Sinks.Graylog.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Defective.JSON;

namespace Serilog.Sinks.Graylog.Core.MessageBuilders
{
    /// <summary>
    /// Message builder
    /// </summary>
    /// <seealso cref="IMessageBuilder" />
    public class GelfMessageBuilder : IMessageBuilder
    {
        private const string DefaultGelfVersion = "1.1";

        protected GraylogSinkOptionsBase Options => _options;

        private readonly string _hostName;
        private readonly GraylogSinkOptionsBase _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="GelfMessageBuilder"/> class.
        /// </summary>
        /// <param name="hostName">Name of the host.</param>
        /// <param name="options">The options.</param>
        public GelfMessageBuilder(string hostName, GraylogSinkOptionsBase options)
        {
            _hostName = hostName;
            _options = options;
        }

        /// <summary>
        /// Builds the specified log event.
        /// </summary>
        /// <param name="logEvent">The log event.</param>
        /// <returns></returns>
        public virtual JSONObject Build(LogEvent logEvent)
        {
            string message = logEvent.RenderMessage();
            string shortMessage = message.Truncate(Options.ShortMessageMaxLength);

            var jsonObject = new JSONObject();
            jsonObject.AddField("version", DefaultGelfVersion);
            jsonObject.AddField("host", Options.HostnameOverride ?? _hostName);
            jsonObject.AddField("short_message", shortMessage);
            jsonObject.AddField("timestamp", logEvent.Timestamp.ConvertToNix());
            jsonObject.AddField("level", LogLevelMapper.GetMappedLevel(logEvent.Level));
            jsonObject.AddField("_stringLevel", logEvent.Level.ToString());
            jsonObject.AddField("_facility", Options.Facility!);

            if (message.Length > Options.ShortMessageMaxLength)
            {
                jsonObject.AddField("full_message", message);
            }

            foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
            {
                if (Options.ExcludeMessageTemplateProperties)
                {
                    var propertyTokens = logEvent.MessageTemplate.Tokens.OfType<PropertyToken>();

                    if (propertyTokens.Any(x => x.PropertyName == property.Key))
                    {
                        continue;
                    }
                }

                AddAdditionalField(jsonObject, property);
            }

            if (Options.IncludeMessageTemplate)
            {
                string messageTemplate = logEvent.MessageTemplate.Text;

                jsonObject.AddField($"_{Options.MessageTemplateFieldName}", messageTemplate);
            }

            return jsonObject;
        }

        private void AddAdditionalField(JSONObject jObject,
                                        KeyValuePair<string, LogEventPropertyValue> property,
                                        string memberPath = "")
        {
            string key = string.IsNullOrEmpty(memberPath)
                ? property.Key
                : $"{memberPath}.{property.Key}";

            switch (property.Value)
            {
                case ScalarValue scalarValue:
                    if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        key = "id_";
                    }

                    if (!key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                    {
                        key = $"_{key}";
                    }

                    if (scalarValue.Value == null)
                    {
                        jObject.AddField(key, JSONObject.nullObject);
                        break;
                    }

                    long l;
                    double d;
                    if (long.TryParse(scalarValue.Value.ToString(), out l))
                    {
                        jObject.AddField(key, l);
                    }
                    else if (double.TryParse(scalarValue.Value.ToString(), out d))
                    {
                        jObject.AddField(key, d);
                    }
                    else
                    {
                        jObject.AddField(key, scalarValue.Value.ToString());
                    }
                    break;
                case SequenceValue sequenceValue:
                    var sequenceValueString = RenderPropertyValue(sequenceValue);

                    jObject.AddField(key, sequenceValueString);

                    if (Options.ParseArrayValues)
                    {
                        int counter = 0;

                        foreach (var sequenceElement in sequenceValue.Elements)
                        {
                            AddAdditionalField(jObject, new KeyValuePair<string, LogEventPropertyValue>(counter.ToString(), sequenceElement), key);

                            counter++;
                        }
                    }

                    break;
                case StructureValue structureValue:
                    foreach (LogEventProperty logEventProperty in structureValue.Properties)
                    {
                        AddAdditionalField(jObject,
                                           new KeyValuePair<string, LogEventPropertyValue>(logEventProperty.Name, logEventProperty.Value),
                                           key);
                    }

                    break;
                case DictionaryValue dictionaryValue:
                    if (Options.ParseArrayValues)
                    {
                        foreach (KeyValuePair<ScalarValue, LogEventPropertyValue> dictionaryValueElement in dictionaryValue.Elements)
                        {
                            var renderedKey = RenderPropertyValue(dictionaryValueElement.Key);

                            AddAdditionalField(jObject, new KeyValuePair<string, LogEventPropertyValue>(renderedKey, dictionaryValueElement.Value), key);
                        }
                    } else
                    {
          
                        var dict = dictionaryValue.Elements.ToDictionary(k => k.Key.Value.ToString()!, v => RenderPropertyValue(v.Value));

                        var stringDictionary = JSONObject.Create(dict);

                        jObject.AddField(key, stringDictionary);
                    }

                    break;
            }
        }

        private static string RenderPropertyValue(LogEventPropertyValue propertyValue)
        {
            using TextWriter tw = new StringWriter();

            propertyValue.Render(tw);

            string result = tw.ToString()!;
            result = result.Trim('"');

            return result;
        }
    }
}
