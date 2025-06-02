using System.Linq.Dynamic.Core;

using Microsoft.AspNetCore.Cors;
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

        [HttpGet("paged", Name = "GetBoardGamesPaged")]
        [EnableCors("AnyOrigin")]
        [ResponseCache(Location = ResponseCacheLocation.Any, Duration = 60)]
        public async Task<RestDTO<BoardGame[]>> GetPaged(int pageIndex = 0, int pageSize = 10, string? sortColumn = "Name", string? sortOrder = "ASC", string? filterQuery = null)
        {
            var query = _context.BoardGames.AsQueryable();

            if (!string.IsNullOrEmpty(filterQuery))
                query = query.Where(b => b.Name.StartsWith(filterQuery));

            var recordCount = await query.CountAsync();

            query = query.OrderBy($"{sortColumn} {sortOrder}")
                .Skip(pageIndex * pageSize)
                .Take(pageSize);


            return new RestDTO<BoardGame[]>()
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                RecordCount = recordCount,
                Data = await query.ToArrayAsync(),
                Links = new List<LinkDTO> { new(Url.Action(null, "BoardGames", new { pageIndex, pageSize }, Request.Scheme)!, "self", "GET") }
            };
        }

        [HttpPost(Name = "UpdateBoardGame")]
        [ResponseCache(NoStore = true)]
        public async Task<RestDTO<BoardGame?>> Post(BoardGameDTO model)
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

            return new RestDTO<BoardGame?>()
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
        public async Task<RestDTO<BoardGame[]?>> Delete(string idList)
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


            return new RestDTO<BoardGame[]?>()
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
