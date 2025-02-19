
using System.Text;
using System.Text.Json;
using OllamaSharp;
using Spectre.Console;

namespace Nyx;

record Response(string Command, string Data);

class Agent(IOllamaApiClient ollama) {

    public async Task Run() {
        ollama.SelectedModel = await SelectModel();
        Console.WriteLine($"[agent] using model {ollama.SelectedModel}");
            
        var systemPrompt = """
you are a chatbot agent, you operate in steps always returning a command and receiving the response of that command as the input to the next step
        
The following commands are available:
task () // gets the current task
say (text: string) // text response to the user
search (query: string) -> string // search for information that is not known

your response must be in the format: {command: string, data: string}
""";

        string task = "find out the current weather";
        var thinking = false;
        var step = 0;

        var chat = new Chat(ollama, systemPrompt);

        // string message = "what is the current weather?";
        var message = "complete tasks";
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
            
        do
        {
            Console.WriteLine($"[agent/{step}] >>");
            response.Clear();
            
            Console.ForegroundColor = ConsoleColor.Blue;
            
            var format = new
                {
                    type = "object",
                    properties = new
                    {
                        command = new
                        {
                            type = "string",
                        },
                        data = new
                        {
                            type = "string",
                        },
                    },
                    required = new[] { "command", "data" }
                };
        
            await foreach (var answerToken in chat.SendAsync(message, null, null, format))
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
            
            var responseObject = JsonSerializer.Deserialize<Response>(response.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (responseObject == null)
            {
                Console.WriteLine("failed to deserialize response");
                continue;
            }
            
            if (commands.TryGetValue(responseObject.Command, out var value))
            {
                message = value(responseObject.Data);
            }
            else
            {
                Console.WriteLine("Unknown command: " + responseObject.Command);
            }
            
            step += 1;
            
        } while (!string.IsNullOrEmpty(message));
        
    }

	protected async Task<string> SelectModel()
	{
		var models = await ollama.ListLocalModelsAsync();
		var modelsWithBackChoice = models.OrderBy(m => m.Name).Select(m => m.Name).ToList();
        
        if (modelsWithBackChoice.Count == 0)
        {
            throw new Exception("No models available");
        }
        
        return modelsWithBackChoice.First();
	}
}
