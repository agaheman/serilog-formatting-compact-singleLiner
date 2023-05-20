using Serilog.Events;
using Serilog.Formatting.Json;
using System;
using System.IO;

namespace Serilog.Formatting.Dotin
{
    /// <summary>
    /// An <see cref="ITextFormatter"/> that writes events in a compact JSON format, for consumption in environments 
    /// without message template support. Message templates are rendered into text and a hashed event id is included.
    /// </summary>
    public class SingleLineRenderedCompactJsonFormatter : ITextFormatter
    {
        readonly JsonValueFormatter _valueFormatter;

        /// <summary>
        /// Construct a <see cref="SingleLineCompactJsonFormatter"/>, optionally supplying a formatter for
        /// <see cref="LogEventPropertyValue"/>s on the event.
        /// </summary>
        /// <param name="valueFormatter">A value formatter, or null.</param>
        public SingleLineRenderedCompactJsonFormatter(JsonValueFormatter valueFormatter = null)
        {
            _valueFormatter = valueFormatter ?? new JsonValueFormatter(typeTagName: "$type");
        }

        /// <summary>
        /// Format the log event into the output. Subsequent events will be newline-delimited.
        /// </summary>
        /// <param name="logEvent">The event to format.</param>
        /// <param name="output">The output.</param>
        public void Format(LogEvent logEvent, TextWriter output)
        {
            FormatEvent(logEvent, output, _valueFormatter);
            output.WriteLine();
        }

        /// <summary>
        /// Format the log event into the output.
        /// </summary>
        /// <param name="logEvent">The event to format.</param>
        /// <param name="output">The output.</param>
        /// <param name="valueFormatter">A value formatter for <see cref="LogEventPropertyValue"/>s on the event.</param>
        public static void FormatEvent(LogEvent logEvent, TextWriter output, JsonValueFormatter valueFormatter)
        {
            if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (valueFormatter == null) throw new ArgumentNullException(nameof(valueFormatter));

            output.Write("{\"@t\":\"");
            output.Write(logEvent.Timestamp.UtcDateTime.ToString("O"));
            output.Write("\",\"@m\":");
            var message = logEvent.MessageTemplate.Render(logEvent.Properties);
            JsonValueFormatter.WriteQuotedJsonString(message, output);
            output.Write(",\"@i\":\"");
            var id = EventIdHash.Compute(logEvent.MessageTemplate.Text);
            output.Write(id.ToString("x8"));
            output.Write('"');

            if (logEvent.Level != LogEventLevel.Information)
            {
                output.Write(",\"@l\":\"");
                output.Write(logEvent.Level);
                output.Write('\"');
            }

            if (logEvent.Exception != null)
            {
                output.Write(",\"@x\":");

                var cleanedException = logEvent.Exception.ToString().Trim()
                    .Replace("\n", " ")
                    .Replace("\r", " ");

                JsonValueFormatter.WriteQuotedJsonString(cleanedException, output);
            }

            foreach (var property in logEvent.Properties)
            {
                var name = property.Key;
                if (name.Length > 0 && name[0] == '@')
                {
                    // Escape first '@' by doubling
                    name = '@' + name;
                }

                output.Write(',');
                JsonValueFormatter.WriteQuotedJsonString(name, output);
                output.Write(':');
                if (property.Value.GetType() == typeof(string))
                {
                    var cleanedValue = property.ToString().Trim()
                        .Replace("\n", " ")
                        .Replace("\r", " ");

                    var cleanedProperty = new LogEventProperty(name, new ScalarValue(cleanedValue));

                    valueFormatter.Format(cleanedProperty.Value, output);
                }
                else
                {
                    valueFormatter.Format(property.Value, output);
                }
            }

            output.Write('}');
        }
    }
}
