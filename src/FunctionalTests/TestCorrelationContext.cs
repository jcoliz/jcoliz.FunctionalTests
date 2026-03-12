using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using Microsoft.Playwright;
using NUnit.Framework;

namespace jcoliz.FunctionalTests;

/// <summary>
/// Encapsulates a <see cref="Activity"/> for test-log correlation, providing
/// W3C Trace Context and custom test metadata headers.
/// </summary>
/// <remarks>
/// This class owns the lifecycle of a <see cref="System.Diagnostics.Activity"/>
/// that links functional test execution to backend API logs via distributed tracing.
/// It generates both standard W3C <c>traceparent</c> headers and custom <c>X-Test-*</c>
/// headers for correlation.
///
/// Usage:
/// <code>
/// using var context = new TestCorrelationContext(TestContext.CurrentContext.Test);
/// var headers = context.BuildCorrelationHeaders();
/// // Attach headers to HTTP requests
/// </code>
/// </remarks>
public sealed class TestCorrelationContext : IDisposable
{
    private readonly Activity _activity;
    private readonly string _testName;
    private readonly string _testClass;
    private readonly string _testId;

    /// <summary>
    /// Creates a new test correlation context and starts the underlying Activity.
    /// </summary>
    /// <param name="test">The NUnit test adapter providing test name and class information.</param>
    public TestCorrelationContext(TestContext.TestAdapter test)
    {
        _testName = test.Name;
        _testClass = test.ClassName ?? "Unknown";
        _testId = Guid.NewGuid().ToString();

        _activity = new Activity("FunctionalTest");
        _activity.SetTag("test.name", _testName);
        _activity.SetTag("test.class", _testClass);
        _activity.SetTag("test.id", _testId);
        _activity.Start();
    }

    /// <summary>
    /// Builds HTTP headers for test correlation including W3C Trace Context
    /// and custom test metadata.
    /// </summary>
    /// <returns>
    /// Dictionary containing:
    /// <list type="bullet">
    ///   <item><c>traceparent</c> — W3C Trace Context header linking requests to this test's trace</item>
    ///   <item><c>X-Test-Name</c> — URL-encoded test method name</item>
    ///   <item><c>X-Test-Id</c> — Unique identifier for this test execution</item>
    ///   <item><c>X-Test-Class</c> — Fully-qualified test class name</item>
    /// </list>
    /// </returns>
    public Dictionary<string, string> BuildCorrelationHeaders(string client = "Playwright")
    {
        var traceParent = $"00-{_activity.TraceId}-{_activity.SpanId}-01";

        return new Dictionary<string, string>
        {
            // W3C Trace Context standard
            ["traceparent"] = traceParent,

            // Direct test correlation (fallback and convenience)
            ["X-Test-Name"] = HttpUtility.UrlEncode(_testName),
            ["X-Test-Id"] = _testId,
            ["X-Test-Class"] = _testClass,
            ["X-Test-Client"] = client
        };
    }

    /// <summary>
    /// Builds a Playwright cookie for test-name correlation.
    /// </summary>
    /// <param name="domain">The domain to set the cookie on (typically the web app host).</param>
    /// <returns>A <see cref="Cookie"/> with name <c>x-test-name</c> containing the URL-encoded test name.</returns>
    public Cookie BuildCorrelationCookie(string domain)
    {
        return new Cookie()
        {
            Name = "x-test-name",
            Value = HttpUtility.UrlEncode(_testName),
            Domain = domain,
            Path = "/"
        };
    }

    /// <summary>
    /// Stops and disposes the underlying Activity.
    /// </summary>
    public void Dispose()
    {
        _activity.Stop();
        _activity.Dispose();
    }
}
