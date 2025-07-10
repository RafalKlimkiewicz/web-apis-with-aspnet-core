using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

using MyBGList.gRPC;

namespace MyBGList.Controllers;

[Route("[controller]/[action]")]
[EnableCors("AnyOrigin")]
[ApiController]
public class GrpcController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetBoardGame(int id)
    {
        using var channel = GrpcChannel.ForAddress("https://localhost:40443");

        var client = new gRPC.Grpc.GrpcClient(channel);
        var response = await client.GetBoardGameAsync(new BoardGameRequest { Id = id });

        var result = new
        {
            response.Id,
            response.Name,
            response.Year
        };

        return Ok(result);
    }

    [HttpPost]
    public async Task<BoardGameResponse> UpdateBoardGame(string token, int id, string name)
    {
        var headers = new Metadata
        {
            { "Authorization", $"Bearer {token}" }
        };

        using var channel = GrpcChannel.ForAddress("https://localhost:40443");

        var client = new gRPC.Grpc.GrpcClient(channel);
        var response = await client.UpdateBoardGameAsync(new UpdateBoardGameRequest
        {
            Id = id,
            Name = name
        }, headers);

        return response;
    }
}
