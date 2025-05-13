using Microsoft.AspNetCore.Mvc;

namespace MyBGList.Controllers
{
    [ApiController]
    public class ErrorController : Controller
    {
        [Route("/error/test")]
        [HttpGet]
        public IActionResult Test()
        {
            throw new Exception("test");
        }
    }
}