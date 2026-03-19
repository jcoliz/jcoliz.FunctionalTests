using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace jcoliz.FunctionalTests;

/// <summary>
/// Defines the capabilities required by step classes to perform their actions.
/// </summary>
/// <remarks>
/// This interface abstracts away the underlying test context and provides access to commonly used resources such as the Playwright page and an object store for sharing data between steps.
/// Step classes can depend on this interface to access the resources they need without being tightly coupled to a specific test framework or context implementation.
/// This promotes separation of concerns and makes the step classes more reusable and easier to test in isolation
/// </remarks>
public interface IBaseStepCapabilities
{
    /// <summary>
    /// The Playwright page instance that steps can use to interact with the web application under test.
    /// </summary>
    IPage Page { get; }

    /// <summary>
    /// The object store for sharing data between steps.
    /// </summary>
    ObjectStore ObjectStore { get; }

    /// <summary>
    /// Gets or creates a page object model of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the page object model.</typeparam>
    /// <returns>The page object model instance.</returns>
    T GetOrCreatePage<T>() where T : PageObjectModel;

    /// <summary>
    /// Registers an async cleanup action to be executed after the test completes.
    /// Actions are keyed for uniqueness — registering the same key again is a no-op.
    /// </summary>
    /// <param name="key">Unique key to prevent duplicate registrations.</param>
    /// <param name="action">The async cleanup action to execute during teardown.</param>
    void AddCleanupAction(string key, Func<Task> action);
}
