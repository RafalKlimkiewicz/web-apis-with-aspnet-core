using System.ComponentModel.DataAnnotations;

namespace MyBGList.Models;

public class Category
{
    [Key]
    [Required]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [Required]
    public DateTime CreatedDate { get; set; }

    [Required]
    public DateTime LastModifiedDate { get; set; }

    public ICollection<BoardGames_Categories>? BoardGames_Categories { get; set; }
}
