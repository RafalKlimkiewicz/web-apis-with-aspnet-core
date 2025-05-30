﻿using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MyBGList.DTO;
using MyBGList.Models;

namespace MyBGList.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class BoardGamesController : ControllerBase
    {
        private readonly ILogger<BoardGamesController> _logger;
        private readonly ApplicationDbContext _context;

        public BoardGamesController(ILogger<BoardGamesController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet(Name = "GetBoardGames")]
        [EnableCors("AnyOrigin")]
        [ResponseCache(Location = ResponseCacheLocation.Any, Duration = 60)]
        public async Task<RestDTO<BoardGame[]>> Get()
        {
            var query = _context.BoardGames;

            return new RestDTO<BoardGame[]>()
            {
                Data = await query.ToArrayAsync(),
                Links = new List<LinkDTO> { new(Url.Action(null, "BoardGames", null, Request.Scheme)!, "self", "GET") }
            };
        }
    }
}
