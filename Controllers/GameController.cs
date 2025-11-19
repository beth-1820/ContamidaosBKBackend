using ContaminaDOS.Business;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.InteropServices;

namespace ContaminaDOS.Api.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GamesController : ControllerBase
    {
        private readonly GameBusiness _business;

        public GamesController(GameBusiness business)
        {
            _business = business;
        }

        // -------------------------------
        // POST /api/games
        // -------------------------------
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGameRequest req)
        {
            try
            {
                // Validaciones básicas
                if (string.IsNullOrWhiteSpace(req.name) ||
                    string.IsNullOrWhiteSpace(req.owner))
                {
                    return BadRequest(new ApiResponse
                    {
                        status = 400,
                        msg = "Client Error",
                        data = new { },
                        others = new { }
                    });
                }

                // Crear juego mediante la capa business
                var game = await _business.CreateGameAsync(
                    req.name,
                    req.owner,
                    req.password
                );

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Game Created",
                    data = game,
                    others = new { }
                });
            }
            catch (Exception ex)
            {
                return Conflict(new ApiResponse
                {
                    status = 409,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
        }

        // -------------------------------
        // REQUEST MODEL
        // -------------------------------
        public class CreateGameRequest
        {
            public string name { get; set; } = string.Empty;
            public string owner { get; set; } = string.Empty;
            public string? password { get; set; }
        }

        // -------------------------------
        // RESPONSE MODEL (Swagger style)
        // -------------------------------
        public class ApiResponse
        {
            public int status { get; set; }
            public string msg { get; set; } = "";
            public object data { get; set; } = new { };
            public object others { get; set; } = new { };
        }
    }
}
