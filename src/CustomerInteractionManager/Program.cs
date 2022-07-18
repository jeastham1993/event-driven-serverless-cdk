// See https://aka.ms/new-console-template for more information


using System.Text.Json;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using CustomerInteractionManager;

var client = new AmazonStepFunctionsClient(new AmazonStepFunctionsConfig()
{
    RegionEndpoint = RegionEndpoint.EUWest1
});

var sqsClient = new AmazonSQSClient(new AmazonSQSConfig()
{
    RegionEndpoint = RegionEndpoint.EUWest1
});

var queueUrl = sqsClient.ListQueuesAsync("AwaitingClaim").Result.QueueUrls[0];

Console.WriteLine("What would you like to do?");
Console.WriteLine("1) Claim a case waiting action?");

var option = Console.ReadLine();

var taskToken = string.Empty;
var output = string.Empty;

if (option == "1")
{
    var message = sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest()
    {
        QueueUrl = queueUrl,
        MaxNumberOfMessages = 1
    }).Result;

    var payload = JsonSerializer.Deserialize<SqsMessage>(message.Messages[0].Body);
    Console.WriteLine($"New negative review from {payload.Payload.EmailAddress} saying '{payload.Payload.ReviewContent}'");
    
    taskToken = payload.TaskToken;
    Console.WriteLine("Who are you?");
    var claimedBy = Console.ReadLine();

    output = JsonSerializer.Serialize(new ClaimedByTaskResult(claimedBy));

    sqsClient.DeleteMessageAsync(queueUrl, message.Messages[0].ReceiptHandle);

}

var result = client.SendTaskSuccessAsync(new SendTaskSuccessRequest()
{
    TaskToken = taskToken,
    Output = output
}).Result;