// See https://aka.ms/new-console-template for more information


using System.Text.Json;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using CustomerInteractionManager;

var client = new AmazonStepFunctionsClient();

Console.WriteLine("What would you like to do?");
Console.WriteLine("1) Claim a case waiting action?");
Console.WriteLine("2) Mark a claim complete?");
var option = Console.ReadLine();

var taskToken = string.Empty;
var output = string.Empty;

if (option == "1")
{
    Console.WriteLine("Task token?");
    taskToken = Console.ReadLine();
    Console.WriteLine("Who are you?");
    var claimedBy = Console.ReadLine();

    output = JsonSerializer.Serialize(new ClaimedByTaskResult(claimedBy));
}
else if (option == "2")
{
    
}

var result = client.SendTaskSuccessAsync(new SendTaskSuccessRequest()
{
    TaskToken = taskToken,
    Output = output
}).Result;