using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace jcoliz.FunctionalTests;

public class BuiltInSteps(IBaseStepCapabilities context)
{
    protected async Task UserHasLaunchedTheSite()
    {
        await UserLaunchesSite();
        await ThenPageLoadedOk();
    }

    /// <summary>
    /// Launch the site under test.
    /// </summary>
    protected async Task UserLaunchesSite()
    {
        var pageModel = context.GetOrCreatePage<PageObjectModel>();

        var result = await pageModel.LaunchSite();

        context.ObjectStore.Add( result! );
    }

    /// <summary>
    /// When reloading the current page
    /// </summary>
    /// <remarks>
    /// REQUIRES: CurrentPage in object store
    /// </remarks>
    public async Task ReloadingTheCurrentPage()
    {
        // We need the exact KIND of page model here, because the reload page logic
        // will check to ensure the RIGHT page was reloaded.
        var pageModel = context.ObjectStore.Get<PageObjectModel>("CurrentPage");
        await pageModel.ReloadPageAsync();
    }

    /// <summary>
    /// Confirms the last page load was successful by checking the response is Ok. 
    /// </summary>
    protected Task ThenPageLoadedOk()
    {
        var response = context.ObjectStore.Get<IResponse>();

        Assert.That(response!.Ok, Is.True);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Saves a screenshot of the current page state. The screenshot is saved with a default name and includes the full page.
    /// </summary>
    protected async Task SaveAScreenshot()
    {
        var pageModel = context.GetOrCreatePage<PageObjectModel>();
        await pageModel.SaveScreenshotAsync();
    }

    /// <summary>
    /// Then save a screenshot named "ChooseStore"
    /// </summary>
    public async Task SaveAScreenshotNamed(string name)
    {
        var pageModel = context.GetOrCreatePage<PageObjectModel>();
        await pageModel.SaveScreenshotAsync(moment: name, fullPage: false);
    }
}