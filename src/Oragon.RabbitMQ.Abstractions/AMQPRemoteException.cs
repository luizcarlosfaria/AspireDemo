namespace Oragon.RabbitMQ;



/// <summary>
/// Represents errors that occur during remote operations.
/// </summary>
[Serializable]
public class AMQPRemoteException : Exception
{
    private readonly string remoteStackTrace;

    /// <summary>
    /// Initializes a new instance of the <see cref="AMQPRemoteException"/> class.
    /// </summary>
    public AMQPRemoteException() : this(message: null, remoteStackTrace: null, inner: null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AMQPRemoteException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public AMQPRemoteException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AMQPRemoteException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public AMQPRemoteException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AMQPRemoteException"/> class with a specified error message, a reference to the remote stack trace, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="remoteStackTrace">The remote stack trace.</param>
    /// <param name="inner">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public AMQPRemoteException(string message, string remoteStackTrace, Exception inner) : base(message, inner) { this.remoteStackTrace = remoteStackTrace; }

    /// <summary>
    /// Gets the string representation of the frames on the call stack at the time the current exception was thrown.
    /// </summary>
    public override string StackTrace => remoteStackTrace;

    /// <summary>
    /// Initializes a new instance of the <see cref="AMQPRemoteException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> that holds the serialized object data about the exception being thrown.</param>
    /// <param name="context">The <see cref="System.Runtime.Serialization.StreamingContext"/> that contains contextual information about the source or destination.</param>
    protected AMQPRemoteException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}
