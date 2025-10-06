using ABCRetailFunctions.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using System.Text;
using System.Text.Json;

namespace ABCRetailFunctions.Services
{
    public class QueueStorageService
    {
        //Define the Queue client
        private readonly QueueClient _queueClient;

        //Initialize the constructor
        public QueueStorageService(string storageConnectionString, string queueName)
        {
            var queueServiceClient = new QueueServiceClient(storageConnectionString);
            _queueClient = queueServiceClient.GetQueueClient(queueName);
            _queueClient.CreateIfNotExists();
        }

        //Send Log entry to queue                                                                               
        public async Task SendLogEntryAsync(object message)
        {
            //Convert the message to a JSON string
            var jsonMessage = JsonSerializer.Serialize(message);
            await _queueClient.SendMessageAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonMessage)));
        }

        //Get log entries from queue
        public async Task<List<AuditLog>> GetLogEntriesAsync()
        {
            var entryList = new List<AuditLog>();
            var entries = await _queueClient.PeekMessagesAsync(maxMessages: 30);
            foreach (PeekedMessage entry in entries.Value)
            {
                entryList.Add(new AuditLog
                {
                    MessageId = entry.MessageId,
                    InsertionTime = entry.InsertedOn,
                    MessageText = Encoding.UTF8.GetString(Convert.FromBase64String(entry.Body.ToString()))
                });
            }
            return entryList;
        }
    }
}

