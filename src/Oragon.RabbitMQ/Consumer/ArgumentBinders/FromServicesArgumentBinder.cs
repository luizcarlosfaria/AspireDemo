// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Dawn;
using Microsoft.Extensions.DependencyInjection;

namespace Oragon.RabbitMQ.Consumer.ArgumentBinders;

/// <summary>
/// Represents an argument binder for an AMQP message.
/// </summary>
/// <param name="parameterType"></param>
/// <param name="serviceKey"></param>
public class FromServicesArgumentBinder(Type parameterType, string serviceKey = null) : IAmqpArgumentBinder
{
    /// <summary>
    /// Get the Service Type used to get a service from dependency injection
    /// </summary>
    public Type ParameterType { get; } = parameterType;

    /// <summary>
    /// Get the Service Key (if needed) used to get a service from dependency injection
    /// </summary>
    public string ServiceKey { get; } = serviceKey;

    /// <summary>
    /// Get value from IAmqpContext
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public object GetValue(IAmqpContext context)
    {
        _ = Guard.Argument(context).NotNull();

        return this.GetValue(context.ServiceProvider);
    }

    /// <summary>
    /// Get value from service provider
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <returns></returns>
    public object GetValue(IServiceProvider serviceProvider)
    {
        _ = Guard.Argument(serviceProvider).NotNull();

        return string.IsNullOrWhiteSpace(this.ServiceKey)
            ? serviceProvider.GetRequiredService(this.ParameterType)
            : serviceProvider.GetRequiredKeyedService(this.ParameterType, this.ServiceKey);
    }
}