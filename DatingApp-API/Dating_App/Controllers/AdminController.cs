using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Dating_App.Data;
using Dating_App.Dtos;
using Dating_App.Helpers;
using Dating_App.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Threading.Tasks;

namespace Dating_App.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private readonly Cloudinary _cloudinary;

        public AdminController(DataContext context, UserManager<User> userManager, IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _context = context;
            _userManager = userManager;
            _cloudinaryConfig = cloudinaryConfig;

            Account acc = new Account(
                _cloudinaryConfig.Value.CloudName,
                _cloudinaryConfig.Value.ApiKey,
                _cloudinaryConfig.Value.ApiSecret
                );

            _cloudinary = new Cloudinary(acc);
        }

        /// <summary>
        /// Return a list of users and their roles, request must be made by an admin.
        /// </summary>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpGet("usersWithRoles")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            var userList = await _context.Users
                    .OrderBy(x => x.UserName)
                    .Select(user => new
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Roles = (from userRole in user.UserRoles
                                 join role in _context.Roles
                                 on userRole.RoleId
                                 equals role.Id
                                 select role.Name).ToList()
                    }).ToListAsync();

            return Ok(userList);
        }

        /// <summary>
        /// Return a list of photos which are waiting for approval, request must be made by an admin or moderator.
        /// </summary>
        /// <returns></returns>
        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpGet("photosForModeration")]
        public async Task<IActionResult> GetPhotosForModeration()
        {
            var photos = await _context.Photos
                                        .Include(u => u.User)
                                        .IgnoreQueryFilters()
                                        .Where(p => p.IsApproved == false)
                                        .Select(p => new
                                        {
                                            Id = p.Id,
                                            UserName = p.User.UserName,
                                            Url = p.Url,
                                            IsApproved = p.IsApproved
                                        })
                                        .ToListAsync();

            return Ok(photos);
        }

        /// <summary>
        /// Update a user's roles, request must be made by an admin.
        /// </summary>
        /// <param name="userName">The username whos roles will be updated</param>
        /// <param name="roleEditDto">The list of roles</param>
        /// <returns></returns>
        [Authorize(Policy = "RequireAdminRole")]
        [HttpPost("editRoles/{userName}")]
        public async Task<IActionResult> EditRoles(string userName, RoleEditDto roleEditDto)
        {
            var user = await _userManager.FindByNameAsync(userName);

            var userRoles = await _userManager.GetRolesAsync(user);

            var selectedRoles = roleEditDto.RoleNames;

            selectedRoles = selectedRoles ?? new string[] { };

            var result = await _userManager.AddToRolesAsync(user, selectedRoles.Except(userRoles));

            if (!result.Succeeded)
            {
                return BadRequest("Failed to add to roles");
            }

            result = await _userManager.RemoveFromRolesAsync(user, userRoles.Except(selectedRoles));

            if (!result.Succeeded)
            {
                return BadRequest("Failed to remove the roles");
            }

            return Ok(await _userManager.GetRolesAsync(user));

        }

        /// <summary>
        /// Approve a photo, request must be made by an admin or moderator.
        /// </summary>
        /// <param name="photoId">The id of the photo to approve</param>
        /// <returns></returns>
        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpPost("approvePhoto/{photoId}")]
        public async Task<IActionResult> ApprovePhoto(int photoId)
        {
            var photo = await _context.Photos
                                       .IgnoreQueryFilters()
                                       .FirstOrDefaultAsync(p => p.Id == photoId);
            photo.IsApproved = true;

            await _context.SaveChangesAsync();

            return Ok();
        }

        /// <summary>
        /// Reject a photo, request must be made by an admin or moderator.
        /// </summary>
        /// <param name="photoId"></param>
        /// <returns></returns>
        [Authorize(Policy = "ModeratePhotoRole")]
        [HttpPost("rejectPhoto/{photoId}")]
        public async Task<IActionResult> RejectPhoto(int photoId)
        {
            var photo = await _context.Photos
                                       .IgnoreQueryFilters()
                                       .FirstOrDefaultAsync(p => p.Id == photoId);
            if (photo.IsMain)
            {
                return BadRequest("You cannot reject the main photo");
            }

            if(photo.PublicId != null)
            {
                var deleteParams = new DeletionParams(photo.PublicId);

                var result = _cloudinary.Destroy(deleteParams);

                if(result.Result == "ok")
                {
                    _context.Photos.Remove(photo);
                }
            }

            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
