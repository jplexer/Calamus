using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Calamus.Database;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ulong Id { get; set; }
    
    public string? LastFmUsername { get; set; }
    
    public string? ListenBrainzUsername { get; set; }

    public bool AiPermissionGiven { get; set; } = false;
    
    public string? ChosenProvider { get; set; }
}