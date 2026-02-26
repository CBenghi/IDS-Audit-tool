using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common.Metadata;

namespace IdsLib.codegen
{
	internal static class stringExtensions
	{
		internal static string FixMarkup(this string str)
		{
			str = str.Replace("|", "&#124;");
			str = str.Replace("[", "&#91;");
			str = str.Replace("]", "&#93;");
			return str;
		}
	}

	internal class IfcSchema_DocumentationGenerator
	{ 
		internal static string Execute(Dictionary<string, typeMetadata> dataTypeDictionary, Dictionary<string, string[]> restrictionNodes)
		{
			var schemas = new string[] { "Ifc2x3", "Ifc4", "Ifc4x3" };

			var sbDataTypes = new StringBuilder();
			foreach (var dataType in dataTypeDictionary.Values.OrderBy(x=>x.Name))
			{
				var checks = schemas.Select(x => dataType.Schemas.Contains(x) ? $"   {CodeHelpers.TrueCheckString}   " : $"   {CodeHelpers.FalseCheckString}   ");
				sbDataTypes.AppendLine($"| {dataType.Name,-45} | {string.Join(" | ", checks),-24} | {dataType.XmlBackingType,-21} |");
			}

			var allOptions = restrictionNodes.SelectMany(kvp => kvp.Value).Distinct().ToArray();


			var sbXmlTypes = new StringBuilder();
			var xmlTypes = dataTypeDictionary.Values.Select(x => x.XmlBackingType).Where(str => !string.IsNullOrWhiteSpace(str)).Distinct();
			// deal with spacing
			var halfLen = allOptions.Max(x => (int)(x.Length / 2));
			var fullLen = 2 * halfLen + 1;
			var halfSpace = new string(' ', halfLen);
			var justifySpace = allOptions.Select( x=> $":{new string('-', fullLen - 2)}:");

			foreach (var dataType in xmlTypes.OrderBy(x => x))
			{
				var t =  "<code>" + XmlSchema_XsTypesGenerator.GetRegexString(dataType).FixMarkup() + "</code>";
				// var t =  "`" + XmlSchema_XsTypesGenerator.GetRegexString(dataType) + "`";
				var dt = dataType.Replace(":", "");
				var nodes = restrictionNodes.TryGetValue(dt, out var n) ? n : Array.Empty<string>();

				var checks = new List<string>();
				foreach (var item in allOptions)
				{
					if (nodes.Contains(item))
						checks.Add(CodeHelpers.TrueCheckString);
					else
						checks.Add(CodeHelpers.FalseCheckString);
				}
				var checksString = string.Join(" | ", checks.Select(x => $"{halfSpace}{x}{halfSpace}"));

				sbXmlTypes.AppendLine($"| {dataType,-11} | {t,-133} | {string.Join(", ", checksString),-100 } |");
			}
			

			var source = stub;
			source = source.Replace($"<PlaceHolderRestrictionOptions>", string.Join(" | ", allOptions.Select(x=> x.PadRight(fullLen))));
			source = source.Replace($"<PlaceHolderRestrictionJustification>", string.Join(" | ", justifySpace));
			source = source.Replace($"<PlaceHolderDataTypes>", sbDataTypes.ToString().TrimEnd('\r', '\n'));
			source = source.Replace($"<PlaceHolderXmlTypes>", sbXmlTypes.ToString().TrimEnd('\r', '\n'));
			return source;
			// Program.Message($"no change.", ConsoleColor.Green);
		}

		private const string stub = """
			# Type constraining

			## DataTypes

			Property dataTypes can be set to any values according to the following table.

			Columns of the table determine the validity of the type depending on the schema version and the required `xs:base` type for any `xs:restriction` constraint.

			| dataType                                      | Ifc2x3  | Ifc4    | Ifc4x3  | Restriction base type |
			| --------------------------------------------- | :-----: | :-----: | :-----: | --------------------- |
			<PlaceHolderDataTypes>

			Please note that [IFCSTRIPPEDOPTIONAL](https://standards.buildingsmart.org/IFC/RELEASE/IFC4_3/HTML/lexical/IfcStrippedOptional.htm) is a special data type that should never be instantiated, but it is listed here for schema tolerance reasons.

			## XML base types

			The list of valid XML base types for the `base` attribute of `xs:restriction`, the associated regex expression to check for the validity of string representation, and the allowed nodes inside the `xs:restriction` are as follows:

			| Base type   | Value string regex constraint                                                                                                         | <PlaceHolderRestrictionOptions> |
			| ----------- | ------------------------------------------------------------------------------------------------------------------------------------- | <PlaceHolderRestrictionJustification> |
			<PlaceHolderXmlTypes>

			For example:

			- To specify numbers: you must use a dot as the decimal separator, and not use a thousands separator (e.g. `4.2` is valid, but `1.234,5` is invalid). Scientific notation is allowed (e.g. `1e3` to represent `1000`).
			- To specify a boolean: valid values are `true` or `false`, `0`, or `1`.

			## Notes

			Please note, this document has been automatically generated via the [IDS Audit Tool repository](https://github.com/buildingSMART/IDS-Audit-tool), any changes should be initiated there.

			""";
	}
}