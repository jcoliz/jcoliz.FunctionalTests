using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotNetEnv;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace jcoliz.FunctionalTests;

/// <summary>
/// Base class for functional tests. Inherit from this class to create a functional test.
/// </summary>
/// <remarks>
/// Features
/// * Parameter handling with environment variable support: GetRequiredParameter method resolves {ENV_VAR} references from .runsettings and .env files.
/// * Environment variable loading: Automatically loads environment variables from .env file if it exists, allowing flexible configuration without hardcoding values in .runsettings.
/// </remarks>
public abstract partial class FunctionalTest : PageTest
{
    #region Fields
    protected ObjectStore _objectStore = new();

    /// <summary>
    /// Gets the test correlation context for distributed tracing.
    /// </summary>
    /// <remarks>
    /// Created in <see cref="SetUpBase"/> and disposed in <see cref="TearDownBase"/>.
    /// Subclasses can use this to attach correlation headers to HTTP clients
    /// via <see cref="TestCorrelationContext.BuildCorrelationHeaders"/>.
    /// </remarks>
    protected TestCorrelationContext? _correlationContext;

    /// <summary>
    /// Gets the cached web application URL, resolving it from test parameters on first access.
    /// </summary>
    /// <remarks>
    /// The value is resolved once from <c>GetRequiredParameter("webAppUrl")</c> and cached
    /// in a static field for all subsequent accesses across test instances.
    /// </remarks>
    protected static string WebAppUrl => _cachedWebAppUrl ??= GetRequiredParameter("webAppUrl");
    private static string? _cachedWebAppUrl;

    #endregion

    #region Overrides

    public override BrowserNewContextOptions ContextOptions() =>
        new()
        {
            AcceptDownloads = true,
            ViewportSize = new ViewportSize() { Width = 1280, Height = 720 },
            BaseURL = WebAppUrl
        };
    #endregion

    #region Setup and Teardown

    [SetUp]
    public async Task SetUpBase()
    {
        // By convention, I put data-test-id attributes on important elements
        Playwright.Selectors.SetTestIdAttribute("data-test-id");

        // Note that this does need to be done in setup, because we get a new
        // browser context every time. Is there a place we could tell Playwright
        // this just ONCE??
        var defaultTimeoutParam = TestContext.Parameters["defaultTimeout"];
        if (Int32.TryParse(defaultTimeoutParam, out var val))
            Context.SetDefaultTimeout(val);

        // Need a fresh object store for each test
        _objectStore = new ObjectStore();

        // Add a basepage object to the object store, so we can later get at the functionality
        // offered by the page object model (like ScreenShotAsync) without needing to pass around the page object itself
        _objectStore.Add(new PageObjectModel(Page));

        // Create test correlation context for distributed tracing
        // and set correlation headers on the browser context
        _correlationContext = new TestCorrelationContext(TestContext.CurrentContext.Test);
        await Context.SetExtraHTTPHeadersAsync(_correlationContext.BuildCorrelationHeaders());
    }

    [TearDown]
    public async Task TearDownBase()
    {
        // Capture screenshot only on test failure
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            var pageModel = _objectStore.Get<PageObjectModel>();
            await pageModel.SaveScreenshotAsync($"FAILED");
        }

        // Dispose test correlation context
        _correlationContext?.Dispose();
        _correlationContext = null;
    }

    #endregion

    #region Page Object Model Access

    /// <summary>
    /// Gets or creates a page object model of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of page object model to retrieve or create.</typeparam>
    /// <returns>An instance of the specified page object model type.</returns>
    /// <remarks>
    /// This method implements a caching pattern:
    /// - If the page object already exists in the ObjectStore, returns the existing instance
    /// - Otherwise, creates a new instance using the constructor that takes IPage
    /// - Stores the new instance in the ObjectStore for reuse
    ///
    /// This ensures page objects are created once per test and reused across steps.
    /// </remarks>
    public T GetOrCreatePage<T>() where T : PageObjectModel
    {
        if (_objectStore.Contains<T>())
        {
            return _objectStore.Get<T>();
        }

        // Create new page using constructor that takes IPage
        var page = (T)Activator.CreateInstance(typeof(T), Page)!;
        _objectStore.Add(page);
        return page;
    }

    #endregion

    #region Parameter Handling

    private static bool _environmentVariablesLoaded = false;

    /// <summary>
    /// Gets a required test parameter and resolves any environment variable references.
    /// </summary>
    /// <param name="parameterName">Name of the test parameter to retrieve.</param>
    /// <returns>The parameter value with any environment variable references resolved.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the parameter is not set or when a referenced environment variable doesn't exist.
    /// </exception>
    /// <remarks>
    /// Parameters can contain environment variable references using the syntax: {ENV_VAR_NAME}
    /// For example: "https://localhost:5001" or "{WEB_APP_URL}"
    /// </remarks>
    protected static string GetRequiredParameter(string parameterName)
    {
        // Ensure environment variables are loaded before resolving parameters
        EnsureEnvironmentVariablesLoaded();

        var rawValue = TestContext.Parameters[parameterName];

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException(
                $"Required test parameter '{parameterName}' is not set in .runsettings file."
            );
        }

        return ResolveEnvironmentVariables(rawValue, parameterName);
    }

    /// <summary>
    /// Ensures environment variables are loaded from .env file before they're needed.
    /// Uses a static flag to ensure loading happens only once across all test instances.
    /// </summary>
    private static void EnsureEnvironmentVariablesLoaded()
    {
        if (_environmentVariablesLoaded)
            return;

        lock (typeof(FunctionalTest))
        {
            if (_environmentVariablesLoaded)
                return;

            LoadEnvironmentVariables();
            _environmentVariablesLoaded = true;
        }
    }


    /// <summary>
    /// Loads environment variables from .env file if it exists in the test project root.
    /// </summary>
    /// <remarks>
    /// This allows test configuration via .env files instead of only using .runsettings.
    /// Environment variables from .env can be referenced in .runsettings using {VAR_NAME} syntax.
    /// Silently continues if .env file doesn't exist.
    /// </remarks>
    private static void LoadEnvironmentVariables()
    {
        try
        {
            // Try multiple paths to find .env file
            var searchPaths = new[]
            {
                // Current directory (where tests are executed from)
                Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                // Test assembly directory
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env"),
                // Go up to find project root (handles bin/Debug/net10.0 structure)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".env")
            };

            foreach (var envFilePath in searchPaths)
            {
                var normalizedPath = Path.GetFullPath(envFilePath);
                if (File.Exists(normalizedPath))
                {
                    Env.Load(normalizedPath);
                    Console.WriteLine($"[Environment] Loaded environment variables from: {normalizedPath}");
                    return;
                }
            }

            // No .env file found - this is OK, not all environments need it
            Console.WriteLine("[Environment] No .env file found (this is optional)");
        }
        catch (Exception ex)
        {
            // Log warning but don't fail tests if .env loading fails
            Console.WriteLine($"[Environment] Warning: Failed to load .env file: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves environment variable references in curly braces (e.g., {ENV_VAR}).
    /// </summary>
    /// <param name="value">String that may contain {ENV_VAR} references.</param>
    /// <param name="contextName">Name of the parameter/setting being resolved (for error messages).</param>
    /// <returns>String with all environment variable references resolved.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a referenced environment variable doesn't exist.
    /// </exception>
    private static string ResolveEnvironmentVariables(string value, string contextName)
    {
        var result = value;

        // Find all {ENV_VAR} patterns
        foreach (Match match in EnvVarRegex().Matches(value))
        {
            var envVarName = match.Groups[1].Value;
            var envVarValue = Environment.GetEnvironmentVariable(envVarName);

            if (envVarValue is null)
            {
                throw new InvalidOperationException(
                    $"Environment variable '{envVarName}' referenced in test parameter '{contextName}' is not set. " +
                    $"Original value: {value}"
                );
            }

            result = result.Replace(match.Value, envVarValue);
        }

        return result;
    }

    [GeneratedRegex(@"\{(.*?)\}")]
    private static partial Regex EnvVarRegex();

    #endregion
}
