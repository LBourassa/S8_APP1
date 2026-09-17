namespace SondageAPI.Modeles;

/// <summary>
/// Un participant est identifie uniquement par une cle unique, emise par le
/// serveur pour un sondage precis (jamais choisie par le client). Cette cle
/// garantit l'unicite de la participation : une fois utilisee, elle ne peut
/// plus servir a soumettre a nouveau ce sondage.
/// </summary>
public class Participant
{
    public string Cle { get; set; } = string.Empty;
    public string SondageId { get; set; } = string.Empty;
    public DateTime DateEmission { get; set; }
    public bool EstUtilisee { get; set; }
    public DateTime? DateUtilisation { get; set; }
}
