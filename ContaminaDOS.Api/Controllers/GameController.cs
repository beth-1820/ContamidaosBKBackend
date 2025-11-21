using ContaminaDOS.Business;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;

namespace ContaminaDOS.Api.Controllers
{
    [ApiController]
    [Route("api/games")]

    public class GamesController : ControllerBase
    {
        private readonly GameBusiness _business;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum GameStatus
        {
            lobby,
            rounds,
            ended
        }

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
            [FromHeader(Name = "password")] string? passwordHeader,
            [FromHeader(Name = "player"), BindRequired] string playerHeader)
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

        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> SearchGames(
            [FromQuery] string? name,
            [FromQuery] GameStatus? status,
            [FromQuery] int page = 0,
            [FromQuery] int limit = 50)
        {
            var games = await _business.SearchGamesAsync(
                name,
                status?.ToString(),
                page,
                limit
            );

            var result = games.Select(g => new
            {
                name = g.Name,
                owner = g.Owner,
                status = g.Status,
                createdAt = g.CreatedAt,
                updatedAt = g.UpdatedAt,
                password = g.Password,
                players = g.Players,
                enemies = g.Enemies,
                currentRound = g.CurrentRound,
                id = g.Id
            });

            return Ok(new ApiResponse
            {
                status = 200,
                msg = $"Search returned {games.Count} result",
                data = result,
                others = new { }
            });
        }


        // GET /api/games/{gameId}/
        [HttpGet("{gameId}")]
        public async Task<IActionResult> GetGame(
            string gameId,
            [FromHeader] string? password,
            [FromHeader, BindRequired] string player)
        {
            var game = await _business.GetGameByIdAsync(gameId);
            if (game == null)
            {
                return NotFound(new
                {
                    msg = "The specified resource was not found",
                    status = 404
                });
            }

            if (game.Password == true && string.IsNullOrEmpty(password))
            {
                return Unauthorized(new
                {
                    msg = "Invalid credentials",
                    status = 401
                });
            }

            if (!game.Players.Contains(player))
            {
                return StatusCode(403, new
                {
                    msg = "Not part of the game",
                    status = 403
                });
            }

            var enemiesList = new List<string>();
            if (game.Enemies.Contains(player))
                enemiesList.Add(player);

            return Ok(new
            {
                status = 200,
                msg = "Game Found",
                data = new
                {
                    name = game.Name,
                    owner = game.Owner,
                    status = game.Status,
                    createdAt = game.CreatedAt,
                    updatedAt = game.UpdatedAt,
                    password = game.Password,
                    players = game.Players,
                    enemies = enemiesList,
                    currentRound = game.CurrentRound,
                    id = gameId
                }
            });
        }

        [HttpHead("{gameId}/start")]
        public async Task<IActionResult> StartGame(
        string gameId,
        [FromHeader(Name = "password")] string? passwordHeader,
        [FromHeader(Name = "player"), BindRequired] string playerHeader)
        {
            try
            {
                await _business.StartGameAsync(gameId, playerHeader, passwordHeader);
                return Ok();
            }
            catch (KeyNotFoundException ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                return Unauthorized();
            }
            catch (InvalidOperationException ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                return StatusCode(428);
            }
            catch (Exception ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                return Conflict();
            }
        }



    }
}
