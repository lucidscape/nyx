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
	AnsiConsole.MarkupLine($"Enter the Ollama [{OllamaConsole.AccentTextColor}]machine name[/] or [{OllamaConsole.AccentTextColor}]endpoint url[/]");

	var url = OllamaConsole.ReadInput();

	if (string.IsNullOrWhiteSpace(url))
		url = "http://localhost:11434";

	if (!url.StartsWith("http"))
		url = "http://" + url;

	if (url.IndexOf(':', 5) < 0)
		url += ":11434";

	var uri = new Uri(url);
	Console.WriteLine($"Connecting to {uri} ...");

	try
	{
		ollama = new OllamaApiClient(url);
		connected = await ollama.IsRunningAsync();

		var models = await ollama.ListLocalModelsAsync();
		if (!models.Any())
			AnsiConsole.MarkupLineInterpolated($"[{OllamaConsole.WarningTextColor}]Your Ollama instance does not provide any models :([/]");
	}
	catch (Exception ex)
	{
		AnsiConsole.MarkupLineInterpolated($"[{OllamaConsole.ErrorTextColor}]{Markup.Escape(ex.Message)}[/]");
		AnsiConsole.WriteLine();
	}
} while (!connected);


try
{
    await new ChatConsole(ollama!).Run();
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"An error occurred. Press [{OllamaConsole.AccentTextColor}]Return[/] to start over.");
    AnsiConsole.MarkupLineInterpolated($"[{OllamaConsole.ErrorTextColor}]{Markup.Escape(ex.Message)}[/]");
    Console.ReadLine();
}