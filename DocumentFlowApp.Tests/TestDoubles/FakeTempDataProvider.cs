using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DocumentFlowApp.Tests.TestDoubles;

internal sealed class FakeTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context)
    {
        return new Dictionary<string, object>();
    }

    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
    }
}
