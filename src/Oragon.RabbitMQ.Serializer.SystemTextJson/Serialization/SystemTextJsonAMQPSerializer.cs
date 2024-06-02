using RabbitMQ.Client;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Oragon.RabbitMQ.Serialization;


/// <summary>
/// Implements serialization using System.Text.Json
/// </summary>
[SuppressMessage("Sonar", "S100", Justification = "AMQP is a acronym for Advanced Message Queuing Protocol, so it's a name.")]
[SuppressMessage("Sonar", "S101", Justification = "AMQP is a acronym for Advanced Message Queuing Protocol, so it's a name.")]
public class SystemTextJsonAMQPSerializer : AMQPBaseSerializer
{

    /// <summary>
    /// Create a instance of SystemTextJsonAMQPSerializer
    /// </summary>
    /// <param name="activitySource"></param>
    public SystemTextJsonAMQPSerializer(ActivitySource activitySource) : base(activitySource, nameof(SystemTextJsonAMQPSerializer)) { }


    /// <summary>
    /// Deserialize a message using System.Text.Json
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    protected override TMessage DeserializeInternal<TMessage>(IReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body)
    {
        var bytes = body.ToArray();
        if (bytes.Length > 0)
        {
            var message = Encoding.UTF8.GetString(bytes);
            if (!string.IsNullOrWhiteSpace(message))
            {
                return System.Text.Json.JsonSerializer.Deserialize<TMessage>(message);
            }
        }
        return default;

    }

    /// <summary>
    /// Serialize a message using System.Text.Json  
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    protected override byte[] SerializeInternal<TMessage>(BasicProperties basicProperties, TMessage message)
    {
        return Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(message));
    }
}

