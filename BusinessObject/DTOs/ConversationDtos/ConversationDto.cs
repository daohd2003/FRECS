using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ConversationDtos
{
    public class ConversationDto
    {
        public Guid Id { get; set; }
        public ParticipantDto OtherParticipant { get; set; }
        public string LastMessageContent { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsRead { get; set; }
        public ProductContextDto? ProductContext { get; set; }
    }
}
