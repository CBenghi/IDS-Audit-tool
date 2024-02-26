# IfcSchemaVersionsExtensions.ParseXmlSchemasFromAttribute method

Converts a set of IFC schema strings, concatenated in a single space separated string (useful for XML attribute reading).

```csharp
public static IfcSchemaVersions ParseXmlSchemasFromAttribute(
    this string spaceSeparatedSchemaStrings)
```

| parameter | description |
| --- | --- |
| spaceSeparatedSchemaStrings | A single string possibly containing multiple space separated values to be evaluated |

## Return Value

A single enumeration value representing all the *spaceSeparatedSchemaStrings*

## See Also

* enum [IfcSchemaVersions](../IfcSchemaVersions.md)
* class [IfcSchemaVersionsExtensions](../IfcSchemaVersionsExtensions.md)
* namespace [IdsLib.IfcSchema](../../ids-lib.md)

<!-- DO NOT EDIT: generated by xmldocmd for ids-lib.dll -->