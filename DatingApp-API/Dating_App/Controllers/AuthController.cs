using AutoMapper;
using Dating_App.Dtos;
using Dating_App.Helpers;
using Dating_App.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Dating_App.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config, IMapper mapper,
                UserManager<User> userManager, SignInManager<User> signInManager)
        {
            _mapper = mapper;
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
        }

        /// <summary>
        /// Register a new user.
        /// </summary>
        /// <param name="userForRegisterDto"></param>
        /// <returns></returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register(UserForRegisterDto userForRegisterDto)
        {
            if(userForRegisterDto.DateOfBirth.CalculateAge() < 18)
            {
                return BadRequest("You must have at least 18 years old!");
            }

            var userToCreate = _mapper.Map<User>(userForRegisterDto);

            var result = await _userManager.CreateAsync(userToCreate, userForRegisterDto.Password);

            _userManager.AddToRoleAsync(userToCreate, "Member").Wait();


            var userToReturn = _mapper.Map<UserForDetailedDto>(userToCreate);

            if (result.Succeeded)
            {
                return CreatedAtRoute("GetUser", new { controller = "Users", id = userToCreate.Id }, userToReturn);
            }
            return BadRequest(result.Errors);
        }

        /// <summary>
        /// Login
        /// </summary>
        /// <param name="userForLoginDto">User which will be logged in</param>
        /// <returns></returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login(UserForLoginDto userForLoginDto)
        {
            var user = await _userManager.FindByNameAsync(userForLoginDto.Username);

            if (user == null)
            {
                return Unauthorized();
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, userForLoginDto.Password, false);

            if (result.Succeeded)
            {
                var appUser = _mapper.Map<UserForListDto>(user);

                return Ok(new
                {
                    token = await GenerateJwtToken(user),
                    user = appUser
                });
            }

            return Unauthorized();
        }

        [HttpPost("loginGoogle/{idToken}")]
        public async Task<IActionResult> LoginGoogle(string idToken)
        {

            var validPayload = await GoogleJsonWebSignature.ValidateAsync(idToken);

            if (validPayload == null)
            {
                return Unauthorized();
            }

            var clientId = _config.GetValue<String>("Google:ClientId");

            if (!validPayload.Audience.Equals(clientId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByNameAsync(validPayload.Email);

            if (user == null)
            {
                user = new User
                {
                    UserName = validPayload.Email,
                    KnownAs = validPayload.GivenName,
                    LastActive = DateTime.Now,
                    Created = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user);
                _userManager.AddToRoleAsync(user, "Member").Wait();

                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }
            }


            var appUser = _mapper.Map<UserForListDto>(user);

            return Ok(new
            {
                token = await GenerateJwtToken(user),
                user = appUser
            });
        }

        private async Task<string> GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
           {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName)
            };

            var roles = await _userManager.GetRolesAsync(user);

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(_config.GetSection("AppSettings:Token").Value));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

    }
}