namespace Marites.Application.Abstractions;

/// <summary>Discovers broker topology, metadata, shadow-resource lifecycle, and permissions.</summary>
public interface IBrokerAdmin;

/// <summary>Opens a non-destructive observation stream from a provider source.</summary>
public interface IMessageTap;

/// <summary>Publishes or replays a message to a provider destination.</summary>
public interface IMessagePublisher;

/// <summary>Inspects messages without consuming the source message.</summary>
public interface IMessageInspector;

/// <summary>Provides a replaceable source of UTC time for deterministic application behavior.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
