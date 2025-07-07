using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObject.DTOs.ConversationDtos
{
    public class FindConversationRequestDto
    {
        [Required]
        public Guid RecipientId { get; set; }
        public Guid? ProductId { get; set; }
    }
}
