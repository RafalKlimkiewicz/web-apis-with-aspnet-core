﻿using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

using MyBGList.DTO;
using MyBGList.Models;

using StackExchange.Redis;

namespace MyBGList.Controllers;

[Route("[controller]/[action]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DomainsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApiUser> _userManager;
    private readonly SignInManager<ApiUser> _signInManager;

    public AccountController(ApplicationDbContext context,
        ILogger<DomainsController> logger,
        IConfiguration configuration,
        UserManager<ApiUser> userManager,
        SignInManager<ApiUser> signInManager)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost]
    [ResponseCache(CacheProfileName = "NoCache")]
    public async Task<ActionResult> Register(RegisterDTO input)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var newUser = new ApiUser
                {
                    UserName = input.UserName,
                    Email = input.Email
                };

                var result = await _userManager.CreateAsync(newUser, input.Password);

                if (!result.Succeeded)
                    throw new Exception(string.Format("Error: {0}", string.Join(" ", result.Errors.Select(e => e.Description))));

                _logger.LogInformation("User {userName} ({email}) has been created.", newUser.UserName, newUser.Email);

                return StatusCode(201, $"User '{newUser.UserName}' has been created.");
            }
            else
            {
                return new BadRequestObjectResult(new ValidationProblemDetails(ModelState)
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Status = StatusCodes.Status400BadRequest
                });
            }

        }
        catch (Exception e)
        {
            var exceptionDetails = new ProblemDetails
            {
                Detail = e.Message,
                Status = StatusCodes.Status500InternalServerError,
                Type = "https:/ /tools.ietf.org/html/rfc7231#section-6.6.1"
            };

            return StatusCode(StatusCodes.Status500InternalServerError, exceptionDetails);
            //or
            //return Problem(
            //    detail: e.Message,
            //    statusCode: StatusCodes.Status500InternalServerError,
            //    type: "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            //);

        }
    }

    [HttpPost]
    [ResponseCache(CacheProfileName = "NoCache")]
    public async Task<ActionResult> Login(LoginDTO input)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(input.UserName);

                if (user == null || !await _userManager.CheckPasswordAsync(user, input.Password))
                    throw new Exception("Invalid login attempt.");

                var signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:SigningKey"])), SecurityAlgorithms.HmacSha256);

                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, user.UserName)
                };

                claims.AddRange((await _userManager.GetRolesAsync(user)).Select(r => new Claim(ClaimTypes.Role, r)));

                var jwtObject = new JwtSecurityToken(
                    issuer: _configuration["JWT:Issuer"],
                    audience: _configuration["JWT:Audience"],
                    claims: claims,
                    expires: DateTime.Now.AddSeconds(300),
                    signingCredentials: signingCredentials);

                var jwtString = new JwtSecurityTokenHandler().WriteToken(jwtObject);

                return StatusCode(StatusCodes.Status200OK, jwtString);
            }

            return new BadRequestObjectResult(new ValidationProblemDetails(ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (Exception e)
        {
            var exceptionDetails = new ProblemDetails
            {
                Detail = e.Message,
                Status = StatusCodes.Status401Unauthorized,
                Type = "https:/ /tools.ietf.org/html/rfc7231#section-6.6.1"
            };

            return StatusCode(StatusCodes.Status401Unauthorized, exceptionDetails);
        }
    }

}
