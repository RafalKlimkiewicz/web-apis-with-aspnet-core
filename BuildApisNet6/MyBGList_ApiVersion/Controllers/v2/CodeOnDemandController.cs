﻿using Asp.Versioning;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace MyBGList_ApiVersion.Controllers.v2;


[ApiController]
[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("2.0")]
public class CodeOnDemandController : ControllerBase
{
    [HttpGet("test")]
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)]
    public ContentResult Test()
    {
        return Content("<script>" +
            "window.alert('Your client supports JavaScript!" +
            "\\r\\n\\r\\n" +
            $"Server time (UTC): {DateTime.UtcNow.ToString("o")}" +
            "\\r\\n" +
            "Client time (UTC): ' + new Date().toISOString());" +
            "</script>" +
            "<noscript>Your client does not support JavaScript</noscript>",
            "text/html");
    }

    [HttpGet("test2")]
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)]
    public ContentResult Test2(int? addMinutes = null)
    {
        var dateTime = DateTime.UtcNow;

        if (addMinutes.HasValue)
            dateTime = dateTime.AddMinutes(addMinutes.Value);

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
