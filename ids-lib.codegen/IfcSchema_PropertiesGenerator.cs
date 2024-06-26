﻿using System;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4x3.Kernel;
using Xbim.Properties;

namespace IdsLib.codegen;

public class IfcSchema_PropertiesGenerator
{
    private class propSetTempInfo
    {
        public string Name { get; set; } = string.Empty;
		public IEnumerable<string>? ApplicableClasses { get; set; }
		public IEnumerable<string>? CsSourceOfProperties { get; set; }

	}

	/// <summary>
	/// Computes the GetPropertiesIFC2x3, GetPropertiesIFC4 and GetPropertiesIFC4x3 of the PropertySetInfo.Generated.cs file 
	/// Depends on the Xbim.Properties assembly.
	/// </summary>
	public static string Execute()
    {
        var source = stub;
		FileInfo f = new FileInfo("buildingSMART\\Pset_IFC4X3.ifc");
        var schemas = new[] { Xbim.Properties.Version.IFC2x3, Xbim.Properties.Version.IFC4, Xbim.Properties.Version.IFC4x3 };
        foreach (var schema in schemas)
        {
            var sb = new StringBuilder();
            var propsets = GetPsets(schema);
			if (schema == Xbim.Properties.Version.IFC4x3)
				propsets = GetPsets(f).OrderBy(x => x.Name).ToArray();
			foreach (var item in propsets)
			{
				if (item.ApplicableClasses is null)
					continue;
				var cArr = NewStringArray(item.ApplicableClasses);
				if (item.CsSourceOfProperties is null)
					continue;
				var rpArr = NewTypeArray("IPropertyTypeInfo", item.CsSourceOfProperties);	
				sb.AppendLine($@"		yield return new PropertySetInfo(""{item.Name}"", {rpArr},{"\r\n"}				{cArr});");
			}
            source = source.Replace($"<PlaceHolder{schema}>\r\n", sb.ToString());
        }
        source = source.Replace($"<PlaceHolderVersion>\r\n", VersionHelper.GetFileVersion(typeof(Definitions<>)));
        return source;
    }

	private static IEnumerable<propSetTempInfo> GetPsets(FileInfo f)
	{
		var store = IfcStore.Open(f.FullName);
		var psets = store.Instances.OfType<Xbim.Ifc4x3.Kernel.IfcPropertySetTemplate>();
		foreach (var pset in psets)
		{
			propSetTempInfo ret = new propSetTempInfo() { Name = pset.Name ?? "" };
			var tmpClasses = (pset.ApplicableEntity!.Value.Value.ToString() ?? "").Split(",");
			ret.ApplicableClasses = tmpClasses.Select(x => RemovePredefinedType(x));
			var props = new List<string>();
			foreach (var proptemp in pset.HasPropertyTemplates.OrderBy(x=>x.Name.ToString()))
			{
				string def = "";
				if (!string.IsNullOrWhiteSpace(proptemp.Description))
				{
					def = $" {{ Definition = \"{EscapeCharacters(proptemp.Description)}\"}}";
				}
				if (proptemp is IfcSimplePropertyTemplate singleV)
				{
					// P_REFERENCEVALUE can also be listed, but value cannot be specified.
					// 
					switch (singleV.TemplateType)
					{
						case IfcSimplePropertyTemplateTypeEnum.Q_AREA:
							props.Add($"new SingleValuePropertyType(\"{singleV.Name}\", \"IfcAreaMeasure\"){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.Q_WEIGHT:
							props.Add($"new SingleValuePropertyType(\"{singleV.Name}\", \"IfcWeightMeasure\"){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.Q_LENGTH:
							props.Add($"new SingleValuePropertyType(\"{singleV.Name}\", \"IfcLengthMeasure\"){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.Q_VOLUME:
							props.Add($"new SingleValuePropertyType(\"{singleV.Name}\", \"IfcVolumeMeasure\"){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.Q_COUNT:
							props.Add($"new SingleValuePropertyType(\"{singleV.Name}\", \"IfcCountMeasure\"){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.Q_TIME:
							props.Add($"new SingleValuePropertyType(\"{singleV.Name}\", \"IfcTimeMeasure\"){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.P_ENUMERATEDVALUE:
							props.Add($"new EnumerationPropertyType(\"{singleV.Name}\", {NewStringArray(singleV.Enumerators.EnumerationValues.Select(x => x.Value.ToString() ?? ""))} ){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.P_SINGLEVALUE:
						case IfcSimplePropertyTemplateTypeEnum.P_REFERENCEVALUE:
						case IfcSimplePropertyTemplateTypeEnum.P_BOUNDEDVALUE:
							props.Add($"new SingleValuePropertyType(\"{singleV.Name}\", \"{singleV.PrimaryMeasureType}\"){def}");
							break;
						case IfcSimplePropertyTemplateTypeEnum.P_TABLEVALUE:
						case IfcSimplePropertyTemplateTypeEnum.P_LISTVALUE:
							continue;
						default:
							break;
					}
				}
			}
			ret.CsSourceOfProperties = props;
			yield return ret;
		}
	}

	private static string RemovePredefinedType(string x)
	{
		if (!x.Contains("/"))
			return x;
		return x.Substring(0, x.IndexOf("/"));
	}

	private static IEnumerable<propSetTempInfo> GetPsets(Xbim.Properties.Version schema)
	{
		var propertyDefinitions = new Definitions<PropertySetDef>(schema);
		propertyDefinitions.LoadAllDefault();
		foreach (var set in propertyDefinitions.DefinitionSets.OrderBy(x => x.Name))
		{
			propSetTempInfo ret = new propSetTempInfo();
			ret.Name = set.Name;
			ret.ApplicableClasses = set.ApplicableClasses.Select(x => x.ClassName).ToArray();
			var richProp = new List<string>();
			foreach (var prop in set.PropertyDefinitions.OrderBy(x => x.Name))
			{
				string def = "";
				if (!string.IsNullOrWhiteSpace(prop.Definition))
				{
					def = $" {{ Definition = \"{EscapeCharacters(prop.Definition)}\"}}";
				}
				if (prop.PropertyType.PropertyValueType is TypePropertySingleValue singleV)
				{
					var t = $"new SingleValuePropertyType(\"{prop.Name}\", \"{singleV.DataType.Type}\"){def}";
					//propTypes.Add(singleV.DataType.Type.ToString()!);
					richProp.Add(t);
				}
				else if (prop.PropertyType.PropertyValueType is TypePropertyBoundedValue range)
				{
					// todo: this case would have a range for value, that we are ignoring.
					var t = $"new SingleValuePropertyType(\"{prop.Name}\", \"{range.DataType.Type}\"){def}";
					richProp.Add(t);
				}
				else if (prop.PropertyType.PropertyValueType is TypePropertyEnumeratedValue enumV)
				{

					if (enumV.ConstantList.Any())
					{
						throw new Exception("Not implemented data structure.");
					}
					else
					{
						richProp.Add($"new EnumerationPropertyType(\"{prop.Name}\", {NewStringArray(enumV.EnumList.Items)} ){def}");
					}
				}
				else if (prop.PropertyType.PropertyValueType is TypePropertyReferenceValue refP)
				{
					// reference values do not have a testing method, and are ignored
					// e.g. they would refer to an IfcMaterial or IfcPerson
				}
				else if (prop.PropertyType.PropertyValueType is TypePropertyListValue lst)
				{
					// list values do not have a testing method, and are ignored
					// e.g. they could have multiple values for the property
				}
				else if (prop.PropertyType.PropertyValueType is TypeSimpleProperty simple)
				{
					// todo: maybe some SimpleProperties can be added to the list, this would need a review
					// list values do not have a testing method, and are ignored
					// e.g. they could have multiple values for the property
				}
				else if (prop.PropertyType.PropertyValueType is TypePropertyTableValue table)
				{
					// list values do not have a testing method, and are ignored
					// e.g. they could have multiple values for the property
				}
				else if (prop.PropertyType.PropertyValueType is TypeComplexProperty cmplex)
				{
					// complex types do not have a testing method, and are ignored
					// e.g. they could have multiple values for the property
				}
				else
				{
					var t = $"new NamedPropertyType(/* {prop.PropertyType.PropertyValueType?.GetType().Name} */\"{prop.Name}\"){def}";
					richProp.Add(t);
				}
			}
			ret.CsSourceOfProperties = richProp;
			yield return ret;	
		}
	}

	private static string EscapeCharacters(string definition)
    {
        // Provides a more robust means of escaping literals and special characters 
        var tmp = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(definition, false);
        tmp = tmp.Replace("\"", @"\""");
        return tmp;
    }


    private static string NewStringArray(IEnumerable<string> values)
    {
        var strippedValues = values.Select(x => x.Trim(' ', '\r', '\n'));
        return @$"new [] {{ ""{string.Join("\", \"", strippedValues)}"" }}";
    }

    private static string NewTypeArray(string type, IEnumerable<string> values)
    {
        return @$"new {type}[] {{{"\r\n"}			{string.Join(",\r\n\t\t\t", values)} }}";
    }

    private const string stub = @"// generated via source generation from ids-lib.codegen using Xbim.PropertySets <PlaceHolderVersion>

using System.Collections.Generic;

namespace IdsLib.IfcSchema;

/// <summary>
/// For reference see IdsLib.codegen.IfcSchema_PropertiesGenerator
/// </summary>
public partial class PropertySetInfo
{
	static IEnumerable<PropertySetInfo> GetPropertiesIFC2x3()
	{
<PlaceHolderIFC2x3>
	}

	private static IEnumerable<PropertySetInfo> GetPropertiesIFC4()
	{
<PlaceHolderIFC4>
	}

    private static IEnumerable<PropertySetInfo> GetPropertiesIFC4x3()
	{
<PlaceHolderIFC4x3>
	}
}

";
}