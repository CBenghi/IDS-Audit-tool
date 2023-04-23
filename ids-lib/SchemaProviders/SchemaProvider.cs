﻿using IdsLib.IdsSchema.IdsNodes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Schema;

namespace IdsLib.SchemaProviders
{
    public abstract class SchemaProvider
    {
        protected static Audit.Status GetSchemasByVersion(IdsVersion vrs, ILogger? logger, out IEnumerable<XmlSchema> schemas)
        {
            var ret = Audit.Status.Ok;
            IEnumerable<string> tmpResources = Enumerable.Empty<string>();
            switch (vrs)
            {
                case IdsVersion.Ids0_9:
                case IdsVersion.Ids1_0:
                    tmpResources = new List<string> { "xsdschema.xsd", "xml.xsd", "ids.xsd" };
                    break;
                case IdsVersion.Invalid:
                    logger?.LogError("Embedded schemas for version {vrs} cannot be resolved.", vrs);
                    ret = Audit.Status.IdsContentError;
                    break;
                default:
                    logger?.LogError("Embedded schema for version {vrs} not implemented.", vrs);
                    ret = Audit.Status.NotImplementedError;
                    break;
            }
            schemas = tmpResources.Select(x => GetSchema(x)).ToList();
            return ret;
        }

        // todo: change to return status and out parameter for schemas
        protected static IEnumerable<XmlSchema> GetResourceSchemasFromImports(ILogger? logger, IEnumerable<string> imports)
        {
            var distinct = imports.Distinct();
            foreach (var schema in distinct)
            {
                switch (schema)
                {
                    case "http://www.w3.org/2001/xml.xsd":
                        yield return GetSchema("xml.xsd")!;
                        yield return GetSchema("xsdschema.xsd")!;
                        break;
                    case "https://www.w3.org/2001/XMLSchema.xsd":
                        break;
                    case "http://www.w3.org/2001/XMLSchema-instance":
                        break;
                    default:
                        logger?.LogError("Unexpected import schema {schema}.", schema);
                        break;
                }
            }
        }

        protected static XmlSchema GetSchema(string name)
        {
            var fullName = "IdsLib.Resources.XsdSchemas." + name;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(fullName)
                    ?? throw new NotImplementedException("Null resource stream.");
            var schema = XmlSchema.Read(stream, null)
                ?? throw new NotImplementedException("Invalid resource stream.");
            return schema;
        }
    }

}
