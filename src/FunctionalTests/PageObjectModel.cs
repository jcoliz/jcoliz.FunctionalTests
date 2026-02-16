using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace jcoliz.FunctionalTests;

/// <summary>
/// Base class for page object models.
/// </summary>
/// <remarks>
/// Provides common cross-domain functionality for page object models. Inherit from this class to create a page object model.
/// </remarks>
public abstract partial class PageObjectModel(IPage page)
{
    /// <summary>
    /// Gets the page title from the browser
    /// </summary>
    public async Task<string> GetPageTitle()
    {
        return await page.TitleAsync();
    }

    /// <summary>
    /// Navigates to the site root
    /// </summary>
    public async Task<IResponse> LaunchSite()
    {
        var result = await page.GotoAsync("/");

        return result!;
    }

    /// <summary>
    /// Checks if a control is available for interaction (both visible and enabled).
    /// </summary>
    /// <param name="locator">The locator for the control to check</param>
    /// <returns>True if the control is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This method abstracts the implementation detail of whether a control is unavailable
    /// due to being hidden or disabled. It provides a unified way to check if a control
    /// can be interacted with, which is particularly useful for permission-based scenarios
    /// where controls may be hidden or disabled based on user roles.
    /// </remarks>
    protected async Task<bool> IsAvailableAsync(ILocator locator)
    {
        var isVisible = await locator.IsVisibleAsync();
        if (!isVisible) return false;
        return await locator.IsEnabledAsync();
    }

    /// <summary>
    /// Waits until a locator becomes enabled
    /// </summary>
    /// <param name="locator">The locator to wait for</param>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// First ensures the element is attached and visible, then polls until it becomes enabled.
    /// This is useful for waiting for Vue.js client-side hydration to complete.
    /// TODO: Move to Base Page
    /// </remarks>
    public async Task WaitForEnabled(ILocator locator, float timeout = 5000)
    {
        await locator.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = timeout });
        await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

        // Poll until the element is enabled
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var isDisabled = await locator.IsDisabledAsync();
            if (!isDisabled)
            {
                return; // Element is now enabled
            }
            await Task.Delay(50);
        }

        throw new TimeoutException($"Locator did not become enabled within {timeout}ms");
    }


    /// <summary>
    /// Executes an action and waits for a matching API response
    /// </summary>
    /// <param name="action">Action that triggers the API call</param>
    /// <param name="regex">Regex pattern to match the API endpoint</param>
    protected async Task WaitForApi(Func<Task> action, Regex regex)
    {
        var response = await page!.RunAndWaitForResponseAsync(action, regex);
        TestContext.Out.WriteLine("API request {0}", response.Url);
    }

    #region Screenshot Helpers

    /// <summary>
    /// Captures a screenshot of the current page
    /// </summary>
    /// <param name="moment">Optional moment identifier for the screenshot filename</param>
    /// <param name="fullPage">Whether to capture the full page or just the viewport</param>
    public async Task SaveScreenshotAsync(string? moment = null, bool fullPage = true)
    {
        // TODO: Centralize test context parameters in a single class to avoid scattering TestContext.Parameters calls throughout the codebase
        var context = TestContext.Parameters["screenshotContext"] ?? "Local";
        var testclassfull = $"{TestContext.CurrentContext.Test.ClassName}";
        var testclass = testclassfull.Split(".").Last();
        var testname = MakeValidFileName($"{TestContext.CurrentContext.Test.Name}");
        var displaymoment = string.IsNullOrEmpty(moment) ? string.Empty : $"-{moment.Replace('/','-')}";
        var filename = $"Screenshot/{context}/{testclass}/{testname}{displaymoment}.png";
        await page.ScreenshotAsync(new PageScreenshotOptions() { Path = filename, OmitBackground = true, FullPage = fullPage });
        TestContext.AddTestAttachment(filename);
    }

    // https://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
    private static string MakeValidFileName( string name )
    {
        var invalidChars = System.Text.RegularExpressions.Regex.Escape( new string( System.IO.Path.GetInvalidFileNameChars() ) );
        var invalidRegStr = string.Format( @"([{0}]*\.+$)|([{0}]+)", invalidChars );

        return Regex.Replace( name, invalidRegStr, "_" );
    }

    #endregion

}
