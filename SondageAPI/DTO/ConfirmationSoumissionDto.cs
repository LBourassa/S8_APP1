namespace SondageAPI.DTO;

public class ConfirmationSoumissionDto
{
    public bool Succes { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime DateSoumission { get; set; }
}
