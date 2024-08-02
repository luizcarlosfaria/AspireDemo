using DotNet.Testcontainers.Builders;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Oragon.RabbitMQ.Serialization;
using RabbitMQ.Client;
using Oragon.RabbitMQ.Consumer;
using RabbitMQ.Client.Events;
using Oragon.RabbitMQ.TestsExtensions;
using System.Text;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class GracefulShutdownTests
{
    public class ServiceDemo
    { }

    public class RequestMessageDemo
    { }

    public class ResponseMessageDemo
    { }

    public class ExampleService
    {
        public Task TestAsync(ExampleMessage message)
        {
            if (message.Error)
                throw new InvalidOperationException("Error");

            return Task.CompletedTask;
        }
    }
    public class ExampleMessage
    {
        public bool Error { get; set; } 
    }

       [Fact]
    public async Task GreacefulShutdownShouldCallBasicCancel()
    {
        string consumerTag = "consumerTag";
        string queueName = "xpto";

        ServiceCollection services = new();

        // Arrange
        AsyncEventingBasicConsumer queueConsumer = null;

        //-------------------------------------------------------
        var channelMock = new Mock<IChannel>();
        _ = channelMock.Setup(it => it.BasicConsumeAsync(
            It.Is<string>(queue => queue == queueName),
            false,
            It.IsAny<string>(),
            true,
            false,
            It.IsAny<IDictionary<string, object?>>(),
            It.IsAny<IAsyncBasicConsumer>(),
            It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string consumerTag, bool noLocal, bool exclusive, IDictionary<string, object> arguments, IAsyncBasicConsumer consumer, CancellationToken cancellationToken) => queueConsumer = (AsyncEventingBasicConsumer)consumer)
            .ReturnsAsync(consumerTag);

        channelMock.Setup(it => it.BasicRejectAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Verifiable(Times.Never);
        channelMock.Setup(it => it.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Verifiable(Times.Never);
        channelMock.Setup(it => it.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Verifiable(Times.Once);
        channelMock.Setup(it => it.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Verifiable(Times.Once);

        var channel = channelMock.Object;
        //-------------------------------------------------------

        //-------------------------------------------------------
        var connectionMock = new Mock<IConnection>();
        _ = connectionMock.Setup(it => it.CreateChannelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(channel);
        var connection = connectionMock.Object;
        _ = services.AddSingleton(connection);
        //-------------------------------------------------------


        //-------------------------------------------------------
        var connectionFactoryMock = new Mock<IConnectionFactory>();
        _ = connectionFactoryMock.Setup(it => it.CreateConnectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(connection);
        var connectionFactory = connectionFactoryMock.Object;
        _ = services.AddSingleton(sp => connectionFactory);
        //-------------------------------------------------------


        _ = services.AddLogging(loggingBuilder => loggingBuilder.AddConsole());
        _ = services.AddSingleton<IAMQPSerializer>(sp => new NewtonsoftAMQPSerializer(null));
        _ = services.AddScoped<ExampleService>();
        //-------------------------------------------------------

        services.MapQueue<ExampleService, ExampleMessage>((config) =>
           config
               .WithDispatchInChildScope()
               .WithAdapter((svc, msg) => svc.TestAsync(msg))
               .WithQueueName(queueName)
               .WithPrefetchCount(1)
       );

        var sp = services.BuildServiceProvider();
        var hostedService = sp.GetRequiredService<IHostedService>();

        CancellationTokenSource cts = new();

        await hostedService.StartAsync(cts.Token);

        Assert.NotNull(queueConsumer);

        var asyncQueueConsumer = (AsyncQueueConsumer<ExampleService, ExampleMessage, Task>)hostedService;

        ReadOnlyBasicProperties properties = new BasicProperties().ToReadOnly();

        // Act
        var bytes = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(new ExampleMessage() { Error = false }));

        await queueConsumer.HandleBasicDeliverAsync(consumerTag: consumerTag, deliveryTag: 1, redelivered: false, exchange: "e", routingKey: "r", properties: properties, body: new ReadOnlyMemory<byte>(bytes));

        cts.Cancel();

        // Assert
        Mock.VerifyAll();

    }
}
