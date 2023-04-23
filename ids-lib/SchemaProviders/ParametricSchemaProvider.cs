using IdsLib.IdsSchema.IdsNodes;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Xml.Schema;

namespace IdsLib.SchemaProviders
{
    internal class ParametricSchemaProvider : SchemaProvider, Audit.ISchemaProvider
    {
        public Audit.Status GetSchemas(IdsVersion vrs, ILogger? logger, out IEnumerable<XmlSchema> schemas)
        {
            return GetResourceSchemasByVersion(vrs, logger, out schemas);
        }
    }

}
