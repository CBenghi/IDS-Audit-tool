﻿using IdsLib.IdsSchema.IdsNodes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace IdsLib.SchemaProviders
{
    internal class AutomaticSchemaProvider : SchemaProvider, Audit.ISchemaProvider
    {
        public Audit.Status GetSchemas(IdsVersion vrs, ILogger? logger, out IEnumerable<XmlSchema> schemas)
        {
            return GetSchemasByVersion(vrs, logger, out schemas);
        }
    }

}
