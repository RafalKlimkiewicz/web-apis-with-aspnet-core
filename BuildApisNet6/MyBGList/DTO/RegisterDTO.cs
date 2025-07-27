using System.ComponentModel.DataAnnotations;

using MyBGList.Attributes;

namespace MyBGList.DTO;

public class RegisterDTO
{
    [Required]
    [CustomKeyValue("x-test-1", "value 1")]
    [CustomKeyValue("x-test-2", "value 2")]
    public string? UserName { get; set; }

    [Required]
    [EmailAddress]
    public string? Email { get; set; }
    [Required]
    public string? Password { get; set; }
}