
using System.Data.SqlTypes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
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
        
        if (modelsWithBackChoice.FirstOrDefault(f => f.Contains("llama3.3")) is string model)
        {
            return model;
        }
        
        if (modelsWithBackChoice.Count == 0)
        {
            throw new Exception("No models available");
        }
        
        return modelsWithBackChoice.First();
	}
    
    // public static async Task<string> GetResult() {
        
    // }
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

record Variable(string Name, string Description) {
    override public string ToString() => $"{Name} ({Description})";
}

record Step(string Description, List<string> Inputs, string Output) {
    override public string ToString() => $"{Description}\n  Inputs: {string.Join(", ", Inputs)}\n  Output: {Output}";
}

record TaskPlan(List<Step> Steps, List<Variable> Variables) {
     public bool Validate() {
        var variables = Variables.Select(v => v.Name).ToHashSet();
        
        
        foreach (var step in Steps)
        {
            foreach (var (variable, kind) in step.Inputs.Select(i => (i, "input")).Union([(step.Output, "output")]))
            {
                if (!variables.Contains(variable)) 
                {
                    Console.WriteLine($"[!] [plan validation] step '{step.Deconstruct}' {kind} '{variable}' does not exist");
                    return false;
                }
            }
        }

        Console.WriteLine("[plan validation] validated OK");
        
        return true;
     }
}

class TaskPlanner(IOllamaApiClient ollama) {

    protected virtual string SystemPrompt => """
You are an AI agent that given a single task generates a sequence of steps to solve the task.
You should not consider any concrete solutions to tasks, only the general series of steps required that another AI agent can then execute.
Do not number or decorate or use bullet points for the steps.
Each step should specify the input and output variables of each step.
Output variables should be used as input variables in subsequent steps.

For example if you were asked "what is the birthday of the current Pope?" you may respond with:
{ 
    "Steps": [
        { Description: "Find out who the current Pope is", "Inputs": [], "Outputs": ["currentPope"] },
        { Description: "Find out the birthday of $currentPope", "Inputs": ["currentPope"], "Outputs": ["birthday"] }
    ],
    "Variables": [
        { Name = "currentPope", Description = "The current Pope" },
        { Name = "birthday", Description = "The birthday of the current Pope" }
    ]
}

""";

    public async Task<TaskPlan> Run(string task) {
        ollama.SelectedModel = await ollama.SelectModel();
        Console.WriteLine($"[planner] using model {ollama.SelectedModel}");
            
        var chat = new Chat(ollama, SystemPrompt);

        var response = new StringBuilder();
    
        Console.WriteLine($"[planner] >>");
        response.Clear();
        
        Console.ForegroundColor = ConsoleColor.Blue;
        
        JsonSerializerOptions options = JsonSerializerOptions.Default;
        
        await foreach (var answerToken in chat.SendAsync(task, null, null, options.GetJsonSchemaAsNode(typeof(TaskPlan))))
        {
            Console.Write($"{answerToken}");
            response.Append(answerToken);
        }
        
        Console.WriteLine(); // end the debug output
        
        Console.ForegroundColor = ConsoleColor.White;
        
        var responseObject = JsonSerializer.Deserialize<TaskPlan>(response.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
                                throw new Exception("Failed to deserialize response");
        
        Console.WriteLine("steps:");
        foreach (var step in responseObject.Steps)
        {
            Console.WriteLine($" {step}");
        }

        // Console.WriteLine("inputs:");
        // foreach (var input in responseObject.Inputs)
        // {
        //     Console.WriteLine($" {input}");
        // }
        
        // Console.WriteLine("outputs:");
        // foreach (var output in responseObject.Outputs)
        // {
        //     Console.WriteLine($" {output}");
        // }
        
        return responseObject;
    }
}

class ExecState {
    public Dictionary<string, string> Variables = [];
}

public class ExecTools {
    /// <summary>
    /// Get the current location of the user
    /// </summary>
    [OllamaTool]
    public static string GetLocation() => "Vancouver, BC, Canada";


    /// <summary>
    /// Get the weather at the given location
    /// </summary>
    [OllamaTool]
    public static string GetWeather(string location) {
        Console.WriteLine($"[tool] getting weather for {location}");
        return  "Rainy, 10C";
    }
}

class StepExecutor(IOllamaApiClient ollama) {
    
    protected virtual string SystemPrompt => """
        Solve the given task and give a minimal response. The response should be a single value that can be assigned to a variable for later use.
        The response should be given without any context from the prompt, just as small a value as possible.
    """;

    public async Task<string> Run(Step step, ExecState state) {
        ollama.SelectedModel = await ollama.SelectModel();
        // Console.WriteLine($"[exec] using model {ollama.SelectedModel}");
        
        var chat = new Chat(ollama, SystemPrompt);

        var response = new StringBuilder();
    
        // Console.WriteLine($"[exec] >>");
        // response.Clear();
        
        Console.ForegroundColor = ConsoleColor.Blue;
        
        var prompt = new StringBuilder(step.Description);
        
        if (step.Inputs.Count > 0)
        {
            prompt.Append("\nVariables:\n");
            foreach (var input in step.Inputs)
            {
                prompt.Append($"${input} = \"{state.Variables[input]}\"\n");
            }
        }
        
        // Console.WriteLine(prompt);
        
        await foreach (var answerToken in chat.SendAsync(prompt.ToString(), [
            new GetLocationTool(),
            new GetWeatherTool()
        ]))
        {
            Console.Write($"{answerToken}");
            response.Append(answerToken);
        }
        
        Console.WriteLine();
        
        return response.ToString();
    }
}


class PlanExecutor(IOllamaApiClient ollama) {
    public async Task Run(TaskPlan plan) {
        ollama.SelectedModel = await ollama.SelectModel();
        Console.WriteLine($"[planex] using model {ollama.SelectedModel}");
        
        var state = new ExecState();
        
        foreach (var step in plan.Steps) 
        {
            AnsiConsole.Write(new Rule().LeftJustified());
            AnsiConsole.WriteLine();
            
            Console.ForegroundColor = ConsoleColor.White;
        
            Console.WriteLine($"[planex] executing step: {step}");
            var executor = new StepExecutor(ollama);
            var result = await executor.Run(step, state);
            state.Variables[step.Output] = result;
        }
    }
}
