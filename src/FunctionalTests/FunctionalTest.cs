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
    #region Overrides

    public override BrowserNewContextOptions ContextOptions() =>
        new()
        {
            AcceptDownloads = true,
            ViewportSize = new ViewportSize() { Width = 1280, Height = 720 },
            BaseURL = GetRequiredParameter("webAppUrl")
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
