using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ConversationDtos
{
    public class ChatAttachmentUploadResult
    {
        public string Url { get; set; }
        public string? PublicId { get; set; }
        public string? Type { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? MimeType { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
    }

    public class MessageDto
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
        public ProductContextDto? ProductContext { get; set; }

        public AttachmentDto? Attachment { get; set; }
    }

    public class AttachmentDto
    {
        public string Url { get; set; }
        public string? Type { get; set; } // image | video | file
        public string? PublicId { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? MimeType { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
    }
}
