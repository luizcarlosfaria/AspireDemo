﻿using System.Diagnostics.CodeAnalysis;
using Dawn;
using OpenTelemetry.Trace;

namespace Oragon.RabbitMQ;

/// <summary>
/// 
/// </summary>
[SuppressMessage("IDE", "IDE1006", Justification = "Template should be a static expression")]

public static class MessagingTelemetryNames
{
    private static readonly List<string> names = [
        "gaGO.io/RabbitMQ/AsyncQueueConsumer",
        "gaGO.io/RabbitMQ/AsyncRpcConsumer",
        "gaGO.io/RabbitMQ/MessagePublisher"
        ];

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static string GetName(string name)
    {
        var fullName = $"gaGO.io/RabbitMQ/{name}";
        return !names.Contains(fullName)
            ? throw new InvalidOperationException($"Name '{name}' is not registred ")
            : fullName;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tracerProviderBuilder"></param>
    /// <returns></returns>
    public static TracerProviderBuilder AddRabbitMQInstrumentation(this TracerProviderBuilder tracerProviderBuilder)
    {
        _ = Guard.Argument(tracerProviderBuilder).NotNull();

        foreach (var name in names)
        {
            tracerProviderBuilder.AddSource(name);
        }
        return tracerProviderBuilder;
    }
}
