using System;
using HealthNet.DTOs;
using HealthNetDb.Data;
using HealthNetDb.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthNet.Repository.User;

public class UserRepository : IUserRepository
{
    private readonly HealthNetContext _context;
    /// <summary>
    /// Constructor for UserRepository, injects HealthNetContext for database access.
    /// </summary>
    /// <param name="context">The database context used for accessing user data.</param>

    public UserRepository(HealthNetContext context)
    {
        _context = context;
    }
    /// <summary>
    /// Handles the database logic for registering a user, including email uniqueness checks and persistence.
    /// </summary>
    /// <param name="request">The registration request containing user details.</param>
    /// <returns>A response DTO containing the mapped details of the newly created user.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the request object is null.</exception>
    public async Task<UserRegisterResponseDto> RegisterUser(
            UserRegisterRequestDto request)
    {
        // Email check 
        if (await _context.Userss
            .AnyAsync(u => u.Email == request.Email))
        {
            throw new InvalidOperationException("Email already exists");
        }

        // FETCH ROLE FROM DATABASE
        var role = await _context.Roles
            .SingleOrDefaultAsync(r => r.RoleName == request.RoleName);

        if (role == null)
        {
            throw new ArgumentException(
                $"Invalid role name: {request.RoleName}");
        }

        var user = request.ToUserEntity();
        user.RoleId = role.RoleId;
        user.Status = true;

        _context.Userss.Add(user);
        await _context.SaveChangesAsync();

        await _context.Entry(user).Reference(u => u.RoleNavigation).LoadAsync();
        return user.ToUserRegisterResponse();
    }
    /// <summary>
    /// Retrieves a user entity from the database based on the provided email address.
    /// </summary>
    /// <param name="email">The email address of the user to retrieve.</param>
    /// <returns>The user entity corresponding to the provided email, or null if not found.</returns>
    /// <exception cref="ArgumentException">Thrown when the email parameter is null or empty.</exception>
    public async Task<Users> GetUserByEmailAsync(string email)
    {
        return await _context.Userss.FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Retrieves all users from the database, including both active and inactive users, along with their associated roles.
    /// </summary>
    /// <returns>A collection of all users with their associated roles.</returns>
    /// <exception cref="HealthNetException"></exception>
    public async Task<IEnumerable<Users>> GetAllUsersAsync()
    {
        try
        {
            // Fetch all users from the database, including their roles
            return await _context.Userss
                .Include(u => u.RoleNavigation)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            // If DB query fails, throw with a clear message
            throw new HealthNetException($"An error occurred while fetching users from the database. {ex.Message}");
        }
    }

    //Get user by id
    /// <summary>
    /// Retrieves a user entity from the database based on the provided user ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>
    /// The user entity corresponding to the provided ID, or null if not found.
    /// </returns>
    /// <exception cref="HealthNetException"></exception>
    public async Task<Users?> GetUserByIdAsync(int id)
    {
        try
        {
            return await _context.Userss.FindAsync(id);
        }
        catch (Exception ex)
        {
            throw new HealthNetException(ex.Message);
        }
    }

    /// <summary>
    /// Updates the details of an existing user in the database.
    /// </summary>
    /// <param name="user"></param>
    /// <returns>
    ///  It updates the user entity in the database based on the provided user.
    /// </returns>
    /// <exception cref="HealthNetException"></exception>
    public async Task UpdateUserAsync(Users user)
    {
        try
        {
            _context.Userss.Update(user);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw new HealthNetException(ex.Message);
        }
    }
}