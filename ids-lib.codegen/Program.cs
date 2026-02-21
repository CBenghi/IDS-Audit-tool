namespace IdsLib.codegen;

internal class Program
{
    static void Main()
    {
        Console.WriteLine("Running code generation for ids-lib.");
        if (IdsRepo_Updater.UpdateRequiresRestart())
        {
            Message("Local code updated, need to restart the generation.", ConsoleColor.Yellow);
            return;
        }
        var GeneratedContentChanged = false;

		GeneratedContentChanged = EvaluateContentChanged(
			IfcSchema_Ifc2x3MapperGenerator.Execute(out var maps),
			@"ids-lib\IfcSchema\SchemaInfo.Ifc2x3Maps.g.cs") | GeneratedContentChanged;
		IfcSchema_Ifc2x3MapperGenerator.CheckExist(maps);

		GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_ObjectToTypeGenerator.Execute(),
            @"ids-lib\IfcSchema\SchemaInfo.ObjectTypes.g.cs") | GeneratedContentChanged;

		GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_ClassAndAttributeNamesGenerator.Execute(maps),
            @"ids-lib\IfcSchema\SchemaInfo.ClassAndAttributeNames.g.cs") | GeneratedContentChanged;

        GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_DatatypeNamesGenerator.Execute(out var dataTypeDictionary),
            @"ids-lib\IfcSchema\SchemaInfo.MeasureNames.g.cs") | GeneratedContentChanged;

		GeneratedContentChanged = EvaluateContentChanged(
			IdsSchema_RestrictionTypeGenerator.Execute(out var restrictionNodes),
			@"ids-lib\IdsSchema\XsNodes\XsRestriction.g.cs") | GeneratedContentChanged;

		// creates the datatypes md and compares it with the existing version
		GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_DocumentationGenerator.Execute(dataTypeDictionary, restrictionNodes),
            @"ids-lib.codegen\buildingSMART\DataTypes.md") | GeneratedContentChanged;

		GeneratedContentChanged = EvaluateContentChanged(
			OnlineResource_Getter.Execute("https://raw.githubusercontent.com/buildingSMART/IFC4.3.x-development/refs/heads/master/docs/schemas/resource/IfcMeasureResource/Entities/IfcConversionBasedUnit.md"),
			@"ids-lib.codegen\buildingSMART\IfcConversionBasedUnit.md") | GeneratedContentChanged;

		GeneratedContentChanged = EvaluateContentChanged(
			IfcSchema_ConversionUnitHelperGenerator.Execute(),
			@"ids-lib\IfcSchema\SchemaInfo.ConversionUnitsHelper.g.cs") | GeneratedContentChanged;

		GeneratedContentChanged = EvaluateContentChanged(
            XmlSchema_XsTypesGenerator.Execute(dataTypeDictionary),
            @"ids-lib\IdsSchema\XsNodes\XsTypes.g.cs") | GeneratedContentChanged;

        GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_ClassGenerator.Execute(maps),
            @"ids-lib\IfcSchema\SchemaInfo.Schemas.g.cs") | GeneratedContentChanged;

        GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_AttributesGenerator.Execute(dataTypeDictionary, maps),
            @"ids-lib\IfcSchema\SchemaInfo.Attributes.g.cs") | GeneratedContentChanged;

        GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_PartOfRelationGenerator.Execute(),
            @"ids-lib\IfcSchema\SchemaInfo.PartOfRelations.g.cs") | GeneratedContentChanged;

        GeneratedContentChanged = EvaluateContentChanged(
            IfcSchema_PropertiesGenerator.Execute(),
            @"ids-lib\IfcSchema\SchemaInfo.Properties.g.cs") | GeneratedContentChanged;

        GeneratedContentChanged = EvaluateContentChanged(
            IdsTool_DocumentationUpdater.Execute(),
            @"ids-tool\README.md") | GeneratedContentChanged;

        if (GeneratedContentChanged)
        {
            Message("Generated code updated, need to restart the generation.", ConsoleColor.Yellow);
            return;
        }

        // documentation changes do not require restart of code generation
        IdsLib_DocumentationUpdater.Execute();
    }

    private static bool EvaluateContentChanged(string? content, string solutionRelativePath)
    {
		if (string.IsNullOrWhiteSpace(content))
		{
			Message($"Warning: {solutionRelativePath} skipped because empty.", ConsoleColor.Yellow);
			return false;
		}
		Console.Write($"Evaluating: {solutionRelativePath}... ");
        var solutionPathFolder = new DirectoryInfo(@"..\..\..\..\");
		string solutionPathAsString = solutionPathFolder.FullName.ToString();
		var destinationFullName = Path.Combine(solutionPathAsString, solutionRelativePath);
        if (File.Exists(destinationFullName))
        {
            var current = File.ReadAllText(destinationFullName);
            if (content == current)
            {
                Message($"no change.", ConsoleColor.Green);
                return false;
            }
        }

        File.WriteAllText(destinationFullName, content);
        Message($"updated.", ConsoleColor.DarkYellow);
        return true;
    }

    internal static void Message(string v, ConsoleColor messageColor)
    {
        var restore = Console.ForegroundColor;
        Console.ForegroundColor = messageColor;
        Console.WriteLine(v);
        Console.ForegroundColor = restore;
    }

    static internal string[] schemas { get; } = new[] { "Ifc2x3", "Ifc4", "Ifc4x3" };
}