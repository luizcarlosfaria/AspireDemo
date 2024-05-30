using Dawn;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using Oragon.RabbitMQ.Consumer.Actions;
using System.Diagnostics.CodeAnalysis;

namespace Oragon.RabbitMQ.Consumer;


/// <summary>
/// A consumer that processes messages in an RPC flow.
/// </summary>
/// <typeparam name="TService"></typeparam>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class AsyncRpcConsumer<TService, TRequest, TResponse> : AsyncQueueConsumer<TService, TRequest, Task<TResponse>>
    where TResponse : class
    where TRequest : class
{
    private readonly AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>> parameters;

    /// <summary>
    /// Creates a new instance of the <see cref="AsyncRpcConsumer{TService, TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="parameters"></param>
    /// <param name="serviceProvider"></param>
    public AsyncRpcConsumer(ILogger logger, AsyncQueueConsumerParameters<TService, TRequest, Task<TResponse>> parameters, IServiceProvider serviceProvider)
        : base(logger, parameters, serviceProvider)
    {
        this.parameters = Guard.Argument(parameters).NotNull().Value;
        this.parameters.Validate();
    }

    private static readonly Action<ILogger, Exception> s_logErrorOnDispatchWithoutReplyTo= LoggerMessage.Define(LogLevel.Error, new EventId(1, "Message cannot be processed in RPC Flow because original message didn't have a ReplyTo."), "Message cannot be processed in RPC Flow because original message didn't have a ReplyTo.");


    /// <summary>
    /// Dispatches the message to the service.
    /// </summary>
    /// <param name="receiveActivity"></param>
    /// <param name="receivedItem"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [SuppressMessage("Design", "CA1031", Justification = "Tratamento de exceçào global, isolando uma MACRO-operação")]
    protected override async Task<IAMQPResult> DispatchAsync(Activity receiveActivity, BasicDeliverEventArgs receivedItem, TRequest request)
    {
        _ = Guard.Argument(receivedItem).NotNull();
        _ = Guard.Argument(receiveActivity).NotNull();
        _ = Guard.Argument(request).NotNull();

        if (receivedItem.BasicProperties.ReplyTo == null)
        {
            s_logErrorOnDispatchWithoutReplyTo(Logger, null);

            return new RejectResult(false);
        }

        TResponse responsePayload = default;

        using (var dispatchActivity = activitySource.StartActivity(parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context))
        {
            try
            {
                var service = parameters.ServiceProvider.GetRequiredService<TService>();

                if (parameters.DispatchScope == DispatchScope.RootScope)
                {
                    responsePayload = await parameters.AdapterFunc(service, request).ConfigureAwait(true);
                }
                else if (parameters.DispatchScope == DispatchScope.ChildScope)
                {
                    using (var scope = parameters.ServiceProvider.CreateScope())
                    {
                        responsePayload = await parameters.AdapterFunc(service, request).ConfigureAwait(true);
                    }
                }
            }
            catch (Exception exception)
            {
                _ = (dispatchActivity?.SetStatus(ActivityStatusCode.Error, exception.ToString()));

                await SendReplyAsync(dispatchActivity, receivedItem, null, exception).ConfigureAwait(true);

                return new NackResult(parameters.RequeueOnCrash);
            }
        }

        using (var replyActivity = activitySource.StartActivity(parameters.AdapterExpressionText, ActivityKind.Internal, receiveActivity.Context))
        {
            await this.SendReplyAsync(replyActivity, receivedItem, responsePayload).ConfigureAwait(true);
        }
        return new AckResult();
    }

    private async Task SendReplyAsync(Activity activity, BasicDeliverEventArgs receivedItem, TResponse responsePayload = null, Exception exception = null)
    {
        _ = Guard.Argument(receivedItem).NotNull();
        _ = Guard.Argument(responsePayload).NotNull();


        var responseProperties = Channel.CreateBasicProperties()
                                                        .SetMessageId()
                                                        .IfFunction(it => exception != null, it => it.SetException(exception))
                                                        .SetTelemetry(activity)
                                                        .SetCorrelationId(receivedItem.BasicProperties);

        _ = (activity?.AddTag("Queue", receivedItem.BasicProperties.ReplyTo));
        _ = (activity?.AddTag("MessageId", responseProperties.MessageId));
        _ = (activity?.AddTag("CorrelationId", responseProperties.CorrelationId));

        await Channel.BasicPublishAsync(string.Empty,
            receivedItem.BasicProperties.ReplyTo,
            responseProperties,
            exception != null
                ? Array.Empty<byte>()
                : parameters.Serializer.Serialize(basicProperties: responseProperties, message: responsePayload)
        ).ConfigureAwait(true);

        //replyActivity?.SetEndTime(DateTime.UtcNow);
    }

}