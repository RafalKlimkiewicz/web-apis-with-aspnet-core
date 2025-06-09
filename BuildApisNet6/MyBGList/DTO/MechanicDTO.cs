using System.ComponentModel.DataAnnotations;

using MyBGList.Attributes;

namespace MyBGList.DTO;

public class MechanicDTO
{
    [Required]
    public int Id { get; set; }

    [LettersOnlyValidator(UseRegex = true)]
    public string? Name { get; set; }
}