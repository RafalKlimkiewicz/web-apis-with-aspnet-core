using System.Linq.Dynamic.Core;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Distributed;

using MyBGList.DTO;
using MyBGList.Extensions;
using MyBGList.Models;

namespace MyBGList.Controllers;

[Route("[controller]")]
[ApiController]
public class MechanicsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MechanicsController> _logger;
    private readonly IDistributedCache _distributedCache;

    public MechanicsController(ApplicationDbContext context, ILogger<MechanicsController> logger, IDistributedCache distributedCache)
    {
        _context = context;
        _logger = logger;
        _distributedCache = distributedCache;
    }

    [HttpGet(Name = "GetMechanics")]
    [ResponseCache(Location = ResponseCacheLocation.Any, Duration = 60)]
    public async Task<ResponseDTO<Mechanic[]>> Get([FromQuery] RequestDTO<MechanicDTO> input)
    {
        var query = _context.Mechanics.AsQueryable();

        if (!string.IsNullOrEmpty(input.FilterQuery))
            query = query.Where(b => b.Name.Contains(input.FilterQuery));

        var cacheKey = $"{input.GetType()}-{JsonSerializer.Serialize(input)}";

        if (!_distributedCache.TryGetValue(cacheKey, out Mechanic[]? result))
        {
            query = query
                .OrderBy($"{input.SortColumn} {input.SortOrder}")
                .Skip(input.PageIndex * input.PageSize)
                .Take(input.PageSize);

            result = await query.ToArrayAsync();

            _distributedCache.Set(cacheKey, result, new TimeSpan(0, 0, 30));
        }

        return new ResponseDTO<Mechanic[]>()
        {
            Data = result ?? Array.Empty<Mechanic>(),
            PageIndex = input.PageIndex,
            PageSize = input.PageSize,
            RecordCount = await query.CountAsync(),
            Links = new List<LinkDTO> { new(Url.Action(null, "Mechanics", new { input.PageIndex, input.PageSize }, Request.Scheme)!, "self", "GET") }
        };
    }

    [Authorize]
    [HttpPost(Name = "UpdateMechanic")]
    [ResponseCache(NoStore = true)]
    public async Task<ResponseDTO<Mechanic?>> Post(MechanicDTO model)
    {
        var mechanic = await _context.Mechanics.Where(b => b.Id == model.Id).FirstOrDefaultAsync();

        if (mechanic != null)
        {
            if (!string.IsNullOrEmpty(model.Name))
                mechanic.Name = model.Name;

            mechanic.LastModifiedDate = DateTime.Now;

            _context.Mechanics.Update(mechanic);

            await _context.SaveChangesAsync();
        }
        ;

        return new ResponseDTO<Mechanic?>()
        {
            Data = mechanic,
            Links = new List<LinkDTO> { new LinkDTO(Url.Action(null, "Mechanics", model, Request.Scheme)!, "self", "POST") }
        };
    }

    [Authorize]
    [HttpDelete(Name = "DeleteMechanic")]
    [ResponseCache(NoStore = true)]
    public async Task<ResponseDTO<Mechanic?>> Delete(int id)
    {
        var mechanic = await _context.Mechanics.Where(b => b.Id == id).FirstOrDefaultAsync();

        if (mechanic != null)
        {
            _context.Mechanics.Remove(mechanic);

            await _context.SaveChangesAsync();
        }
        ;

        return new ResponseDTO<Mechanic?>()
        {
            Data = mechanic,
            Links = new List<LinkDTO> { new LinkDTO(Url.Action(null, "Mechanics", id, Request.Scheme)!, "self", "DELETE") }
        };
    }
}