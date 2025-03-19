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
        
        Console.WriteLine("Models:");
        foreach (var model in models)
        {
            Console.WriteLine($"- {model.Name}");
        }
            
	}
	catch (Exception ex)
	{
		Console.WriteLine($"error: {ex}");
	}
} while (!connected);


// var agent = new Agent(ollama!);

// try
// {
//     await agent.Run();
// }
// catch (Exception ex)
// {
//     Console.WriteLine($"error: {ex}");
// }

var agent = new TaskPlanner(ollama!);
var executor = new PlanExecutor(ollama!);

try
{
    var task = "find out what the current weather is in the user's current location";

    var plan = await agent.Run(task);
    if (plan.Validate())
    {
        await executor.Run(plan);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"error: {ex}");
}



// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.Emit;
// using System.Reflection;

// void ExecuteCode(string code) {
//     SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
//     string assemblyName = Path.GetRandomFileName();
//     var references = AppDomain.CurrentDomain.GetAssemblies()
//         .Where(a => !a.IsDynamic)
//         .Select(a => MetadataReference.CreateFromFile(a.Location))
//         .ToList();

//     CSharpCompilation compilation = CSharpCompilation.Create(
//         assemblyName,
//         [syntaxTree],
//         references,
//         new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

//     using var ms = new MemoryStream();
//     EmitResult result = compilation.Emit(ms);

//     if (!result.Success)
//     {
//         foreach (Diagnostic diagnostic in result.Diagnostics)
//         {
//             Console.WriteLine($"{diagnostic.Id}: {diagnostic.GetMessage()}");
//         }
//     }
//     else
//     {
//         ms.Seek(0, SeekOrigin.Begin);
//         Assembly assembly = Assembly.Load(ms.ToArray());
//         var type = assembly.GetType("DynamicCode") ?? throw new Exception("Failed to get type");
//         var instance = Activator.CreateInstance(type);
//         var method = type.GetMethod("Execute") ?? throw new Exception("Failed to get method");
//         method.Invoke(instance, null);
//     }

// }

// ExecuteCode(@"
// using System;

// public class DynamicCode
// {
//     public void Execute()
//     {
//         Console.WriteLine(""Hello from dynamically compiled code!"");
//     }
// }");



// void ListAgentTools()
// {
//     var currentAssembly = Assembly.GetExecutingAssembly();
    
//     var agentTools = currentAssembly.GetTypes()
//         .SelectMany(t => t.GetMethods())
//         .Where(m => m.GetCustomAttributes(typeof(AgentToolAttribute), false).Length > 0)
//         .Select(m => new
//         {
//             Method = m,
//             Attribute = (AgentToolAttribute)m.GetCustomAttributes(typeof(AgentToolAttribute), false).First()
//         });

//     foreach (var tool in agentTools)
//     {
//         Console.WriteLine($"Method: {tool.Method.Name}, Name: {tool.Attribute.Name}, Description: {tool.Attribute.Description}");
//     }
// }


// ListAgentTools();

// public class AgentTools {

//     [AgentTool("TestAgentTool", "This is a test agent tool")]
//     public static string TestAgentTool(string input) {
//         return "Hello from TestAgentTool!";
//     }
// }


// [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
// sealed class AgentToolAttribute : Attribute
// {
//     public string Name { get; }
//     public string Description { get; }

//     public AgentToolAttribute(string name, string description)
//     {
//         Name = name;
//         Description = description;
//     }
// }

