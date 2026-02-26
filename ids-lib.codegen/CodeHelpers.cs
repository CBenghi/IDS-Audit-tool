namespace IdsLib.codegen;

internal class CodeHelpers
{
    internal static string NewStringArray(IEnumerable<string> classes)
    {
        return @$"new[] {{ ""{string.Join("\", \"", classes)}"" }}";
    }

	internal static string TrueCheckString = "✓";
	internal static string FalseCheckString = " ";//"✗";
}
