using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HealthNetDb.Data;
using HealthNetDb.Entities;
using HealthNet.DTOs.UserDTO;
using HealthNet.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using HealthNet.DTOs;
using HealthNet.Repository.User;
using System.Text.RegularExpressions;
using HealthNet.Utility;
using Microsoft.AspNetCore.Http.HttpResults;

namespace HealthNet.Services.UserServices;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    /// <summary>
    /// Constructor for UserService, injects IUserRepository for data access and business logic separation.
    /// </summary>
    /// <param name="repository">The user repository instance for data access.</param>

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }
    // Login Service
    public async Task<LoginResult> LoginServiceAsync(UserLoginRequest request, HealthNetContext _context, IConfiguration _config)
    {
        // Check whether email and password are non empty fields
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Email and Password are required."
            };
        }
        // Validate the Email and user status
        Users? user = await _context.Userss.Include(u => u.RoleNavigation).FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !user.Status)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "No active account found with the provided email address."
            };
        }
        // Validate the password
        bool isPassword = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
        if (!isPassword)
        {
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Invalid Password."
            };
        }
        // Generate token
        var token = await GenerateJwtTokenServiceAsync(user, _config);
        return new LoginResult
        {
            Success = true,
            Token = token,
        };
    }

    //Jwt Token Generation
    // <summary>
    // GenerateJwtToken for generating the token
    // </summary>
    // <param name="user">user object for DB communication </param>
    private async Task<string> GenerateJwtTokenServiceAsync(Users user, IConfiguration _config)
    {

        // Jwt token variables
        var secretKey = _config["JwtSettings:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("JWT SecretKey is not configured.");
        }
        var issuer = _config["JwtSettings:Issuer"];
        var audience = _config["JwtSettings:Audience"];
        var expiryMinutes = int.TryParse(_config["JwtSettings:ExpiryMinutes"], out var minutes) ? minutes : 60;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));  //Converting plain secret key into Cryptographic key object
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);  //Create a Unique signing key using secret key

        var claims = new[]
        {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.RoleNavigation?.RoleName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return await Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));        //Token Generated with Paylaod
    }

    //Register a User
    public async Task<UserRegisterResponseDto> RegisterUser(UserRegisterRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||                 //Validating the user details 
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword) ||
            string.IsNullOrWhiteSpace(request.RoleName))
        {
            throw new ArgumentException("Invalid input");
        }
        if (request.Password != request.ConfirmPassword)                //validating wheather password and confirmpassword match
        {
            throw new ArgumentException("Passwords do not match");
        }
        request.Password =
            BCrypt.Net.BCrypt.HashPassword(request.Password);           //Hashing the password and stroring in DB

        return await _repository.RegisterUser(request);
    }


    //Forgot Password Functionality
    // <summary>
    // ResetPasswordAsync for resetting the user's password
    // </summary>
    // <param name="dto">ForgotPasswordDto object containing the reset password details </param>
    public async Task<(bool success, string message)> ResetPasswordAsync(ForgotPasswordDto dto)
    {
        try
        {
            // Steps for verification of new password
            if (dto.NewPassword != dto.ConfirmPassword)
            {
                return (false, ForgotPasswordHelper.PasswordsDoNotMatch);
            }
            // Validate Password Strength
            if (!IsValidPassword(dto.NewPassword))
            {
                return (false, ForgotPasswordHelper.InvalidPassword);
            }

            //Get the user by email from the repo
            var user = await _repository.GetUserByEmailAsync(dto.Email);
            if (user == null)
            {
                return (false, ForgotPasswordHelper.UserNotFound); // User doesn't exist
            }

            // Implementing the BCrypt hashing algorithm 
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.Password = hashedPassword;

            // Update the user in the database
            await _repository.UpdateUserAsync(user);

            return (true, ForgotPasswordHelper.PasswordUpdatedSuccess);
        }
        catch
        {
            // Return the error message to the controller
            return (false, ForgotPasswordHelper.GenericError);
        }
    }

    public bool IsValidPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return false;

        var pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$";
        return Regex.IsMatch(password, pattern);
    }

    // Get All Users Service
    public async Task<IEnumerable<UserResponse>> GetAllUsersAsync()
    {
        try
        {
            // Fetch users from the repository
            var users = await _repository.GetAllUsersAsync();

            // Map Users entities to UserResponse DTOs
            return users.Select(u => new UserResponse
            {
                UserId = u.UserId,
                Name = u.Name,
                Email = u.Email,
                Phone = u.Phone,
                Status = u.Status,
                RoleName = u.RoleNavigation?.RoleName ?? "Unknown"
            });
        }
        catch (Exception ex)
        {
            // If mapping or repository call fails, throw with a clear message
            throw new HealthNetException($"An error occurred while processing the user list. {ex.Message}");
        }
    }

    // Get User By Id Service
    public async Task<Users?> GetUserByIdAsync(int id)
    {
        return await _repository.GetUserByIdAsync(id);
    }

    // Update User Service
    /// <summary>
    /// Updates the details of an existing user based on the provided user ID and update data transfer object (DTO).
    /// </summary>
    /// <param name="id"></param>
    /// <param name="dto"></param>
    /// <returns>
    /// If the user is updated successfully it return the user, otherwise it will return null if the user does not exist or is inactive.
    /// </returns>
    /// <exception cref="HealthNetException"></exception>

    public async Task<Users?> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _repository.GetUserByIdAsync(id);

        if (user == null)
            throw new HealthNetException(UpdateHelper.UserNotFound);

        if (!user.Status)
            throw new HealthNetException(UpdateHelper.UserInactive);

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.Phone = dto.PhoneNumber;

        await _repository.UpdateUserAsync(user);
        return user;
    }
}