using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace HealthNet.DTOs.UserDTO;

public class UpdateUserDto
{
    [Required]
    public string Name { get; set; } = null!;

    [Required]
    [EmailAddress] //validation attribute-> this field(Email) must be in a valid format,it validates email
    public string Email { get; set; } = null!;

    [Required]
    [Phone]    //checks if the input looks like a valid phone number format
    public string PhoneNumber { get; set; } = null!;
}