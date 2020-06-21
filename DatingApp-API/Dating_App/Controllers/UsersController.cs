using AutoMapper;
using Dating_App.Data;
using Dating_App.Dtos;
using Dating_App.Helpers;
using Dating_App.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Dating_App.Controllers
{
    [Produces("application/json")]
    [ServiceFilter(typeof(LogUserActivity))]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly IDatingRepository _repo;

        public UsersController(IDatingRepository repo, IMapper mapper)
        {
            _mapper = mapper;
            _repo = repo;
        }

        /// <summary>
        /// Returns a list of users, request must be made by a logged in user.
        /// </summary>
        /// <param name="userParams">The filters for retrieving users</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] UserParams userParams)
        {
            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var userFromRepo = await _repo.GetUser(currentUserId, true);

            userParams.UserId = currentUserId;

            if (string.IsNullOrEmpty(userParams.Gender))
            {
                userParams.Gender = userFromRepo.Gender == "male" ? "female" : "male";
            }

            var users = await _repo.GetUsers(userParams);

            var usersToReturn = _mapper.Map<IEnumerable<UserForListDto>>(users);

            Response.AddPagination(users.CurrentPage, users.PageSize,
                users.TotalCount, users.TotalPages);

            return Ok(usersToReturn);
        }

        /// <summary>
        /// Returns a user, request must be made by a logged in user.
        /// </summary>
        /// <param name="id">The of the user to return</param>
        /// <returns></returns>
        [HttpGet("{id}", Name = "GetUser")]
        public async Task<IActionResult> GetUser(int id)
        {
            var isCurrentUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) == id;

            var user = await _repo.GetUser(id, isCurrentUser);

            var userToReturn = _mapper.Map<UserForDetailedDto>(user);

            return Ok(userToReturn);
        }

        /// <summary>
        /// Updates a user, request must be made by a logged in user.
        /// </summary>
        /// <param name="id">The id of the user to update</param>
        /// <param name="userForUpdateDto">The user information</param>
        /// <returns></returns>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, UserForUpdateDto userForUpdateDto)
        {
            if (userForUpdateDto.DateOfBirth.CalculateAge() < 18)
            {
                return BadRequest("You must have at least 18 years old!");
            }

            if (id != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var userFromRepo = await _repo.GetUser(id, true);

            _mapper.Map(userForUpdateDto, userFromRepo);

            if (await _repo.SaveAll())
            {
                return NoContent();
            }

            throw new Exception($"Updating user {id} failed on save");
        }

        /// <summary>
        /// Likes a user, request must be made by a logged in user.
        /// </summary>
        /// <param name="id">The id of the user who sends the "Like"</param>
        /// <param name="recipientId">The id of the user who is "Liked"</param>
        /// <returns></returns>
        [HttpPost("{id}/like/{recipientId}")]
        public async Task<IActionResult> LikeUser(int id, int recipientId)
        {
            if (id != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var like = await _repo.GetLike(id, recipientId);

            if(like != null)
            {
                return BadRequest("You already liked this user");
            }

            if(await _repo.GetUser(recipientId, false) == null)
            {
                return NotFound();
            }

            like = new Like
            {
                LikerId = id,
                LikeeId = recipientId
            };

            _repo.Add<Like>(like);
            if(await _repo.SaveAll())
            {
                return Ok();
            }

            return BadRequest("Failed to like user");

        }
    }
}