
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OllamaSharp;
using Spectre.Console;

namespace Nyx;

record Response(string Command, string Data);

static class OllamaExtensions {

	public static async Task<string> SelectModel(this IOllamaApiClient ollama)
	{
		var models = await ollama.ListLocalModelsAsync();
		
        var modelsWithBackChoice = models.OrderBy(m => m.Name).Select(m => m.Name).ToList();
        
        if (modelsWithBackChoice.FirstOrDefault(f => f.Contains("qwq")) is string model)
        {
            return model;
        }
        
        if (modelsWithBackChoice.Count == 0)
        {
            throw new Exception("No models available");
        }
        
        return modelsWithBackChoice.First();
	}
}

class Agent(IOllamaApiClient ollama) {

    protected virtual string SystemPrompt => """
you are an AI agent that generates code to solve problems. The language you use to respond is a simplified version of C#. Do not write any classes or main function, simply write
a series of statements to solve the problem.
""";

    public async Task Run(string task) {
        ollama.SelectedModel = await ollama.SelectModel();
        Console.WriteLine($"[agent] using model {ollama.SelectedModel}");
            
//         var systemPrompt = """
// you are a chatbot agent, you operate in steps always returning a command and receiving the response of that command as the input to the next step
        
// The following commands are available:
// task () // gets the current task
// say (text: string) // text response to the user
// search (query: string) -> string // search for information that is not known

// your response must be in the format: {command: string, data: string}
// """;

        var thinking = false;

        var chat = new Chat(ollama, SystemPrompt);

        // string message = "what is the current weather?";
        var response = new StringBuilder();

        var commands = new Dictionary<string, Func<string, string>>
            {
                { "task", (data) => {
                    return task;
                }},
                { "say", (data) => {
                    Console.WriteLine("[agent:say] " + data);
                    return "";
                } },
                { "search", (data) => { 
                    Console.WriteLine("[agent:search] " + data);
                    return "search response..." ;
                }}
            };
        
        Console.WriteLine($"[agent] >>");
        response.Clear();
        
        Console.ForegroundColor = ConsoleColor.Blue;
        
        // var format = new
        //     {
        //         type = "object",
        //         properties = new
        //         {
        //             command = new
        //             {
        //                 type = "string",
        //             },
        //             data = new
        //             {
        //                 type = "string",
        //             },
        //         },
        //         required = new[] { "command", "data" }
        //     };
    
        await foreach (var answerToken in chat.SendAsync(task)) //, null, null, format))
        {
            Console.Write($"{answerToken}");
        
            if (answerToken == "<think>") {
                thinking = true;
                continue;
            }
            
            if (answerToken == "</think>") {
                thinking = false;
                continue;
            }
            
            if (!thinking) {
                response.Append(answerToken);
            }
        }
        
        Console.WriteLine(); // end the debug output
        
        Console.ForegroundColor = ConsoleColor.White;
        
        // var responseObject = JsonSerializer.Deserialize<Response>(response.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        // if (responseObject == null)
        // {
        //     Console.WriteLine("failed to deserialize response");
        //     continue;
        // }
        
        // if (commands.TryGetValue(responseObject.Command, out var value))
        // {
        //     message = value(responseObject.Data);
        // }
        // else
        // {
        //     Console.WriteLine("Unknown command: " + responseObject.Command);
        // }
    }
}

record TaskPlan(List<string> Steps);

class TaskPlanner(IOllamaApiClient ollama) {

    protected virtual string SystemPrompt => """
You are an AI agent that given a single task generates a sequence of steps to solve the task.
You should not consider any concrete solutions to tasks, only the general series of steps required that another AI agent can then execute.
Do not number or decorate or use bullet points for the steps.

For example if you were asked "what is the birthday of the current Pope?" you may respond with:
[
    "Find out who the current Pope is",
    "Find out the birthday of that person"
]

""";

    public async Task<TaskPlan> Run(string task) {
        ollama.SelectedModel = await ollama.SelectModel();
        Console.WriteLine($"[planner] using model {ollama.SelectedModel}");
            
        var chat = new Chat(ollama, SystemPrompt);

        var response = new StringBuilder();
    
        Console.WriteLine($"[planner] >>");
        response.Clear();
        
        Console.ForegroundColor = ConsoleColor.Blue;
        
        var format = new
            {
                type = "object",
                properties = new
                {
                    steps = new
                    {
                        type = "array",
                        items = new {
                            type = "string"
                        }
                    },
                },
                required = new[] { "steps" }
            };
        
        await foreach (var answerToken in chat.SendAsync(task, null, null, format))
        {
            Console.Write($"{answerToken}");
            response.Append(answerToken);
        }
        
        Console.WriteLine(); // end the debug output
        
        Console.ForegroundColor = ConsoleColor.White;
        
        var responseObject = JsonSerializer.Deserialize<TaskPlan>(response.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                                throw new Exception("Failed to deserialize response");
        
        Console.WriteLine($"{responseObject}");
        
        return responseObject;
    }
}

class PlanExecutor(IOllamaApiClient ollama) {
    readonly Agent agent = new(ollama);

    public async Task Run(TaskPlan plan) {
        ollama.SelectedModel = await ollama.SelectModel();
        Console.WriteLine($"[planex] using model {ollama.SelectedModel}");

        foreach (var step in plan.Steps) 
        {
            Console.WriteLine($"[planex] executing step: {step}");
            await agent.Run(step);
        }
    }
}
