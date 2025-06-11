using System.Linq.Dynamic.Core;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        public BoardGamesController(ILogger<BoardGamesController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet(Name = "GetBoardGamesPagedRequest")]
        [EnableCors("AnyOrigin")]
        [ResponseCache(Location = ResponseCacheLocation.Any, Duration = 60)]
        public async Task<ResponseDTO<BoardGame[]>> Get([FromQuery] RequestDTO<BoardGameDTO> input)
        {
            _logger.LogInformation(CustomLogEvents.BoardGamesController_Get, "Get method started at {0:HH:mm}", DateTime.Now);
            _logger.LogInformation(CustomLogEvents.BoardGamesController_Get, "Get method started [{MachineName}] [{ThreadId}].",
                Environment.MachineName, Environment.CurrentManagedThreadId);

            var query = _context.BoardGames.AsQueryable();

            if (!string.IsNullOrEmpty(input.FilterQuery))
                query = query.Where(b => b.Name.StartsWith(input.FilterQuery));

            var recordCount = await query.CountAsync();

            query = query.OrderBy($"{input.SortColumn} {input.SortOrder}").Skip(input.PageIndex * input.PageSize).Take(input.PageSize);

            return new ResponseDTO<BoardGame[]>()
            {
                PageIndex = input.PageIndex,
                PageSize = input.PageSize,
                RecordCount = recordCount,
                Data = await query.ToArrayAsync(),
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
