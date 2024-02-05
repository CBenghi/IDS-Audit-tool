﻿using System.Xml;

namespace IdsLib.IdsSchema.Cardinality;

internal class MinMaxCardinality : ICardinality
{
    private readonly string minString;
    private readonly string maxString;

    public override string ToString()
    {
        return $"[{minString}..{maxString}]";
    }

    public bool IsRequired => minString == "1";

    public MinMaxCardinality(XmlReader reader)
    {
        // both default to "1" according to xml:xs specifications
        minString = reader.GetAttribute("minOccurs") ?? "1";
        maxString = reader.GetAttribute("maxOccurs") ?? "1";
    }

    internal const Audit.Status CardinalityErrorStatus = IdsLib.Audit.Status.IdsContentError;

    /// <summary>
    /// Audits the validity of an occurrence setting.
    /// </summary>
    /// <param name="errorMessage">if invalid returns an errors string without punctuation.</param>
    /// <returns>the evaluated status</returns>
    public Audit.Status Audit(out string errorMessage)
    {
        uint max;
        if (maxString == "unbounded")
            max = uint.MaxValue;
        else if (!uint.TryParse(maxString, out max))
        {
            errorMessage = $"Invalid maxOccurs '{maxString}'";
            return CardinalityErrorStatus;
        }
        if (!uint.TryParse(minString, out var min))
        {
            errorMessage = $"Invalid minOccurs '{minString}'";
            return CardinalityErrorStatus;
        }
        if (max < min)
        {
            errorMessage = $"Invalid range '{minString}' to `{maxString}`";
            return CardinalityErrorStatus;
        }
        if (
            min > 1 ||
            max != 0 && max != uint.MaxValue
            )
        {
            errorMessage = $"Invalid configuration for IDS implementation agreements {this}";
            return CardinalityErrorStatus;
        }

        errorMessage = string.Empty;
        return IdsLib.Audit.Status.Ok;
    }
}