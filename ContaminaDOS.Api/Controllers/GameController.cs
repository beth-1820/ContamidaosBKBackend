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

        public GamesController(GameBusiness business)
        {
            _business = business;
        }


        [JsonConverter(typeof(JsonStringEnumConverter))]
        public enum GameStatus
        {
            lobby,
            rounds,
            ended
        }

        public class CreateGameRequest
        {
            public string name { get; set; } = string.Empty;
            public string owner { get; set; } = string.Empty;
            public string? password { get; set; }
        }

        public class JoinGameBody
        {
            public string player { get; set; } = string.Empty;
            public string? password { get; set; }
        }

        public class ProposeGroupRequest
        {
            public List<string> group { get; set; } = new();
        }

        public class VoteRequest
        {
            public bool vote { get; set; }
        }

        public class ActionRequest
        {
            public bool action { get; set; }
        }

        public class ApiResponse
        {
            public int status { get; set; }
            public string msg { get; set; } = "";
            public object data { get; set; } = new { };
            public object others { get; set; } = new { };
        }


        // POST /api/games  (Game Create)

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGameRequest req)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.name) ||
                    req.name.Length < 3 || req.name.Length > 20)
                {
                    return BadRequest(new ApiResponse
                    {
                        status = 400,
                        msg = "Client Error: Invalid game name.",
                        data = new { },
                        others = new { }
                    });
                }

                if (string.IsNullOrWhiteSpace(req.owner) ||
                    req.owner.Length < 3 || req.owner.Length > 20)
                {
                    return BadRequest(new ApiResponse
                    {
                        status = 400,
                        msg = "Client Error: Invalid owner name.",
                        data = new { },
                        others = new { }
                    });
                }

                if (req.password != null &&
                    (req.password.Length < 3 || req.password.Length > 20))
                {
                    return BadRequest(new ApiResponse
                    {
                        status = 400,
                        msg = "Client Error: Invalid password.",
                        data = new { },
                        others = new { }
                    });
                }

                var game = await _business.CreateGameAsync(
                    req.name,
                    req.owner,
                    req.password
                );

                return CreatedAtAction(nameof(GetGame), new { gameId = game.Id }, new ApiResponse
                {
                    status = 201,
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


        // GET /api/games  (Game Search)

        [HttpGet]
        public async Task<IActionResult> SearchGames(
            [FromQuery] string? name,
            [FromQuery] GameStatus? status,
            [FromQuery] int page = 0,
            [FromQuery] int limit = 50)
        {
            try
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
            catch (Exception ex)
            {
                return BadRequest(new ApiResponse
                {
                    status = 400,
                    msg = ex.Message, // importante para debug
                    data = { },
                    others = new { }
                });
            }
        }


        // GET /api/games/{gameId} (Game Get)

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

            var enemiesList = game.Enemies.Contains(player)
                ? new List<string> { player }
                : new List<string>();

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
                    id = game.Id
                }
            });
        }


        // PUT /api/games/{gameId} (Join Game)

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
                    msg = "Player joined successfully",
                    data = new
                    {
                        id = game.Id,
                        name = game.Name,
                        owner = game.Owner,
                        status = game.Status,
                        createdAt = game.CreatedAt,
                        updatedAt = game.UpdatedAt,
                        password = game.Password,
                        players = game.Players,
                        enemies = game.Enemies,
                        currentRound = game.CurrentRound
                    },
                    others = new { }
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ApiResponse
                {
                    status = 404,
                    msg = "The specified resource was not found",
                    data = new { },
                    others = new { }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse
                {
                    status = 401,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (InvalidOperationException ex)
            {
                // Jugador ya existe
                return Conflict(new ApiResponse
                {
                    status = 409,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (ApplicationException ex)
            {
                // 428 PreconditionRequired
                return StatusCode(428, new ApiResponse
                {
                    status = 428,
                    msg = ex.Message,
                    data = new { },
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


        // HEAD /api/games/{gameId}/start  (Game Start)

        [HttpHead("{gameId}/start")]
        public async Task<IActionResult> StartGame(
            string gameId,
            [FromHeader(Name = "password")] string? passwordHeader,
            [FromHeader(Name = "player"), BindRequired] string playerHeader)
        {
            try
            {
                await _business.StartGameAsync(gameId, playerHeader, passwordHeader);
                Response.Headers.Add("X-msg", "Game started");
                return Ok();
            }
            catch (KeyNotFoundException ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                return StatusCode(404);
            }
            catch (UnauthorizedAccessException ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                // Puede mapearse a 401 o 403 según mensaje, aquí uso 401
                return StatusCode(401);
            }
            catch (InvalidOperationException ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                return StatusCode(428);
            }
            catch (Exception ex)
            {
                Response.Headers.Add("X-msg", ex.Message);
                return StatusCode(409);
            }
        }


        // GET /api/games/{gameId}/rounds  (Rounds list)

        [HttpGet("{gameId}/rounds")]
        public async Task<IActionResult> GetRounds(
            string gameId,
            [FromHeader] string? password,
            [FromHeader, BindRequired] string player)
        {
            try
            {
                var (game, rounds) = await _business.GetRoundsAsync(gameId);

                if (game.Password && string.IsNullOrEmpty(password))
                {
                    return Unauthorized(new { status = 401, msg = "Invalid credentials" });
                }

                if (!game.Players.Contains(player))
                {
                    return StatusCode(403, new { status = 403, msg = "Not part of the game" });
                }

                var response = rounds.Select(r => new
                {
                    id = r.Id,
                    leader = r.Leader,
                    status = r.Status,
                    phase = r.Phase,
                    result = r.Result,
                    createdAt = r.CreatedAt,
                    updatedAt = r.UpdatedAt,
                    group = r.Group,
                    votes = r.Votes
                });

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Rounds found",
                    data = response,
                    others = new { }
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ApiResponse
                {
                    status = 404,
                    msg = "The specified resource was not found",
                    data = new { },
                    others = new { }
                });
            }
        }


        // GET /api/games/{gameId}/rounds/{roundId}  (Round Get)

        [HttpGet("{gameId}/rounds/{roundId}")]
        public async Task<IActionResult> GetRound(
            string gameId,
            string roundId,
            [FromHeader] string? password,
            [FromHeader, BindRequired] string player)
        {
            try
            {
                var (game, round) = await _business.GetRoundAsync(gameId, roundId);

                if (game.Password && string.IsNullOrEmpty(password))
                    return Unauthorized(new { status = 401, msg = "Invalid credentials" });

                if (!game.Players.Contains(player))
                    return StatusCode(403, new { status = 403, msg = "Not part of the game" });

                var response = new
                {
                    id = round.Id,
                    leader = round.Leader,
                    status = round.Status,
                    phase = round.Phase,
                    result = round.Result,
                    createdAt = round.CreatedAt,
                    updatedAt = round.UpdatedAt,
                    group = round.Group,
                    votes = round.Votes
                };

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Round found",
                    data = response,
                    others = new { }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse
                {
                    status = 404,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
        }

        // PATCH /api/games/{gameId}/rounds/{roundId} (Propose group)

        [HttpPatch("{gameId}/rounds/{roundId}")]
        public async Task<IActionResult> ProposeGroup(
            string gameId,
            string roundId,
            [FromHeader] string? password,
            [FromHeader(Name = "player"), BindRequired] string leader,
            [FromBody] ProposeGroupRequest request)
        {
            try
            {
                var (game, round) = await _business.GetRoundAsync(gameId, roundId);

                if (game.Password && string.IsNullOrEmpty(password))
                    return Unauthorized(new { status = 401, msg = "Invalid credentials" });

                if (!game.Players.Contains(leader))
                    return StatusCode(403, new { status = 403, msg = "Not part of the game" });

                (game, round) = await _business.ProposeGroupAsync(
                    gameId,
                    roundId,
                    leader,
                    request.group
                );

                var response = new
                {
                    id = round.Id,
                    leader = round.Leader,
                    status = round.Status,
                    phase = round.Phase,
                    result = round.Result,
                    createdAt = round.CreatedAt,
                    updatedAt = round.UpdatedAt,
                    group = round.Group,
                    votes = round.Votes
                };

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Group Created",
                    data = response,
                    others = new { }
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ApiResponse
                {
                    status = 404,
                    msg = "The specified resource was not found",
                    data = new { },
                    others = new { }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse
                {
                    status = 401,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse
                {
                    status = 400,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (ApplicationException ex)
            {
                return StatusCode(428, new ApiResponse
                {
                    status = 428,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
        }

        // POST /api/games/{gameId}/rounds/{roundId} (Vote)

        [HttpPost("{gameId}/rounds/{roundId}")]
        public async Task<IActionResult> SubmitVote(
            string gameId,
            string roundId,
            [FromHeader] string? password,
            [FromHeader, BindRequired] string player,
            [FromBody] VoteRequest voteRequest)
        {
            try
            {
                var (game, round) = await _business.GetRoundAsync(gameId, roundId);

                if (game.Password && string.IsNullOrEmpty(password))
                    return Unauthorized(new { status = 401, msg = "Invalid credentials" });

                if (!game.Players.Contains(player))
                    return StatusCode(403, new { status = 403, msg = "Not part of the game" });

                (game, round) = await _business.SubmitVoteAsync(
                    gameId,
                    roundId,
                    player,
                    voteRequest.vote);

                var response = new
                {
                    id = round.Id,
                    leader = round.Leader,
                    status = round.Status,
                    phase = round.Phase,
                    result = round.Result,
                    createdAt = round.CreatedAt,
                    updatedAt = round.UpdatedAt,
                    group = round.Group,
                    votes = round.Votes
                };

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Voted successfully",
                    data = response,
                    others = new { }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse
                {
                    status = 404,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse
                {
                    status = 401,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (ApplicationException ex)
            {
                return StatusCode(428, new ApiResponse
                {
                    status = 428,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
        }


        // PUT /api/games/{gameId}/rounds/{roundId} (Action)

        [HttpPut("{gameId}/rounds/{roundId}")]
        public async Task<IActionResult> SubmitAction(
            string gameId,
            string roundId,
            [FromHeader] string? password,
            [FromHeader, BindRequired] string player,
            [FromBody] ActionRequest actionRequest)
        {
            try
            {
                var (game, round) = await _business.GetRoundAsync(gameId, roundId);

                if (game.Password && string.IsNullOrEmpty(password))
                    return Unauthorized(new { status = 401, msg = "Invalid credentials" });

                if (!game.Players.Contains(player))
                    return StatusCode(403, new { status = 403, msg = "Not part of the game" });

                (game, round) = await _business.SubmitActionAsync(
                    gameId,
                    roundId,
                    player,
                    actionRequest.action);

                var response = new
                {
                    id = round.Id,
                    leader = round.Leader,
                    status = round.Status,
                    phase = round.Phase,
                    result = round.Result,
                    createdAt = round.CreatedAt,
                    updatedAt = round.UpdatedAt,
                    group = round.Group,
                    votes = round.Votes
                };

                return Ok(new ApiResponse
                {
                    status = 200,
                    msg = "Action registered",
                    data = response,
                    others = new { }
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse
                {
                    status = 404,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse
                {
                    status = 401,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse
                {
                    status = 400,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
            catch (ApplicationException ex)
            {
                return StatusCode(428, new ApiResponse
                {
                    status = 428,
                    msg = ex.Message,
                    data = new { },
                    others = new { }
                });
            }
        }
    }
}