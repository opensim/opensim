using System;
using System.Collections.Generic;

namespace OpenSim.Framework.Security.DOSProtector.SDK
{
    /// <summary>
    /// Interface for DOS protection implementations
    /// </summary>
    public interface IDOSProtector : IDisposable
    {
        /// <summary>
        /// Check if a given key is currently blocked
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="context">Optional context data for decision making (e.g., Country, UserId, Authenticated)</param>
        /// <returns>True if blocked, false otherwise</returns>
        bool IsBlocked(string key, IDOSProtectorContext context = null);

        /// <summary>
        /// Process the velocity of this context
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="endpoint">The endpoint for logging purposes</param>
        /// <param name="context">Optional context data for decision making (e.g., Country, UserId, Authenticated)</param>
        /// <returns>True if allowed, false if throttled</returns>
        bool Process(string key, string endpoint, IDOSProtectorContext context = null);

        /// <summary>
        /// Mark the end of processing for this context (decrements session counter)
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="endpoint">The endpoint for logging purposes</param>
        /// <param name="context">Optional context data (same as passed to Process)</param>
        void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null);

        /// <summary>
        /// Creates a disposable session scope that automatically calls ProcessEnd when disposed.
        /// Use with 'using' statement to ensure ProcessEnd is always called.
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="endpoint">The endpoint for logging purposes</param>
        /// <param name="context">Optional context data for decision making (e.g., Country, UserId, Authenticated)</param>
        /// <returns>A SessionScope that calls ProcessEnd on dispose</returns>
        IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null);
    }

    /// <summary>
    /// Context object for passing additional request data to DOS protector implementations.
    /// Thread-safe and supports namespaced keys to prevent collisions between plugins.
    /// Defends on the caller given context data integrity.
    /// </summary>
    public interface IDOSProtectorContext
    {
        /// <summary>
        /// Read-only access to all context data.
        /// Key format: "key" for default namespace, "namespace:key" for namespaced data.
        /// </summary>
        IReadOnlyDictionary<string, object> ContextData { get; }

        /// <summary>
        /// Gets a strongly-typed value from the context.
        /// </summary>
        /// <typeparam name="T">The type to cast the value to</typeparam>
        /// <param name="key">The context key</param>
        /// <param name="namespace">Optional namespace to avoid key collisions (default: null)</param>
        /// <param name="defaultValue">Value to return if key not found or type mismatch</param>
        /// <returns>The value cast to T, or defaultValue if not found</returns>
        T GetContextData<T>(string key, string @namespace = null, T defaultValue = default);

        /// <summary>
        /// Tries to get a strongly-typed value from the context.
        /// </summary>
        /// <typeparam name="T">The type to cast the value to</typeparam>
        /// <param name="key">The context key</param>
        /// <param name="value">The output value if found and type matches</param>
        /// <param name="namespace">Optional namespace to avoid key collisions (default: null)</param>
        /// <returns>True if value was found and type matches, false otherwise</returns>
        bool TryGetContextData<T>(string key, out T value, string @namespace = null);

        /// <summary>
        /// Sets a value in the context.
        /// </summary>
        /// <param name="key">The context key (cannot be null or empty)</param>
        /// <param name="value">The value to store (can be null)</param>
        /// <param name="namespace">Optional namespace to avoid key collisions (default: null)</param>
        /// <param name="allowOverwrite">If false, throws exception when key already exists (default: true)</param>
        /// <exception cref="System.ArgumentNullException">Thrown when key is null or empty</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when key exists and allowOverwrite is false</exception>
        void SetContextData(string key, object value, string @namespace = null, bool allowOverwrite = true);

        /// <summary>
        /// Checks if a key exists in the context.
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="namespace">Optional namespace (default: null)</param>
        /// <returns>True if key exists, false otherwise</returns>
        bool HasContextData(string key, string @namespace = null);

        /// <summary>
        /// Removes a key from the context.
        /// </summary>
        /// <param name="key">The context key</param>
        /// <param name="namespace">Optional namespace (default: null)</param>
        /// <returns>True if key was found and removed, false otherwise</returns>
        bool RemoveContextData(string key, string @namespace = null);

        /// <summary>
        /// Gets all keys in the context, optionally filtered by namespace.
        /// </summary>
        /// <param name="namespace">Optional namespace filter (default: null = all keys)</param>
        /// <returns>Enumerable of context keys</returns>
        IEnumerable<string> GetKeys(string @namespace = null);
    }

    /// <summary>
    /// Interface for DOS protector configuration options
    /// </summary>
    public interface IDOSProtectorOptions
    {
        string ReportingName { get; set; }
        ThrottleAction ThrottledAction { get; set; }
        TimeSpan InspectionTTL { get; set; }
        DOSProtectorLogLevel LogLevel { get; set; }
        bool RedactClientIdentifiers { get; set; }
    }
}

