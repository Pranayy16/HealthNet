using System.Net;
using HealthNet.DTOs;
using HealthNet.DTOs.UserDTO;
using HealthNet.Services.UserServices;
using HealthNet.Utility;
using HealthNetDb.Data;
using HealthNetDb.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HealthNet.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HealthNetContext _context;
        private readonly IUserService _userService;

        // <summary>
        // Constructor for initializing fields
        // </summary>
        // <param name="HealthNetContext"> Context class to communicate with DB </param>
        // <param name="configuration"> Configuration object to read the data from appsettings.json </param>
        // <param name="userService"> userService object to use the methods in it. </param>
        public UserController(HealthNetContext context, IConfiguration configuration, IUserService userService)
        {
            _configuration = configuration;
            _context = context;
            _userService = userService;
        }

        // <summary>
        // LoginAsync for User login
        // </summary>
        // <param name="request"> User Login Request DTO for secured data Transfer from client </param>
        [HttpPost]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> LoginAsync([FromBody] UserLoginRequest request)
        {
            var loginResult = await _userService.LoginServiceAsync(request, _context, _configuration);

            if (!loginResult.Success)
            {
                return Unauthorized(new { error = loginResult.ErrorMessage });
            }

            return Ok(new { token = loginResult.Token });
        }

        // <summary>
        // Register method for new user
        // </summary>
        /// <param name="user">The user registration data transfer object containing credentials and profile info.</param>
        /// <returns>An IActionResult containing the registration response or an error message.</returns>
        [HttpPost("register")]
        [ProducesResponseType(typeof(UserRegisterResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Register([FromBody] UserRegisterRequestDto request)
        {
            try
            {
                // Validate Model State
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                var result = await _userService.RegisterUser(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // <summary>
        // ForgotPassword method for resetting the password of the user
        // </summary>
        /// <param name="request">The forgot password data transfer object containing the email and new password.</param>
        /// <returns>An IActionResult indicating the success or failure of the password reset operation.</returns>
        [HttpPost("forgotpassword")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest(ForgotPasswordHelper.InvalidPassword);
                }
                // Call the service to do the actual work
                var (success, message) = await _userService.ResetPasswordAsync(request);

                // User doesn't exist
                if (!success)
                {
                    return BadRequest(message);
                }

                return Ok(new { message });
            }
            catch
            {
                // Handle exception if any 
                return StatusCode(500, ForgotPasswordHelper.GenericError);
            }
        }
        /// <summary>
        /// Retrieves all users in the system regardless of their status.
        /// Route: GET /api/v1/user/GetAll
        /// </summary>
        /// <returns>
        /// 200 OK — List of all users with count and success flag.
        /// 401 Unauthorized — User is not logged in.
        /// 403 Forbidden — Logged in but does not have Admin role.
        /// 500 Internal Server Error — Unexpected server error.
        /// </returns>
        [HttpGet("GetAll")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
        public async Task<IActionResult> GetAll()
        {
            // Call the service to get all users
            var users = await _userService.GetAllUsersAsync();
            return Ok(new
            {
                // Return a structured response with success status, count of users, and the user data
                success = true,
                count = users.Count(),
                data = users
            });
        }

        // Update user details
        /// <summary>
        /// Updates the details of an existing user based on the provided user ID and update data transfer object (DTO).
        /// </summary>
        /// <param name="id"></param>
        /// <param name="dto"></param>
        /// <returns>
        /// If the user is updated successfully it return the user, otherwise it will return null if the user does not exist or is inactive.
        /// </returns>
        /// <exception cref="HealthNetException"></exception>
        // Update user details
        [HttpPost("update/{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            try
            {
                var user = await _userService.UpdateUserAsync(id, dto);
                return Ok(user);
            }
            catch (HealthNetException ex)
            {
                return BadRequest(new
                {
                    message = ex.Message
                });
            }
        }

        // Get user by id
        /// <summary>
        /// Retrieves a user entity from the database based on the provided user ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>
        /// The user entity corresponding to the provided ID, or null if not found.
        /// </returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                return Ok(user);
            }
            catch (HealthNetException ex)
            {
                return NotFound(new
                {
                    message = ex.Message
                });
            }
        }
    }
}