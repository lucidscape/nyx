using Nyx;
using OllamaSharp;
using Spectre.Console;

Console.ResetColor();

AnsiConsole.Write(new Rule("OllamaSharp Api Console").LeftJustified());
AnsiConsole.WriteLine();

OllamaApiClient? ollama = null;
var connected = false;

do
{
    var url = "http://nyx-server:11434";

	var uri = new Uri(url);
	Console.WriteLine($"Connecting to {uri} ...");

	try
	{
		ollama = new OllamaApiClient(url);
		connected = await ollama.IsRunningAsync();

		var models = await ollama.ListLocalModelsAsync();
		if (!models.Any())
			Console.WriteLine($"Your Ollama instance does not provide any models :([/]");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"error: {ex}");
	}
} while (!connected);


var agent = new Agent(ollama!);

try
{
    await agent.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"error: {ex}");
}