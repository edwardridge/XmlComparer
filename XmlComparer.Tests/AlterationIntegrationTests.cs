using System.IO;
using System.Linq;
using System.Xml;
using FluentAssertions;
using NUnit.Framework;

namespace XmlComparer.Tests
{
    public class AlterationIntegrationTests
    {
        private GetXmlsFromFolder getXmlsFromFolder;
        private XmlAlterater xmlComparer;

        [SetUp]
        public void Setup()
        {
            var pathToFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestXmls");
            this.getXmlsFromFolder = new GetXmlsFromFolder(pathToFolder);
            this.xmlComparer = new XmlAlterater(new XmlGetter(getXmlsFromFolder, getXmlsFromFolder));
        }

        [Test]
        public void XmlComparerCanLoadControlXmlDocument()
        {
            xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

            var expectedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcess.xml");

            var actualXmlDocument = xmlComparer.GetControlXmlDocuments().First();
            actualXmlDocument.Should().BeEquivalentTo(expectedXmlDocument);
        }

        [Test]
        public void XmlComparerCanLoadTargetXmlDocument()
        {
            xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

            var expectedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcess.xml");

            var actualXmlDocument = xmlComparer.GetTargetXmlDocuments().First();
            actualXmlDocument.Should().BeEquivalentTo(expectedXmlDocument);
        }

        [Test]
        public void XmlComparerCanRunAlterationsOnBothXmlDocuments()
        {
            xmlComparer.AddJointAlteration(new RemoveXPathFromXml("//IShouldBeRemoved"));
            xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

            var expectedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcessExpected.xml");

            var controlXmlDocument = xmlComparer.GetControlXmlDocuments().First();
            controlXmlDocument.Should().BeEquivalentTo(expectedXmlDocument);

            var targetXmlDocument = xmlComparer.GetTargetXmlDocuments().First();
            targetXmlDocument.Should().BeEquivalentTo(expectedXmlDocument);
        }

        [Test]
        public void XmlComparerCanRunAlterationsOnlyOnControlXmlDocuments()
        {
            xmlComparer.AddControlAlteration(new RemoveXPathFromXml("//IShouldBeRemoved"));
            xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

            var changedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcessExpected.xml");
            var originalXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcess.xml");

            var controlXmlDocument = xmlComparer.GetControlXmlDocuments().First();
            controlXmlDocument.Should().BeEquivalentTo(changedXmlDocument);

            var targetXmlDocument = xmlComparer.GetTargetXmlDocuments().First();
            targetXmlDocument.Should().BeEquivalentTo(originalXmlDocument);
        }

        [Test]
        public void XmlComparerCanRunAlterationsOnlyOnTargetXmlDocuments()
        {
            xmlComparer.AddTargetAlteration(new RemoveXPathFromXml("//IShouldBeRemoved"));
            xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

            var changedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcessExpected.xml");
            var originalXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcess.xml");

            var controlXmlDocument = xmlComparer.GetControlXmlDocuments().First();
            controlXmlDocument.Should().BeEquivalentTo(originalXmlDocument);

            var targetXmlDocument = xmlComparer.GetTargetXmlDocuments().First();
            targetXmlDocument.Should().BeEquivalentTo(changedXmlDocument);
        }

        public static XmlDocument GetTestXmlDocument(string filePath)
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, filePath));
            return xmlDocument;
        }

    }
}
