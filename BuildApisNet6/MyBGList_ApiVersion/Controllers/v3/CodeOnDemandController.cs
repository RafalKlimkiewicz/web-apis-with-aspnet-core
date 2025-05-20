using Asp.Versioning;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace MyBGList_ApiVersion.Controllers.v3;

[ApiController]
[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("3.0")]
public class CodeOnDemandController : ControllerBase
{
    [HttpGet("Test2")]
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)]
    public ContentResult Test2(int? minutesToAdd = null)
    {
        var dateTime = DateTime.UtcNow;
        if (minutesToAdd.HasValue)
            dateTime = dateTime.AddMinutes(minutesToAdd.Value);

        return Content("<script>" +
            "window.alert('Your client supports JavaScript!" +
            "\\r\\n\\r\\n" +
            $"Server time (UTC): {dateTime.ToString("o")}" +
            "\\r\\n" +
            "Client time (UTC): ' + new Date().toISOString());" +
            "</script>" +
            "<noscript>Your client does not support JavaScript</noscript>",
            "text/html");
    }
}
