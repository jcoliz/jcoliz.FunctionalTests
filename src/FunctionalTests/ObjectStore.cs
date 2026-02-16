using System.Collections.Generic;

namespace jcoliz.FunctionalTests;

/// <summary>
/// Stores objects to be shared between test steps.
/// </summary>
/// <remarks>
/// This helps make the feature tests generatable, without having to
/// worry about local variables. All objects generated or needed by the tests are
/// contained here.
/// </remarks>
public class ObjectStore
{
    private readonly Dictionary<string, object> _objects = new();

    /// <summary>
    /// Adds an object to the store with a specific key.
    /// </summary>
    /// <typeparam name="T">Type of object to store.</typeparam>
    /// <param name="key">Key to store the object under.</param>
    /// <param name="obj">Object to store.</param>
    public void Add<T>(string key, T obj) where T : class
    {
        _objects[key] = obj;
    }

    /// <summary>
    /// Adds an object to the store using its type name as the key.
    /// </summary>
    /// <typeparam name="T">Type of object to store.</typeparam>
    /// <param name="obj">Object to store.</param>
    public void Add<T>(T obj) where T : class
    {
        _objects[typeof(T).Name] = obj;
    }

    /// <summary>
    /// Gets an object from the store by key.
    /// </summary>
    /// <typeparam name="T">Type of object to retrieve.</typeparam>
    /// <param name="key">Key the object was stored under.</param>
    /// <returns>The stored object.</returns>
    public T Get<T>(string key) where T : class
    {
        return (T)_objects[key];
    }

    /// <summary>
    /// Gets an object from the store using its type name as the key.
    /// </summary>
    /// <typeparam name="T">Type of object to retrieve.</typeparam>
    /// <returns>The stored object.</returns>
    public T Get<T>() where T : class
    {
        return (T)_objects[typeof(T).Name];
    }

    /// <summary>
    /// Checks if the store contains an object with the specified key.
    /// </summary>
    /// <typeparam name="T">Type of object to check for.</typeparam>
    /// <param name="key">Key to check.</param>
    /// <returns>True if an object exists with the specified key.</returns>
    public bool Contains<T>(string key) where T : class
    {
        return _objects.ContainsKey(key);
    }

    /// <summary>
    /// Checks if the store contains an object of the specified type.
    /// </summary>
    /// <typeparam name="T">Type of object to check for.</typeparam>
    /// <returns>True if an object of the specified type exists.</returns>
    public bool Contains<T>() where T : class
    {
        return _objects.ContainsKey(typeof(T).Name);
    }
}
