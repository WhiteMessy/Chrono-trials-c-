using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChronoTrial.Models;

[Table("gebruiker")]
public class ApplicationUser
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Column("wachtwoord")]
    public string Wachtwoord { get; set; } = string.Empty;

    [Column("purchased")]
    public bool Purchased { get; set; }

    [Column("time")]
    public double? Time { get; set; }

    [Column("time_set_at")]
    public DateTime? TimeSetAt { get; set; }
}
