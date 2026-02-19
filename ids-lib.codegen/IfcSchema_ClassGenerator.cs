using System.Diagnostics;
using System.Reflection;
using System.Text;
using Xbim.Common.Metadata;
using static IdsLib.codegen.IfcSchema_Ifc2x3MapperGenerator;

namespace IdsLib.codegen;

public class IfcSchema_ClassGenerator
{
    /// <summary>
    /// SchemaInfo.GeneratedClass.cs
    /// </summary>
    internal static string Execute(List<Ifc2x3EntityMappingInformation> maps)
    {
        var source = stub;
        foreach (var schema in Program.schemas)
        {
			List<TypeMapper> entities = TypeMapper.GetFor(schema, maps);
			var sb = new StringBuilder();

            foreach (var classMap in entities)
            {
                var t = classMap.IfcMapToExpressType.Type.GetInterfaces().Select(x => x.Name).Contains("IExpressValueType");
                //if (t ) //!string.IsNullOrEmpty(daType?.UnderlyingType?.Name))
                //{
                //    Debug.WriteLine($"{daType.Name}: {daType.UnderlyingType.Name} - {t}");
                //}

                // Enriching schema with predefined types
                var propPdefT = classMap.IfcMapToExpressType.Properties.Values.FirstOrDefault(x => x.Name == "PredefinedType");
                var predType = "Enumerable.Empty<string>()";
                if (propPdefT != null)
                {
                    var pt = propPdefT.PropertyInfo.PropertyType;
                    pt = Nullable.GetUnderlyingType(pt) ?? pt;
                    var vals = Enum.GetValues(pt);

                    List<string> pdtypes = new();
                    foreach (var val in vals)
                    {
                        if (val is null)
                            continue;
                        pdtypes.Add(val.ToString()!);
                    }
                    predType = NewStringArray(pdtypes.ToArray());
                }

                // other fields
                var abstractOrNot = classMap.IfcMapToExpressType.Type.IsAbstract ? "ClassType.Abstract" : "ClassType.Concrete";
                var ns = classMap.IfcMapToExpressType.Type.Namespace![5..];

                // Enriching schema with attribute names
                var attnames = NewStringArray(classMap.IfcMapToExpressType.Properties.Values.Select(x => x.Name).ToArray());
                sb.AppendLine($@"			new ClassInfo(""{classMap.IdsName}"", ""{classMap.IfcMapToExpressType.SuperType?.Name}"", {abstractOrNot}, {predType}, ""{ns}"", {attnames}),");
            }
            source = source.Replace($"<PlaceHolder{schema}>\r\n", sb.ToString());
        }
        source = source.Replace($"<PlaceHolderVersion>", VersionHelper.GetFileVersion(typeof(ExpressMetaData)));
        return source;
    }

    private static string NewStringArray(string[] classes)
    {
        return @$"new[] {{ ""{string.Join("\", \"", classes)}"" }}";
    }

    private const string stub = """
		// generated code via ids-lib.codegen using Xbim.Essentials <PlaceHolderVersion>, any changes to this file will be lost at next regeneration

		using System.Linq;

		namespace IdsLib.IfcSchema;

		public partial class SchemaInfo
		{
			private static partial SchemaInfo GetClassesIFC2x3()
			{
				var schema = new SchemaInfo(IfcSchemaVersions.Ifc2x3) {
		<PlaceHolderIfc2x3>
				};
				return schema;
			}

			private static partial SchemaInfo GetClassesIFC4() 
			{
				var schema = new SchemaInfo(IfcSchemaVersions.Ifc4) {
		<PlaceHolderIfc4>
				};
				return schema;
			}

		    private static partial SchemaInfo GetClassesIFC4x3() 
			{
				var schema = new SchemaInfo(IfcSchemaVersions.Ifc4x3) {
		<PlaceHolderIfc4x3>
				};
				return schema;
			}
		}
		""";
}