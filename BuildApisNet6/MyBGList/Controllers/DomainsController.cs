﻿using System.Diagnostics;
using System.Linq.Dynamic.Core;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MyBGList.Attributes;
using MyBGList.DTO;
using MyBGList.Models;

namespace MyBGList.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class DomainsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly ILogger<DomainsController> _logger;

        public DomainsController(ApplicationDbContext context,
            ILogger<DomainsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet(Name = "GetDomains")]
        [ResponseCache(Location = ResponseCacheLocation.Any, Duration = 60)]
        [EnableCors("AnyOrigin")]
        [ManualValidationFilter]
        public async Task<ActionResult<ResponseDTO<Domain[]>>> Get([FromQuery] RequestDTO<DomainDTO> input)
        {
            if (!ModelState.IsValid)
            {
                var details = new ValidationProblemDetails(ModelState);
                details.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

                if (ModelState.Keys.Any(k => k == "PageSize"))
                {
                    details.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.2";
                    details.Status = StatusCodes.Status501NotImplemented;

                    return new ObjectResult(details)
                    {
                        StatusCode = StatusCodes.Status501NotImplemented
                    };
                }
                else
                {
                    details.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                    details.Status = StatusCodes.Status400BadRequest;

                    return new BadRequestObjectResult(details);
                }
            }

            var query = _context.Domains.AsQueryable();
            if (!string.IsNullOrEmpty(input.FilterQuery))
                query = query.Where(b => b.Name.Contains(input.FilterQuery));

            var recordCount = await query.CountAsync();

            query = query.OrderBy($"{input.SortColumn} {input.SortOrder}")
                    .Skip(input.PageIndex * input.PageSize)
                    .Take(input.PageSize);

            return new ResponseDTO<Domain[]>()
            {
                Data = await query.ToArrayAsync(),
                PageIndex = input.PageIndex,
                PageSize = input.PageSize,
                RecordCount = recordCount,
                Links = new List<LinkDTO> {
                    new LinkDTO(Url.Action(null,"Domains",new { input.PageIndex, input.PageSize },Request.Scheme)!,"self","GET"),
                }
            };
        }

        [Authorize]
        [HttpPost(Name = "UpdateDomain")]
        [ResponseCache(NoStore = true)]
        //[EnableCors("AnyOrigin")]
        [ManualValidationFilter]
        public async Task<ActionResult<ResponseDTO<Domain?>>> Post(DomainDTO model)
        {
            if (!ModelState.IsValid)
            {
                var details = new ValidationProblemDetails(ModelState);

                details.Extensions["traceId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

                if (model.Id != 3 && model.Name != "Wargames")
                {
                    details.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3";
                    details.Status = StatusCodes.Status403Forbidden;

                    return new ObjectResult(details)
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                }
                else
                {
                    details.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                    details.Status = StatusCodes.Status400BadRequest;

                    return new BadRequestObjectResult(details)
                    {
                        StatusCode = StatusCodes.Status400BadRequest
                    };
                }
            }

            var domain = await _context.Domains.Where(b => b.Id == model.Id).FirstOrDefaultAsync();

            if (domain != null)
            {
                if (!string.IsNullOrEmpty(model.Name))
                    domain.Name = model.Name;

                domain.LastModifiedDate = DateTime.Now;

                _context.Domains.Update(domain);

                await _context.SaveChangesAsync();
            }

            return new ResponseDTO<Domain?>()
            {
                Data = domain,
                Links = new List<LinkDTO>
                {
                    new LinkDTO(Url.Action(null,"Domains",model,Request.Scheme)!,"self","POST"),
                }
            };
        }

        [Authorize]
        [HttpDelete(Name = "DeleteDomain")]
        [ResponseCache(NoStore = true)]
        //[EnableCors("AnyOrigin")]
        public async Task<ResponseDTO<Domain?>> Delete(int id)
        {
            var domain = await _context.Domains.Where(b => b.Id == id).FirstOrDefaultAsync();

            if (domain != null)
            {
                _context.Domains.Remove(domain);
                await _context.SaveChangesAsync();
            }

            return new ResponseDTO<Domain?>()
            {
                Data = domain,
                Links = new List<LinkDTO>
                {
                    new LinkDTO(Url.Action(null,"Domains",id,Request.Scheme)!,"self","DELETE")
                }
            };
        }
    }
}