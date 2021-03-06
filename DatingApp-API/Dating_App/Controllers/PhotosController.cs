﻿using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Dating_App.Data;
using Dating_App.Dtos;
using Dating_App.Helpers;
using Dating_App.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dating_App.Controllers
{
    [Produces("application/json")]
    [Route("api/users/{userId}/[controller]")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private readonly IDatingRepository _repo;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;

        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repo,
            IMapper mapper, IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _repo = repo;
            _mapper = mapper;
            _cloudinaryConfig = cloudinaryConfig;

            Account acc = new Account(
                _cloudinaryConfig.Value.CloudName,
                _cloudinaryConfig.Value.ApiKey,
                _cloudinaryConfig.Value.ApiSecret
                );

            _cloudinary = new Cloudinary(acc);
        }

        /// <summary>
        /// Returns a photo, request must be made by a logged in user.
        /// </summary>
        /// <param name="id">The id of the photo to return</param>
        /// <returns></returns>
        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photoFromRepo = await _repo.GetPhoto(id);

            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);

            return Ok(photo);
        }

        /// <summary>
        /// Adds a photo, request must be made by a logged in user.
        /// </summary>
        /// <param name="userId">The id of the user which will add the photo</param>
        /// <param name="photoForCreationDto">The photo which will be added</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId,
          [FromForm] PhotoForCreationDto photoForCreationDto)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var userFromRepo = await _repo.GetUser(userId, true);

            var file = photoForCreationDto.File;

            var uploadResult = new ImageUploadResult();

            if (file.Length > 0)
            {
                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                    };

                    uploadResult = _cloudinary.Upload(uploadParams);
                }
            }

            photoForCreationDto.Url = uploadResult.Url.ToString();
            photoForCreationDto.PublicId = uploadResult.PublicId;

            var photo = _mapper.Map<Photo>(photoForCreationDto);

            if (!userFromRepo.Photos.Any(u => u.IsMain))
            {
                photo.IsMain = true;
            }

            userFromRepo.Photos.Add(photo);

            if (await _repo.SaveAll())
            {
                var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                return CreatedAtRoute(
                    routeName: "GetPhoto",
                    routeValues: new { userId = userFromRepo.Id, id = photo.Id },
                    value: photoToReturn);
            }

            return BadRequest("Could not add the photo");
        }

        /// <summary>
        /// Sets a photo as main photo, request must be made by a logged in user.
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <param name="id">The id of the photo</param>
        /// <returns></returns>
        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var user = await _repo.GetUser(userId, true);

            if (!user.Photos.Any(p => p.Id == id))
            {
                return Unauthorized();
            }

            var photoFromRepo = await _repo.GetPhoto(id);

            if (photoFromRepo.IsMain)
            {
                return BadRequest("This is already the main photo");
            }

            var currentMainPhoto = await _repo.GetMainPhotoForUser(userId);
            currentMainPhoto.IsMain = false;
            photoFromRepo.IsMain = true;

            if (await _repo.SaveAll())
            {
                return NoContent();
            }

            return BadRequest("Could not set photo to main");
        }

        /// <summary>
        /// Deletes a photo, request must be made by a logged in user.
        /// </summary>
        /// <param name="userId">The id of the user</param>
        /// <param name="id">The id of the photo</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var user = await _repo.GetUser(userId, true);

            if (!user.Photos.Any(p => p.Id == id))
            {
                return Unauthorized();
            }

            var photoFromRepo = await _repo.GetPhoto(id);

            if (photoFromRepo.IsMain)
            {
                return BadRequest("You cannot delete your main photo");

            }

            if (photoFromRepo.PublicId != null)
            {
                var deletionParams = new DeletionParams(photoFromRepo.PublicId);
                var result = _cloudinary.Destroy(deletionParams);

                if (result.Result == "ok")
                {
                    _repo.Delete(photoFromRepo);
                }
            }
            if(photoFromRepo.PublicId == null)
            {
                _repo.Delete(photoFromRepo);
            }

            if (await _repo.SaveAll())
            {
                return Ok();
            }
            return BadRequest("Failed to delete the photo");
        }
    }
}
