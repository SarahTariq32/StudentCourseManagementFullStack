namespace StudentCourseManagement.Application.DTOs;

public class DocumentInfoDto
{
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public int PageCount { get; set; }
}
