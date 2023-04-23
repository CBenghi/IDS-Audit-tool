using IdsLib.IdsSchema.IdsNodes;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Xml.Schema;

namespace IdsLib;

public static partial class Audit
{
    public interface ISchemaProvider
    {
        Audit.Status GetSchemas(IdsVersion vrs, ILogger? logger, out IEnumerable<XmlSchema> schemas);
    }
}
