using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Text.RegularExpressions;

namespace CsToMarkdown;
internal class SolutionParser
{
	public async Task Parse(string directoryPath, string outputPath)
	{

		foreach (var file in Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories))
		{
			Console.WriteLine($"Processing {file}...");
			var code = File.ReadAllText(file);
			var syntaxTree = CSharpSyntaxTree.ParseText(code);

			var root = syntaxTree.GetRoot();
			foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
			{
				var className = classDeclaration.Identifier.ValueText;
				Console.WriteLine($"Class: {className}");

				using (var writer = new StreamWriter(Path.Combine(outputPath, $"{className}.md")))
				{
					writer.WriteLine($"# {className}\n");

					writer.WriteLine($"### Inheritance {GetInheritanceInfo(classDeclaration)}");

					var classSummary = GetSummary(classDeclaration.GetLeadingTrivia());
					if (!string.IsNullOrWhiteSpace(classSummary))
					{
						writer.WriteLine($"\n{classSummary}\n");
					}

					writer.WriteLine($"{SpecialCharacters.H2} Fields\n");
					foreach (var field in classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
					{
						var fieldName = field.Declaration.Variables.First().Identifier.ValueText;
						var fieldType = FormatTypeName(field.Declaration.Type.ToString());
						var fieldSummary = GetSummary(field.GetLeadingTrivia());
						writer.WriteLine($"	- [[{fieldType}]] {fieldName}");
						if (!string.IsNullOrWhiteSpace(fieldSummary))
						{
							writer.WriteLine($"{Indentation.LVL2}{SpecialCharacters.Bullet}Summary:\n{fieldSummary}");
						}
					}

					writer.WriteLine($"\n{SpecialCharacters.H2} Properties\n");
					foreach (var property in classDeclaration.DescendantNodes().OfType<PropertyDeclarationSyntax>())
					{
						var propertyName = property.Identifier.ValueText;
						var propertyType = FormatTypeName(property.Type.ToString());
						var propertySummary = GetSummary(property.GetLeadingTrivia());
						writer.WriteLine($"	- [[{propertyType}]] {propertyName}");
						if (!string.IsNullOrWhiteSpace(propertySummary))
						{
							writer.WriteLine($"{Indentation.LVL2}{SpecialCharacters.Bullet}Summary:\n{propertySummary}");
						}
					}

					writer.WriteLine("\n## Methods\n");
					foreach (var method in classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>())
					{
						var methodName = method.Identifier.ValueText;
						var methodSummary = GetSummary(method.GetLeadingTrivia());

						writer.WriteLine($"{Indentation.LVL1}{SpecialCharacters.Bullet}{SpecialCharacters.H3}{FormatTypeName(method.ReturnType.ToString())} {methodName}");
						writer.WriteLine($"{Indentation.LVL2}{SpecialCharacters.Bullet}{SpecialCharacters.H4} Parameters: {GetMethodParameters(method)}");
						if (!string.IsNullOrWhiteSpace(methodSummary))
						{
							writer.WriteLine($"{Indentation.LVL3}{SpecialCharacters.Bullet}Summary:\n{methodSummary}");
						}
						ProcessMethod(method, writer);
					}
				}
			}
		}
	}

	static void ProcessMethod(MethodDeclarationSyntax method, StreamWriter writer)
	{
		var invokedMembers = method.DescendantNodes()
			.OfType<InvocationExpressionSyntax>()
			.SelectMany(invocation => invocation.DescendantNodes().OfType<IdentifierNameSyntax>())
			.Select(identifier => identifier.Identifier.ValueText);

		if (invokedMembers.Any())
		{
			writer.WriteLine("  - **Invoked methods/properties:**");
			foreach (var member in invokedMembers.Distinct())
			{
				writer.WriteLine($"    - {member}");
			}
		}

		var objectCreations = method.DescendantNodes()
			.OfType<ObjectCreationExpressionSyntax>()
			.Select(creation => creation.Type.ToString())
			.Distinct();

		if (objectCreations.Any())
		{
			writer.WriteLine("	- **Objects created:**");
			foreach (var obj in objectCreations)
			{
				writer.WriteLine($"		- {FormatTypeName(obj)}");
			}
		}
	}

	static string GetSummary(SyntaxTriviaList leadingTrivia)
	{
		var summaryText = leadingTrivia
			.SelectMany(trivia => trivia.GetStructure()?.ChildNodes().OfType<XmlElementSyntax>() ?? Enumerable.Empty<XmlElementSyntax>())
			.FirstOrDefault(node => node.StartTag.Name.ToString() == "summary")?
			.Content.ToString();

		return summaryText?.Trim();
	}

	static string GetInheritanceInfo(ClassDeclarationSyntax classDeclaration)
	{
		var inheritanceInfo = classDeclaration.BaseList?.Types.Select(x => x.ToString());
		return inheritanceInfo != null ? ": " + string.Join(", ", $"[[{inheritanceInfo}]]") : string.Empty;
	}

	static string GetAttributes(SyntaxList<AttributeListSyntax> attributeLists)
	{
		var attributes = attributeLists.SelectMany(attrList => attrList.Attributes.Select(attr => attr.ToString()));
		return string.Join(", ", attributes);
	}

	static string GetMethodParameters(MethodDeclarationSyntax method)
	{
		var parameters = method.ParameterList.Parameters
			.Select(p => $"[[{p.Type}]] `{p.Identifier}` {GetParameterDefault(p)}");
		return "(" + string.Join(", ", parameters) + ")";
	}

	static string GetParameterDefault(ParameterSyntax parameter)
	{
		return parameter.Default != null ? $" = {parameter.Default.Value}" : string.Empty;
	}

	static string GetExceptionDocumentation(MethodDeclarationSyntax method)
	{
		var exceptions = method.GetLeadingTrivia()
			.SelectMany(trivia => trivia.GetStructure()?.ChildNodes().OfType<XmlElementSyntax>() ?? Enumerable.Empty<XmlElementSyntax>())
			.Where(node => node.StartTag.Name.ToString() == "exception")
			.Select(node => $"{node.StartTag.Attributes.FirstOrDefault()} - {node.Content.ToString().Trim()}");
		return string.Join("\n", exceptions);
	}
	static string FormatTypeName(string typeName)
	{
		// Pattern to match generic types (e.g., List<Parcel>)
		var genericTypePattern = @"(\w+)<(.+)>";
		var match = Regex.Match(typeName, genericTypePattern);

		if (match.Success)
		{
			// If the type is a generic type, format it accordingly
			var genericType = match.Groups[1].Value; // e.g., List
			var typeArguments = match.Groups[2].Value; // e.g., Parcel
													   // Splitting multiple generic arguments if present and formatting them as links
			var formattedArguments = string.Join(", ", typeArguments.Split(',')
														.Select(arg => $"[[{arg.Trim()}]]"));
			return $"{genericType}<{formattedArguments}>"; // e.g., List<[[Parcel]]>
		}
		else
		{
			// If not a generic type, simply return the type name, potentially as a link
			return $"[[{typeName}]]";
		}
	}

	static string FormatTypeNameWithNamespace(string typeName)
	{
		// Replace generic type definitions' angle brackets for markdown compatibility
		typeName = Regex.Replace(typeName, @"<", "&lt;").Replace(@">", "&gt;");

		// Converts Namespace.a1.a2.ClassName to [[Namespace/a1/a2/ClassName]]
		var formattedName = typeName.Replace('.', '/');
		return $"[[{formattedName}]]";
	}
}
