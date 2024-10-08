﻿using FluentAssertions;
using IdsLib.IdsSchema;
using IdsLib.IdsSchema.IdsNodes;
using idsTool.tests.Helpers;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace idsTool.tests;

public class IdsInfoTests : BuildingSmartRepoFiles
{
    [Theory]
    [InlineData("InvalidFiles/empty.ids", false)]
    [InlineData("InvalidFiles/InvalidSchemaLocation.ids", true)]
    [InlineData("InvalidFiles/notAnIdsElement.ids", false)]
    [InlineData("InvalidFiles/notAnXml.ids", false)]
    [InlineData("InvalidFiles/smallcross_gif.ids", false)]
    public async Task InvalidFilesDontBreak(string idsFile, bool isIds)
    {
        var f = new FileInfo(idsFile);
        var t = await IdsXmlHelpers.GetIdsInformationAsync(f);
        t.Should().NotBeNull();
        t.Version.Should().Be(IdsVersion.Invalid);
        t.IsIds.Should().Be(isIds);
    }

    [SkippableTheory]
    [MemberData(nameof(GetIdsRepositoryExampleIdsFiles))]
    public async Task CanReadIdsDevelopmentFiles(string idsFile)
    {
        Skip.If(idsFile == string.Empty, "IDS repository folder not available for extra tests.");
        FileInfo f = LoggerAndAuditHelpers.GetAndCheckIdsRepositoryDevelopmentFileInfo(idsFile);
        var t = await IdsXmlHelpers.GetIdsInformationAsync(f);
        t.Should().NotBeNull();
        t.Version.Should().NotBe(IdsVersion.Invalid);
    }

    [SkippableTheory]
    [MemberData(nameof(GetIdsRepositoryTestCaseIdsFiles))]
    public async Task CanReadIdsTestCases(string idsFile)
    {
        Skip.If(idsFile == string.Empty, "IDS repository folder not available for extra tests.");
        FileInfo f = LoggerAndAuditHelpers.GetAndCheckDocumentationTestCaseFileInfo(idsFile);
        var idsInformation = await IdsXmlHelpers.GetIdsInformationAsync(f);
        idsInformation.Should().NotBeNull();
        var schemaVersion = idsInformation.Version;
        schemaVersion.Should().NotBe(IdsVersion.Invalid, $"{f.FullName}, as a documentation case, should have a valid schema version");
    }
}
