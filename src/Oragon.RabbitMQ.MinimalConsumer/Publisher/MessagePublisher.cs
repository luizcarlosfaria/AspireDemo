using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using RabbitMQ.Client;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Oragon.RabbitMQ.Publisher;

#nullable enable

/// <summary>
/// Basic publisher for RabbitMQ.
/// </summary>
/// <param name="connection"></param>
/// <param name="serializer"></param>
/// <param name="logger"></param>
public class MessagePublisher(IConnection connection, IAMQPSerializer serializer, ILogger<MessagePublisher> logger)
{
    private static readonly ActivitySource? s_activitySource = new(MessagingTelemetryNames.GetName(nameof(MessagePublisher)));
    private static readonly TextMapPropagator s_propagator = Propagators.DefaultTextMapPropagator;

    private readonly IAMQPSerializer serializer = serializer;
    private readonly ILogger<MessagePublisher> logger = logger;
    private readonly IConnection connection = connection;

    /// <summary>
    /// Send a message to the RabbitMQ.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="exchange"></param>
    /// <param name="routingKey"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    [SuppressMessage("Usage", "CA2201", Justification = "Do not raise reserved exception types")]
    public async Task SendAsync<T>(string exchange, string routingKey, T message)
    {
        //TODO: Rever
        using Activity? publisherActivity = s_activitySource?.StartActivity("MessagePublisher.SendAsync", ActivityKind.Producer) ?? null;

        using IChannel model = await this.connection.CreateChannelAsync().ConfigureAwait(true);

        var properties = model.CreateBasicProperties().EnsureHeaders().SetDurable(true);

        //TODO: Rever
        var contextToInject = GetActivityContext(publisherActivity);

        // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
        s_propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), properties, this.InjectTraceContextIntoBasicProperties);

        var body = this.serializer.Serialize(basicProperties: properties, message: message);

        await model.BasicPublishAsync(exchange, routingKey, properties, body).ConfigureAwait(true);

        //publisherActivity?.SetEndTime(DateTime.UtcNow);
    }

    private static ActivityContext GetActivityContext(Activity? activity)
    {
        ActivityContext contextToInject = default;
        if (activity != null)
        {
            contextToInject = activity.Context;
        }
        else if (Activity.Current != null)
        {
            contextToInject = Activity.Current.Context;
        }
        return contextToInject;
    }

    [SuppressMessage("Performance", "CA1848", Justification = "Use the LoggerMessage delegates")]
    [SuppressMessage("Performance", "CA2254", Justification = "Template should be a static expression")]
    [SuppressMessage("Design", "CA1031", Justification = "Do not catch general exception types")]
    private void InjectTraceContextIntoBasicProperties(BasicProperties props, string key, string value)
    {
        try
        {
            props.Headers ??= new Dictionary<string, object?>();

            props.Headers[key] = value;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to inject trace context.");
        }
    }
}
