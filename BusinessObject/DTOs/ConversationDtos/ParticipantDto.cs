using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ConversationDtos
{
    public class ParticipantDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; }
        public string ProfilePictureUrl { get; set; }
    }
}
