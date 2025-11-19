using ContaminaDOS.Business;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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


        public class CreateGameRequest
        {
            public string name { get; set; } = string.Empty;
            public string owner { get; set; } = string.Empty;
            public string? password { get; set; }
        }

        public class ApiResponse
        {
            public int status { get; set; }
            public string msg { get; set; } = "";
            public object data { get; set; } = new { };
            public object others { get; set; } = new { };
        }


        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGameRequest req)
        {
            try
            {
                var game = await _business.CreateGameAsync(
                    req.name,
                    req.owner,
                    req.password
                );

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Game Created",
                    data = new
                    {
                        name = game.Name,
                        owner = game.Owner,
                        status = game.Status,
                        createdAt = game.CreatedAt,
                        updatedAt = game.UpdatedAt,
                        password = game.Password,
                        players = game.Players,
                        enemies = game.Enemies,
                        currentRound = game.CurrentRound,
                        id = game.Id
                    },
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

        // PUT /api/games/{gameId}/
        [HttpPut("{gameId}")]
        public async Task<IActionResult> JoinGame(
            string gameId,
            [FromHeader(Name = "player"), BindRequired] string playerHeader,
            [FromHeader(Name = "password")] string? passwordHeader)
        {
            try
            {
                var game = await _business.JoinGameAsync(gameId, playerHeader, passwordHeader);

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Joined Game",
                    data = game,
                    others = new { }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    status = 400,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
        }



    }
}
