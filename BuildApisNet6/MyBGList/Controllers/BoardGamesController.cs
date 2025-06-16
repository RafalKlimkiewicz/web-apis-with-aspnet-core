using System.Linq.Dynamic.Core;
using System.Text.Json;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using MyBGList.Constants;
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
        private readonly IMemoryCache _memoryCache;

        public BoardGamesController(ILogger<BoardGamesController> logger, ApplicationDbContext context, IMemoryCache memoryCache)
        {
            _logger = logger;
            _context = context;
            _memoryCache = memoryCache;
        }

        [HttpGet(Name = "GetBoardGamesPagedRequest")]
        [EnableCors("AnyOrigin")]
        [ResponseCache(CacheProfileName = "Any-60")]
        public async Task<ResponseDTO<BoardGame[]>> Get([FromQuery] RequestDTO<BoardGameDTO> input)
        {
            _logger.LogInformation(CustomLogEvents.BoardGamesController_Get, "Get method started at {0:HH:mm}", DateTime.Now);
            _logger.LogInformation(CustomLogEvents.BoardGamesController_Get, "Get method started [{MachineName}] [{ThreadId}].",
                Environment.MachineName, Environment.CurrentManagedThreadId);

            var query = _context.BoardGames.AsQueryable();

            if (!string.IsNullOrEmpty(input.FilterQuery))
                query = query.Where(b => b.Name.StartsWith(input.FilterQuery));

            var cacheKey = $"{input.GetType()}-{JsonSerializer.Serialize(input)}";

            if (!_memoryCache.TryGetValue(cacheKey, out BoardGame[]? result))
            {
                query = query.OrderBy($"{input.SortColumn} {input.SortOrder}").Skip(input.PageIndex * input.PageSize).Take(input.PageSize);
                
                result = await query.ToArrayAsync();

                _memoryCache.Set(cacheKey, result, new TimeSpan(0, 0, 30));
            }

            return new ResponseDTO<BoardGame[]>()
            {
                PageIndex = input.PageIndex,
                PageSize = input.PageSize,
                RecordCount = await query.CountAsync(),
                Data = result ?? Array.Empty<BoardGame>(),
                Links = new List<LinkDTO> { new(Url.Action(null, "BoardGames", new { input.PageIndex, input.PageSize }, Request.Scheme)!, "self", "GET") }
            };
        }

        [HttpPost(Name = "UpdateBoardGame")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<BoardGame?>> Post(BoardGameDTO model)
        {
            var boardgame = await _context.BoardGames.Where(b => b.Id == model.Id).FirstOrDefaultAsync();

            if (boardgame != null)
            {
                if (!string.IsNullOrEmpty(model.Name))
                    boardgame.Name = model.Name;

                if (model.Year.HasValue && model.Year.Value > 0)
                    boardgame.Year = model.Year.Value;

                if (model.MinPlayers.HasValue)
                    boardgame.MinPlayers = model.MinPlayers.Value;

                if (model.MaxPlayers.HasValue)
                    boardgame.MaxPlayers = model.MaxPlayers.Value;

                if (model.MinAge.HasValue)
                    boardgame.MinAge = model.MinAge.Value;

                if (model.PlayTime.HasValue)
                    boardgame.PlayTime = model.PlayTime.Value;

                boardgame.LastModifiedDate = DateTime.Now;

                _context.BoardGames.Update(boardgame);

                await _context.SaveChangesAsync();
            }
            ;

            return new ResponseDTO<BoardGame?>()
            {
                Data = boardgame,
                Links = new List<LinkDTO>
                {
                    new LinkDTO(Url.Action(null,"BoardGames", model,Request.Scheme)!,"self","POST")
                }
            };
        }

        [HttpDelete(Name = "DeleteBoardGame")]
        [ResponseCache(NoStore = true)]
        public async Task<ResponseDTO<BoardGame[]?>> Delete(string idList)
        {
            var ids = idList.Split(",").Select(id => int.Parse(id)).ToList();
            var deletedBGList = new List<BoardGame>();


            foreach (var id in ids)
            {
                var boardgame = await _context.BoardGames.Where(b => ids.Contains(b.Id)).FirstOrDefaultAsync();

                if (boardgame != null)
                {
                    deletedBGList.Add(boardgame);
                    _context.BoardGames.Remove(boardgame);
                    await _context.SaveChangesAsync();
                }
                ;
            }


            return new ResponseDTO<BoardGame[]?>()
            {
                Data = deletedBGList.Count > 0 ? deletedBGList.ToArray() : null,
                Links = new List<LinkDTO>
                {
                    new LinkDTO(Url.Action(null, "BoardGames", ids, Request.Scheme)!,"self","DELETE"),
                }
            };
        }
    }
}
