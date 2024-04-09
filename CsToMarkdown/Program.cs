using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

namespace CsToMarkdown
{
	internal static class Indentation
	{
		public const string LVL0 = "";
		public const string LVL1 = "	";
		public const string LVL2 = "		";
		public const string LVL3 = "			";
		public const string LVL4 = "				";
	}

	internal static class SpecialCharacters
	{
		public const string NewLine = "\n";
		public const string Bullet = "- ";
		public const string H1 = "#";
		public const string H2 = "##";
		public const string H3 = "###";
		public const string H4 = "####";
		public const string H5 = "#####";
	}

	class Program
	{
		static async Task Main(string[] args)
		{
			// Define options
			var inputOption = new Option<string>(
				aliases: new[] { "--input", "-i" },
				description: "Specifies the input directory path.");
			var outputOption = new Option<string>(
				aliases: new[] { "--output", "-o" },
				description: "Specifies the output directory path.");

			// Create a root command
			var rootCommand = new RootCommand("A tool to generate markdown from C# code.");
			rootCommand.AddOption(inputOption);
			rootCommand.AddOption(outputOption);

			rootCommand.SetHandler(MainOperations.MainCommand, inputOption, outputOption);

			CommandLineBuilder builder = new CommandLineBuilder(rootCommand);

			builder.UseDefaults();
			Parser p = builder.Build();

			await p.InvokeAsync(args);

		}

		internal class MainOperations
		{
			public static void MainCommand(string input, string output)
			{
				SolutionParser solutionParser = new SolutionParser();
				solutionParser.Parse(input, output).Wait();
			}
		}
	}
}
