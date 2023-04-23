﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Schema;
using System.Xml;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using IdsLib.IdsSchema;
using IdsLib.IdsSchema.IdsNodes;
using System.Data;
using IdsLib.SchemaProviders;
using System.Runtime.InteropServices;
using System.Runtime;

namespace IdsLib;

/// <summary>
/// Main static class for invoking the audit functions.
/// 
/// If you wish to audit a single file, the best entry point is <see cref="Run(Stream, SingleAuditOptions, ILogger?)"/>.
/// This method allows you to run audits on the provided stream.
/// 
/// For more complex auditing scenarios (e.g. those used by the tool), some automation can be achieved with <see cref="Run(IdsLib.IBatchAuditOptions, ILogger?)"/>.
/// 
/// Both APIs provide a return value that can be interpreted to determine if errors have been found.
/// 
/// For more detailed feedback on the specific location of issues encountered, you must pass an <see cref="ILogger"/> interface, and collect events.
/// </summary>
public static partial class Audit
{
    /// <summary>
    /// Summary return status of the audit functions
    /// </summary>
    [Flags]
    public enum Status
    {
        /// <summary>
        /// No errors encountered
        /// </summary>
        Ok = 0,
        /// <summary>
        /// The tool did not complete all the audits, because some aspect of the process are not implemented yet
        /// </summary>
        NotImplementedError = 1 << 0,
        /// <summary>
        /// The options provided in input are incomplete or inconsistent.
        /// </summary>
        InvalidOptionsError = 1 << 1,
        /// <summary>
        /// A resources passed via file name was not found.
        /// </summary>
        NotFoundError = 1 << 2,
        /// <summary>
        /// When auditing an IDS, one or more errors were encountered in the XML structure (includes XSD compliance errors).
        /// Depending on the <see cref="AuditProcessOptions.XmlWarningAction"/> property, this might include XSD schema warnings.
        /// </summary>
        IdsStructureError = 1 << 3,
        /// <summary>
        /// When auditing an IDS, one or more errors encountered auditing against the implementation agreement.
        /// </summary>
        IdsContentError = 1 << 4,
        /// <summary>
        /// A custom XSD was passed, but it could not be used because of an error in its content or structure.
        /// </summary>
        XsdSchemaError = 1 << 6,
        /// <summary>
        /// An unmanaged error occurred in the main audit methods. Please contact the authors to address the problem.
        /// </summary>
        UnhandledError = 1 << 7,
        /// <summary>
        /// When auditing an IDS, one or more warnings were encountered in the XML structure as defined by the XSD schemas.
        /// Triggering this status is configurable using the <see cref="AuditProcessOptions.XmlWarningAction"/> property.
        /// </summary>
        IdsStructureWarning = 1 << 8,
    }

    /// <summary>
    /// Main entry point to access the library features via a stream to read the IDS content.
    /// </summary>
    /// <param name="idsSource">the stream providing access to the content of the IDS to be audited</param>
    /// <param name="options">specifies the behaviour of the audit</param>
    /// <param name="logger">the optional logger provides fine-grained feedback on all the audits performed and any issues encountered</param>
    /// <returns>A status enum that summarizes the result for all audits on the single stream</returns>
    public static Status Run(Stream idsSource, SingleAuditOptions options, ILogger? logger = null)
    {
        var auditSettings = new AuditHelper(logger, options);
        return AuditStreamAsync(idsSource, auditSettings, logger).Result; // in  run(stream)
    }

    /// <summary>
    /// Entry point to access the library features in batch mode either on directories or single files
    /// </summary>
    /// <param name="batchOptions">configuration options for the execution of audits on a file or folder</param>
    /// <param name="logger">the optional logger provides fine-grained feedback on all the audits performed</param>
    /// <returns>A status enum that summarizes the result for all audits executed</returns>
    public static Status Run(IBatchAuditOptions batchOptions, ILogger? logger = null)
    {
        Status retvalue = Status.Ok;
        if (string.IsNullOrEmpty(batchOptions.InputSource) && !batchOptions.SchemaFiles.Any())
        {
            // no IDS and no schema => nothing to do
            logger?.LogWarning("No audits are required, with the options passed.");
            retvalue |= Status.InvalidOptionsError;
        }
        else if (string.IsNullOrEmpty(batchOptions.InputSource))
        {
            // No ids, but we have a schemafile => check the schema itself
            batchOptions.AuditSchemaDefinition = true;
        }
        if (!string.IsNullOrWhiteSpace(batchOptions.OmitIdsContentAuditPattern))
        {
            try
            {
                // we are trying to see if the 
                var r = new Regex(batchOptions.OmitIdsContentAuditPattern);
            }
            catch (ArgumentException)
            {
                logger?.LogWarning("Invalid OmitIdsContentAuditPattern `{pattern}`.", batchOptions.OmitIdsContentAuditPattern);
                retvalue |= Status.InvalidOptionsError;
            }
        }
        if (retvalue.HasFlag(Status.InvalidOptionsError))
        {
            logger?.LogError("No audit performed.", batchOptions.OmitIdsContentAuditPattern);
            return retvalue;
        }

        var auditsList = new List<string>();
        if (!string.IsNullOrEmpty(batchOptions.InputSource))
            auditsList.Add("Ids structure");
        if (batchOptions.AuditSchemaDefinition)
            auditsList.Add("Xsd schemas correctness");
        if (!batchOptions.OmitIdsContentAudit)
        {
            if (!string.IsNullOrWhiteSpace(batchOptions.OmitIdsContentAuditPattern))
                auditsList.Add("Ids content (omitted on regex match)");
            else
                auditsList.Add("Ids content");
        }
        if (!auditsList.Any())
        {
            logger?.LogError("Invalid options.");
            return Status.InvalidOptionsError;
        }
        // inform on the config
        logger?.LogInformation("Auditing: {audits}.", string.Join(", ", auditsList.ToArray()));

        // start audit
        if (batchOptions.AuditSchemaDefinition)
        {
            retvalue |= PerformSchemaCheck(batchOptions, logger);
            if (retvalue != Status.Ok)
                return retvalue;
        }

        if (Directory.Exists(batchOptions.InputSource))
        {
            var t = new DirectoryInfo(batchOptions.InputSource);
            var ret = ProcessFolder(t, batchOptions, logger);
            return CompleteWith(ret, logger);
        }
        else if (File.Exists(batchOptions.InputSource))
        {
            var t = new FileInfo(batchOptions.InputSource);
            var ret = ProcessSingleFile(t, batchOptions, logger);
            return CompleteWith(ret, logger);
        }
        logger?.LogError("Invalid input source '{missingSource}'", batchOptions.InputSource);
        return Status.NotFoundError;
    }

    private static Status CompleteWith(Status ret, ILogger? writer)
    {
        writer?.LogInformation("Completed with status: {status}.", ret);
        return ret;
    }

    private async static Task<Status> AuditIdsComplianceAsync(IBatchAuditOptions options, FileInfo theFile, ILogger? logger)
    {
        var opts = new AuditProcessOptions()
        {
            SchemaProvider =
                (options.SchemaFiles.Any())
                ? new FileBasedSchemaProvider(options.SchemaFiles) // we load the schemas from the configuration options
                : new AutomaticSchemaProvider(), // we determine the schema version from the file,
            OmitIdsContentAudit =
                options.OmitIdsContentAudit ||
                (!string.IsNullOrWhiteSpace(options.OmitIdsContentAuditPattern) && Regex.IsMatch(theFile.FullName, options.OmitIdsContentAuditPattern, RegexOptions.IgnoreCase))
        };
        var auditSettings = new AuditHelper(logger, opts);
        using var stream = File.OpenRead(theFile.FullName);
        return await AuditStreamAsync(stream, auditSettings, logger); // in AuditIdsComplianceAsync
    }

    private static XmlReaderSettings GetXmlSettings(AuditProcessOptions options)
    {
        // todo: we should set the validation type only once the schemas are loaded
        var rSettings = new XmlReaderSettings()
        {
            ValidationType = options.OmitIdsSchemaAudit ? ValidationType.None : ValidationType.Schema,
            Async = true,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };
        rSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
        return rSettings;
    }

    private static async Task<Status> AuditStreamAsync(Stream theStream, AuditHelper auditSettings, ILogger? logger)
    {
        Status contentStatus = Status.Ok;
        // the handler needs to be set before creating the reader,
        // otherwise the validation event is not registered
        var rSettings = GetXmlSettings(auditSettings.Options);
        if (!auditSettings.Options.OmitIdsSchemaAudit)
            rSettings.ValidationEventHandler += new ValidationEventHandler(auditSettings.ValidationReporter);
        XmlReader reader;
        try
        {
            // the creation is inside a try block because there might be problems when 
            // using schemas from the end user
            //
            reader = XmlReader.Create(theStream, rSettings);
        }
        catch (Exception ex)
        {
            logger?.LogCritical("{exceptionType}: {exceptionMessage}", ex.GetType().Name, ex.Message);
            return auditSettings.SchemaStatus | contentStatus | Status.XsdSchemaError;
        }

        var cntRead = 0;
        var elementsStack = new Stack<BaseContext>(); // prepare the stack to evaluate the IDS content
        BaseContext? current = null;
        var needLoadSchema = !auditSettings.Options.OmitIdsSchemaAudit;
        while (await reader.ReadAsync()) // the loop reads the entire file to trigger validation events.
        {
            cntRead++;
            if (needLoadSchema || !auditSettings.Options.OmitIdsContentAudit) // content audit can be omitted, but the while loop is still executed
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        // Debug.WriteLine($"Start Element {reader.LocalName}");
                        BaseContext? parent = null;
#if NETSTANDARD2_0
                        if (elementsStack.Count > 0)
                            parent = elementsStack.Peek();
#else
                            if (elementsStack.TryPeek(out var peeked))
                                parent = peeked;
#endif
                        if (needLoadSchema && reader.LocalName == "ids")
                        {
                            var loc = reader.GetAttribute("schemaLocation", "http://www.w3.org/2001/XMLSchema-instance") ?? string.Empty;
                            var vrs = IdsFacts.GetVersionFromLocation(loc);
                            var ret = auditSettings.Options.SchemaProvider.GetSchemas(vrs, logger, out var schemas);
                            if (ret != Status.Ok)
                                return auditSettings.SchemaStatus | contentStatus | ret;
                            foreach (var schema in schemas)
                            {
                                rSettings.Schemas.Add(schema);
                            }
                            try
                            {
                                rSettings.Schemas.Compile();
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError("Schema compilation error: {message}", ex.Message);
                                return auditSettings.SchemaStatus | contentStatus | Status.XsdSchemaError;
                            }
                            var names = rSettings.Schemas.GlobalElements.Names.OfType<XmlQualifiedName>().Select(x => x.Name);
                            if (!names.Contains("http://standards.buildingsmart.org/IDS:ids"))
                            {

                            }
                            needLoadSchema = false; // prevent further loading
                        }
                        var newContext = IdsXmlHelpers.GetContextFromElement(reader, parent, logger); // this is always not null

                        // we only push on the stack if it's not empty, e.g.: <some /> does not go on the stack
                        if (!reader.IsEmptyElement)
                            elementsStack.Push(newContext);
                        else
                            contentStatus |= newContext.PerformAudit(logger); // invoking audit empty element
                        current = newContext;
                        break;

                    case XmlNodeType.Text:
                        // Debug.WriteLine($"  Text Node: {reader.GetValueAsync().Result}");
                        current!.SetContent(reader.GetValueAsync().Result);
                        break;
                    case XmlNodeType.EndElement:
                        // Debug.WriteLine($"End Element {reader.LocalName}");
                        var closing = elementsStack.Pop();
                        // Debug.WriteLine($"  auditing {closing.type} on end element");
                        contentStatus |= closing.PerformAudit(logger); // invoking audit on end of element
                        break;
                    default:
                        // Debug.WriteLine("Other node {0} with value '{1}'.", reader.NodeType, reader.Value);
                        break;
                }
            }
        }

        reader.Dispose();
        if (!auditSettings.Options.OmitIdsSchemaAudit)
            rSettings.ValidationEventHandler -= new ValidationEventHandler(auditSettings.ValidationReporter);

        auditSettings.Logger?.LogDebug("Completed reading {cntRead} xml elements.", cntRead);
        return auditSettings.SchemaStatus | contentStatus;
    }

    private static Status ProcessSingleFile(FileInfo theFile, IBatchAuditOptions batchOptions, ILogger? logger)
    {
        Status ret = Status.Ok;
        logger?.LogInformation("Auditing file: `{filename}`.", theFile.FullName);
        ret |= AuditIdsComplianceAsync(batchOptions, theFile, logger).Result;
        return ret;
    }

    private static Status ProcessFolder(DirectoryInfo directoryInfo, IBatchAuditOptions options, ILogger? logger)
    {
#if NETSTANDARD2_0
        var allIdss = directoryInfo.GetFiles($"*.{options.InputExtension}", SearchOption.AllDirectories).ToList();
#else
        var eop = new EnumerationOptions() { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
        var allIdss = directoryInfo.GetFiles($"*.{options.InputExtension}", eop).ToList();
#endif
        Status ret = Status.Ok;
        var tally = 0;
        foreach (var ids in allIdss.OrderBy(x => x.FullName))
        {
            var sgl = ProcessSingleFile(ids, options, logger);
            ret |= sgl;
            tally++;
        }
        var fileCardinality = tally != 1 ? "files" : "file";
        logger?.LogInformation("{tally} {fileCardinality} processed.", tally, fileCardinality);
        return ret;
    }

    /// todo: remove, possibly relocate to <see cref="FileBasedSchemaProvider"/>.
    private static Status PerformSchemaCheck(IBatchAuditOptions auditOptions, ILogger? logger)
    {
        Status ret = Status.Ok;
        var rSettings = new XmlReaderSettings();
        foreach (var schemaFile in auditOptions.SchemaFiles) // within PerformSchemaCheck
        {
            try
            {
                using var reader = File.OpenText(schemaFile);
                var schema = XmlSchema.Read(reader, null);
                if (schema is null)
                {
                    logger?.LogError("XSD\t{schemaFile}\tSchema error.", schemaFile);
                    ret |= Status.XsdSchemaError;
                    continue;
                }
                rSettings.Schemas.Add(schema);
            }
            catch (XmlSchemaException ex)
            {
                logger?.LogError("XSD\t{schemaFile}\tSchema error: {errMessage} at line {line}, position {pos}.", schemaFile, ex.Message, ex.LineNumber, ex.LinePosition);
                ret |= Status.XsdSchemaError;
            }
            catch (Exception ex)
            {
                logger?.LogError("XSD\t{schemaFile}\tSchema error: {errMessage}.", schemaFile, ex.Message);
                ret |= Status.XsdSchemaError;
            }
        }
        return ret;
    }
}
