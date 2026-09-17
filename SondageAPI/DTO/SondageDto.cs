namespace SondageAPI.DTO;

public class SondageDto
{
    public string Id { get; set; } = string.Empty;
    public string Titre { get; set; } = string.Empty;
    public List<QuestionDto> Questions { get; set; } = new();
}
