using DotNetAspire.Architecture.Messaging.Consumer.Actions;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Text;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Actions;

namespace Oragon.RabbitMQ.Consumer;


public class AsyncQueueConsumer<TService, TRequest, TResponse> : ConsumerBase
    where TResponse : Task
    where TRequest : class
{
    private AsyncQueueConsumerParameters<TService, TRequest, TResponse> parameters;

    protected static readonly ActivitySource activitySource = new(MessagingTelemetryNames.GetName(nameof(AsyncQueueConsumer<TService, TRequest, TResponse>)));
    private static readonly TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;

    #region Constructors 

    public AsyncQueueConsumer(ILogger logger, AsyncQueueConsumerParameters<TService, TRequest, TResponse> parameters, IServiceProvider serviceProvider)
        : base(logger, parameters, serviceProvider)
    {
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
    }

    #endregion


    protected override IBasicConsumer BuildConsumer()
    {
        Guard.Argument(Model).NotNull();

        var consumer = new AsyncEventingBasicConsumer(Model);

        consumer.Received += Receive;

        return consumer;
    }

    public async Task Receive(object sender, BasicDeliverEventArgs delivery)
    {
        Guard.Argument(delivery).NotNull();
        Guard.Argument(delivery.BasicProperties).NotNull();


        var parentContext = propagator.Extract(default, delivery.BasicProperties, this.ExtractTraceContextFromBasicProperties);
        Baggage.Current = parentContext.Baggage;

        using var receiveActivity = activitySource.StartActivity("AsyncQueueConsumer.Receive", ActivityKind.Consumer, parentContext.ActivityContext) ?? new Activity("?AsyncQueueConsumer.Receive");

        receiveActivity.AddTag("Queue", parameters.QueueName);
        receiveActivity.AddTag("MessageId", delivery.BasicProperties.MessageId);
        receiveActivity.AddTag("CorrelationId", delivery.BasicProperties.CorrelationId);

        receiveActivity.SetTag("messaging.system", "rabbitmq");
        receiveActivity.SetTag("messaging.destination_kind", "queue");
        receiveActivity.SetTag("messaging.destination", delivery.Exchange);
        receiveActivity.SetTag("messaging.rabbitmq.routing_key", delivery.RoutingKey);

        var result = TryDeserialize(receiveActivity, delivery, out var request)
                            ? await Dispatch(receiveActivity, delivery, request)
                            : new RejectResult(false);

        result.Execute(Model, delivery);

        //receiveActivity?.SetEndTime(DateTime.UtcNow);
    }

    private IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
    {
        try
        {
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return new[] { Encoding.UTF8.GetString(bytes) };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to extract trace context.");
        }

        return Enumerable.Empty<string>();
    }

    private bool TryDeserialize(Activity receiveActivity, BasicDeliverEventArgs receivedItem, out TRequest request)
    {
        Guard.Argument(receivedItem).NotNull();

        var returnValue = true;

        request = default;
        try
        {
            request = parameters.Serializer.Deserialize<TRequest>(eventArgs: receivedItem);
        }
        catch (Exception exception)
        {
            returnValue = false;

            receiveActivity.SetStatus(ActivityStatusCode.Error, exception.ToString());

            logger.LogWarning("Message rejected during deserialization {exception}", exception);
        }

        return returnValue;
    }

    protected virtual async Task<IAMQPResult> Dispatch(Activity receiveActivity, BasicDeliverEventArgs receivedItem, TRequest request)
    {
        Guard.Argument(receiveActivity).NotNull();
        Guard.Argument(receivedItem).NotNull();

        if (request == null) return new RejectResult(false);

        IAMQPResult returnValue;

        using var dispatchActivity = activitySource.StartActivity(parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context);

        //using (var logContext = new EnterpriseApplicationLogContext())
        //{
        try
        {
            var service = parameters.ServiceProvider.GetRequiredService<TService>();

            if (parameters.DispatchScope == DispatchScope.RootScope)
            {
                await parameters.AdapterFunc(service, request);
            }
            else if (parameters.DispatchScope == DispatchScope.ChildScope)
            {
                using (var scope = parameters.ServiceProvider.CreateScope())
                {
                    await parameters.AdapterFunc(service, request);
                }
            }
            returnValue = new AckResult();
        }
        catch (Exception exception)
        {

            logger.LogWarning("Exception on processing message {queueName} {exception}", parameters.QueueName, exception);
            returnValue = new NackResult(parameters.RequeueOnCrash);

            dispatchActivity?.SetStatus(ActivityStatusCode.Error, exception.ToString());
        }
        //}



        return returnValue;
    }
}