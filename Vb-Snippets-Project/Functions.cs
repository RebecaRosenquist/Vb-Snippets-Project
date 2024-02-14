using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Vb_Snippets_Project.Models.Ticketorder;

namespace Vb_Snippets_Project
{
    public class Functions
    {
        private readonly ILogger _logger;

        public Functions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Functions>();
        }

        [Function("F1")]
        public async Task<string> RunF1([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req, [FromBody] List<TicketOrder> ticketOrders)
        {
            string connectionString = Environment.GetEnvironmentVariable("storageAccountConnString") ?? string.Empty;
            try
            {
                // Deserialize the ticketOrders string into a list of TicketOrder objects
                var ticketOrderList = ticketOrders;

                //todo create a blob in a storage container called "orders" with the name "order-{Guid.NewGuid()}.json" and the content of the ticketOrders string
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

                var blobContainerClient = blobServiceClient.GetBlobContainerClient("ticket-orders");

                await blobContainerClient.CreateIfNotExistsAsync();

                var blobClient = blobContainerClient.GetBlobClient($"tickets-{Guid.NewGuid()}.txt");

                string blobContent = System.Text.Json.JsonSerializer.Serialize(ticketOrderList);


                byte[] bytes = Encoding.UTF8.GetBytes(blobContent);

                //create a MemoryStream with the content from param
                using var stream = new MemoryStream(bytes);

                //upload the stream content to the blob, overwriting if it already exists
                await blobClient.UploadAsync(stream, overwrite: true);

                //todo Return a relevant message
                return blobContent;
            }
            catch (Exception)
            {
                _logger.LogError("Something went wrong with executing F1...try again!");
                throw;
            }

        }

        [Function("F2")]
        public async Task<string> RunF2([BlobTrigger("ticket-orders/{name}", Connection = "storageAccountConnString")] Stream stream, string name)
        {
            try
            {
                // Receive blob content from the specified Azure Storage blob trigger
                using var blobStreamReader = new StreamReader(stream);
                // Read the entire blob content asynchronously
                string content = await blobStreamReader.ReadToEndAsync();
                // Create a list to store valid TicketOrder objects
                var validTickets = new List<TicketOrder>();
                // Deserialize JSON content into a list of TicketOrder objects
                var tickets = System.Text.Json.JsonSerializer.Deserialize<List<TicketOrder>>(content);
                // Validate each TicketOrder in the list
                foreach (TicketOrder ticket in tickets)
                {
                    if (ticket.OrderId > 0 &&
                !string.IsNullOrWhiteSpace(ticket.FirstName) &&
                !string.IsNullOrWhiteSpace(ticket.LastName) &&
                !string.IsNullOrWhiteSpace(ticket.Email) &&
                !string.IsNullOrWhiteSpace(ticket.PhoneNumber) &&// Add other fields as needed
                ticket.AmountPaid > 0)
                    {
                        // Add valid tickets to the list
                        validTickets.Add(ticket);
                    }
                }
                // EventGrid endpoint and access key for publishing events
                string endpoint = "https://evgt-snippets-project.eastus-1.eventgrid.azure.net/api/events";
                //Bad practice bro
                string key = "v24bprHJvDc6bQTTqo4HoG4wBNOlxSRFO59xZ1pQZLU=";

                // Create an EventGridEvent with specified properties, including serialized valid tickets
                EventGridPublisherClient client = new EventGridPublisherClient(
                    new Uri(endpoint),
                    //BAD PRACTICE
                    new Azure.AzureKeyCredential(key)
                    );

                EventGridEvent egEvent =
                new EventGridEvent(
                "ExampleEventSubject",
                "Example.EventType",
                "1.0",
                System.Text.Json.JsonSerializer.Serialize(validTickets));

                // Send the EventGridEvent asynchronously
                await client.SendEventAsync(egEvent);
                // Return the string representation of valid tickets
                return validTickets.ToString();
            }
            catch (Exception)
            {
                _logger.LogError("Something went wrong with executing F2...try again!");
                throw;
            }

        }

        [Function("F3")]
        public async Task RunF3([EventGridTrigger] EventGridEvent ev)
        {
            try
            {
                // Our content
                string eventData = ev.Data.ToString().Replace("\"", "").Replace("\\u0022", "\"").Replace("\\n", "");

                // Deserialize the JSON content of the event into a list of TicketOrder objects
                var tickets = System.Text.Json.JsonSerializer.Deserialize<List<TicketOrder>>(eventData);

                List<TicketOrder> validTickets = new List<TicketOrder>();

                // Loop through ticketlist if over 799 amountPaid add to list
                foreach (var ticket in tickets)
                {
                    if (ticket.AmountPaid > 799)
                    {
                        validTickets.Add(ticket);
                    }
                }

                string connectionString = "Endpoint=sb://sb-snippets3213.servicebus.windows.net/;SharedAccessKeyName=sas-policy-send;SharedAccessKey=dblxpC/hH00Aov97LmmPySgt8wy6xI8uM+ASbNBJer8=;EntityPath=sbt-snippetstopic";
                string topicName = "sbt-snippetstopic";

                ServiceBusClient sbc = new ServiceBusClient(connectionString);
                ServiceBusSender sender = sbc.CreateSender(topicName);
                ServiceBusMessage message = new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(validTickets));

                await sender.SendMessageAsync(message);
            }
            catch (Exception)
            {
                _logger.LogError("Something went wrong with executing F3...try again!");
                throw;
            }


        }

        [Function("F4")]
        public async Task Run([ServiceBusTrigger("sbt-snippetstopic", "sbs-snippetssubscription", Connection = "sbConnString")]
         ServiceBusReceivedMessage message,
         ServiceBusMessageActions messageActions)
        {
            try
            {
                // Complete the message
                await messageActions.CompleteMessageAsync(message);

                // Account url
                string cosmosEndpointUrl = "https://tickets-db-snippets.documents.azure.com:443/";
                //Azure account primary key
                string cosmosPrimaryKey = "4rHk3FHvconAhBWJ5wLiNFUIEcjwmSq8KMSirHfMmcUhOMhkfNMUqUhUVFQ6Te9iQsWvuf3VT2y6ACDbZ4ddBw==";

                //Skapa en cosmos client, släng in url och primarykey
                CosmosClient client = new CosmosClient(cosmosEndpointUrl, cosmosPrimaryKey);
                //Om ej skapad, skapa och döp till namn. Returnerar dbresponse med ref till db
                DatabaseResponse dbResponse = await client.CreateDatabaseIfNotExistsAsync("valid-ticket-orders");

                //Skapa container returnerar container response
                //Med namn på container orders, partitionkey = customId
                ContainerResponse cResponse = await dbResponse.Database.CreateContainerIfNotExistsAsync("ticketOrders", "/OrderId");

                // DESERIALISERA - SKAPA TICKETORDERS - LOOPA - LÄGG TILL I DB
                // Deserialize ticket orders
                var tickets = message.Body.ToObjectFromJson<List<TicketOrder>>();
                //Loop through list of tickets and create items
                foreach (TicketOrder ticket in tickets)
                {
                    ItemResponse<TicketOrder> itemResponse = await cResponse.Container.CreateItemAsync(ticket, new PartitionKey(ticket.OrderId));
                }

            }

            catch (Exception)
            {
                _logger.LogError("Something went wrong with executing F4...try again!");
                throw;
            }

        }
    }
    }

