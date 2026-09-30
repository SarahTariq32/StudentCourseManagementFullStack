using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentCourseManagement.Infrastructure.Entities;

public class DocumentChunk
{
    public int Id { get; set; }
    public string DocumentName { get; set; }
    public int PageNumber { get; set; }
    public int ChunkIndex { get; set; }
    public string TextContent { get; set; }
    public string EmbeddingJson { get; set; }
}