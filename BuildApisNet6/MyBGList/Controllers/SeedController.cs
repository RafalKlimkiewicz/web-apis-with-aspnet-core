﻿using System.Globalization;

using CsvHelper;
using CsvHelper.Configuration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MyBGList.Constants;
using MyBGList.Models;
using MyBGList.Models.Csv;

using Path = System.IO.Path;

namespace MyBGList.Controllers;

[Authorize]
[Route("[controller]/[action]")]
[ApiController]
public class SeedController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SeedController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApiUser> _userManager;

    public SeedController(ApplicationDbContext context,
        ILogger<SeedController> logger,
        IWebHostEnvironment env,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApiUser> userManager)
    {
        _context = context;
        _logger = logger;
        _env = env;
        _roleManager = roleManager;
        _userManager = userManager;
    }

    [Authorize(Roles = RoleNames.Administrator)]
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true)]
    public async Task<ActionResult<SeedResult>> BoardGameData(int? id = null)
    {
        var config = new CsvConfiguration(CultureInfo.GetCultureInfo("pt-BR"))
        {
            HasHeaderRecord = true,
            Delimiter = ";",
        };

        using var reader = new StreamReader(Path.Combine(_env.ContentRootPath, "Data/bgg_dataset.csv"));
        using var csv = new CsvReader(reader, config);

        var existingBoardGames = await _context.BoardGames.ToDictionaryAsync(bg => bg.Id);
        var existingDomains = await _context.Domains.ToDictionaryAsync(d => d.Name);
        var existingMechanics = await _context.Mechanics.ToDictionaryAsync(m => m.Name);

        var now = DateTime.Now;

        var records = csv.GetRecords<BggRecord>();
        var skippedRows = 0;

        foreach (var record in records)
        {
            if (!record.ID.HasValue || string.IsNullOrEmpty(record.Name)
                || existingBoardGames.ContainsKey(record.ID.Value)
                || (id.HasValue && id.Value != record.ID.Value))
            {
                skippedRows++;
                continue;
            }

            var boardgame = new BoardGame()
            {
                Id = record.ID.Value,
                Name = record.Name,
                BGGRank = record.BGGRank ?? 0,
                ComplexityAverage = record.ComplexityAverage ?? 0,
                MaxPlayers = record.MaxPlayers ?? 0,
                MinAge = record.MinAge ?? 0,
                MinPlayers = record.MinPlayers ?? 0,
                OwnedUsers = record.OwnedUsers ?? 0,
                PlayTime = record.PlayTime ?? 0,
                RatingAverage = record.RatingAverage ?? 0,
                UsersRated = record.UsersRated ?? 0,
                Year = record.YearPublished ?? 0,
                CreatedDate = now,
                LastModifiedDate = now,
            };

            _context.BoardGames.Add(boardgame);

            if (!string.IsNullOrEmpty(record.Domains))
            {
                foreach (var domainName in record.Domains.Split(',', StringSplitOptions.TrimEntries).Distinct(StringComparer.InvariantCultureIgnoreCase))
                {
                    var domain = existingDomains.GetValueOrDefault(domainName);

                    if (domain == null)
                    {
                        domain = new Domain()
                        {
                            Name = domainName,
                            CreatedDate = now,
                            LastModifiedDate = now
                        };

                        _context.Domains.Add(domain);
                        existingDomains.Add(domainName, domain);
                    }

                    _context.BoardGames_Domains.Add(new BoardGames_Domains()
                    {
                        BoardGame = boardgame,
                        Domain = domain,
                        CreatedDate = now
                    });
                }
            }

            if (!string.IsNullOrEmpty(record.Mechanics))
            {
                foreach (var mechanicName in record.Mechanics.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.InvariantCultureIgnoreCase))
                {
                    var mechanic = existingMechanics.GetValueOrDefault(mechanicName);

                    if (mechanic == null)
                    {
                        mechanic = new Mechanic()
                        {
                            Name = mechanicName,

                            CreatedDate = now,
                            LastModifiedDate = now
                        };

                        _context.Mechanics.Add(mechanic);
                        existingMechanics.Add(mechanicName, mechanic);
                    }

                    _context.BoardGames_Mechanics.Add(new BoardGames_Mechanics()
                    {
                        BoardGame = boardgame,
                        Mechanic = mechanic,
                        CreatedDate = now
                    });
                }
            }
        }

        using var transaction = _context.Database.BeginTransaction();

        _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT BoardGames ON");

        await _context.SaveChangesAsync();

        _context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT BoardGames OFF");

        transaction.Commit();

        return Ok(new SeedResult(
            BoardGames: await _context.BoardGames.CountAsync(),
            Domains: await _context.Domains.CountAsync(),
            Mechanics: await _context.Mechanics.CountAsync(),
            SkippedRows: skippedRows
        ));
    }

    [Authorize(Roles = RoleNames.Administrator)]
    [AllowAnonymous]
    [HttpPost]
    [ResponseCache(NoStore = true)]
    public async Task<IActionResult> AuthData()
    {
        int rolesCreated = 0;
        int usersAddedToRoles = 0;

        if (!await _roleManager.RoleExistsAsync(RoleNames.Moderator))
        {
            await _roleManager.CreateAsync(new IdentityRole(RoleNames.Moderator));

            rolesCreated++;
        }

        if (!await _roleManager.RoleExistsAsync(RoleNames.Administrator))
        {
            await _roleManager.CreateAsync(new IdentityRole(RoleNames.Administrator)); 

            rolesCreated++;
        }

        if (!await _roleManager.RoleExistsAsync(RoleNames.SuperAdmin))
        {
            await _roleManager.CreateAsync(new IdentityRole(RoleNames.SuperAdmin));

            rolesCreated++;
        }

        var testModerator = await _userManager.FindByNameAsync("TestModerator");

        if (testModerator != null && !await _userManager.IsInRoleAsync(testModerator, RoleNames.Moderator))
        {
            await _userManager.AddToRoleAsync(testModerator, RoleNames.Moderator);

            usersAddedToRoles++;
        }

        var testAdministrator = await _userManager.FindByNameAsync("TestAdministrator");

        if (testAdministrator != null && !await _userManager.IsInRoleAsync(testAdministrator, RoleNames.Administrator))
        {
            await _userManager.AddToRoleAsync(testAdministrator, RoleNames.Moderator);
            await _userManager.AddToRoleAsync(testAdministrator, RoleNames.Administrator);

            usersAddedToRoles++;
        }

        var testSuperAdministrator = await _userManager.FindByNameAsync("TestSuperAdministrator");

        if (testSuperAdministrator != null && !await _userManager.IsInRoleAsync(testSuperAdministrator, RoleNames.Administrator))
        {
            await _userManager.AddToRoleAsync(testSuperAdministrator, RoleNames.Moderator);
            await _userManager.AddToRoleAsync(testSuperAdministrator, RoleNames.Administrator);
            await _userManager.AddToRoleAsync(testSuperAdministrator, RoleNames.SuperAdmin);

            usersAddedToRoles++;
        }
        

        return new JsonResult(new
        {
            RolesCreated = rolesCreated,
            UsersAddedToRoles = usersAddedToRoles
        });
    }

    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true)]
    public ActionResult<string> Test(string input)
    {
        return Content(input, "text/plain");
    }
}

public record SeedResult(int BoardGames, int Domains, int Mechanics, int SkippedRows);
