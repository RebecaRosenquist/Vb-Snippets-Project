using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Vb_Snippets_Project.Models
{
    public class Ticketorder
    {
        [JsonSerializable(typeof(TicketOrder))]
        public class TicketOrder
        {
            public Guid id { get; private set; } = Guid.NewGuid();
            [JsonPropertyName("orderId")]
            public int OrderId { get; set; }

            [JsonPropertyName("firstName")]
            public string FirstName { get; set; } = null!;

            [JsonPropertyName("lastName")]
            public string LastName { get; set; } = null!;

            [JsonPropertyName("email")]
            public string Email { get; set; } = null!;

            [JsonPropertyName("phoneNumber")]
            public string PhoneNumber { get; set; } = null!;

            [JsonPropertyName("amountPaid")]
            public int AmountPaid { get; set; }

        }
    }
}
