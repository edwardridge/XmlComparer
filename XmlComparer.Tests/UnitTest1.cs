using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using FluentAssertions;
using NUnit.Framework;

namespace XmlComparer.Tests
{
    public class UnitTest1
    {
        [Test]
        public void GetXmlsFromFolder_ReturnsCorrectNumberOfXmls()
        {
            var pathToFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestXmls");
            var getXmlsFromFolder = new GetXmlsFromFolder(pathToFolder);

            getXmlsFromFolder.GetXml("TestFile1").Should().NotBeNull();
        }

        [Test]
        public void ProcessXml_RemoveXpath_RemovesXpath()
        {
            var preAlterationXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><IShouldBeRemoved></IShouldBeRemoved></RootNode>";
            var expectedXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode></RootNode>";
            var inputXml = XmlDoc.LoadFromString(preAlterationXmlString);
            var expectedXml = XmlDoc.LoadFromString(expectedXmlString);

            var removeXPath = new RemoveXPathFromXml("//IShouldBeRemoved");
            removeXPath.Alter(inputXml);

            inputXml.Should().BeEquivalentTo(expectedXml);
        }

        [Test]
        public void ProcessXml_RemoveXpath_RemovesXpathBasedOnAttribute()
        {
            var preAlterationXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><SomeNode></SomeNode><SomeNode Name='RemoveMe'></SomeNode></RootNode>";
            var expectedXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><SomeNode></SomeNode></RootNode>";
            var inputXml = XmlDoc.LoadFromString(preAlterationXmlString);
            var expectedXml = XmlDoc.LoadFromString(expectedXmlString);

            var removeXPath = new RemoveXPathFromXml("//SomeNode[@Name='RemoveMe']");
            removeXPath.Alter(inputXml);

            inputXml.Should().BeEquivalentTo(expectedXml);
        }

        [Test]
        public void ProcessXml_RemoveXpath_RemovesXpathBasedOnText()
        {
            var preAlterationXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><SomeNode>Remove Me</SomeNode></RootNode>";
            var expectedXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode></RootNode>";
            var inputXml = XmlDoc.LoadFromString(preAlterationXmlString);
            var expectedXml = XmlDoc.LoadFromString(expectedXmlString);

            var removeXPath = new RemoveXPathFromXml("//SomeNode[text()='Remove Me']");
            removeXPath.Alter(inputXml);

            inputXml.Should().BeEquivalentTo(expectedXml);
        }

        public class IntegrationTests
        {
            [Test]
            public void XmlComparerCanLoadControlXmlDocument()
            {
                var pathToFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestXmls");
                var getXmlsFromFolder = new GetXmlsFromFolder(pathToFolder);

                var xmlComparer = new XmlCompare(getXmlsFromFolder, getXmlsFromFolder);

                xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

                var expectedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcess.xml");

                var actualXmlDocument = xmlComparer.GetControlXmlDocuments().First();
                actualXmlDocument.Should().BeEquivalentTo(expectedXmlDocument);
            }

            [Test]
            public void XmlComparerCanLoadTargetXmlDocument()
            {
                var pathToFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestXmls");
                var getXmlsFromFolder = new GetXmlsFromFolder(pathToFolder);

                var xmlComparer = new XmlCompare(getXmlsFromFolder, getXmlsFromFolder);

                xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

                var expectedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcess.xml");

                var actualXmlDocument = xmlComparer.GetTargetXmlDocuments().First();
                actualXmlDocument.Should().BeEquivalentTo(expectedXmlDocument);
            }

            [Test]
            public void XmlComparerCanRunAlterationsOnBothXmlDocuments()
            {
                var pathToFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestXmls");
                var getXmlsFromFolder = new GetXmlsFromFolder(pathToFolder);

                var xmlComparer = new XmlCompare(getXmlsFromFolder, getXmlsFromFolder);
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
                var pathToFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestXmls");
                var getXmlsFromFolder = new GetXmlsFromFolder(pathToFolder);

                var xmlComparer = new XmlCompare(getXmlsFromFolder, getXmlsFromFolder);
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
                var pathToFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestXmls");
                var getXmlsFromFolder = new GetXmlsFromFolder(pathToFolder);

                var xmlComparer = new XmlCompare(getXmlsFromFolder, getXmlsFromFolder);
                xmlComparer.AddTargetAlteration(new RemoveXPathFromXml("//IShouldBeRemoved"));
                xmlComparer.ProcessAlterations("TestFileForLoadAndProcess");

                var changedXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcessExpected.xml");
                var originalXmlDocument = GetTestXmlDocument("TestXmls/TestFileForLoadAndProcess.xml");

                var controlXmlDocument = xmlComparer.GetControlXmlDocuments().First();
                controlXmlDocument.Should().BeEquivalentTo(originalXmlDocument);

                var targetXmlDocument = xmlComparer.GetTargetXmlDocuments().First();
                targetXmlDocument.Should().BeEquivalentTo(changedXmlDocument);
            }
        }

        public static XmlDocument GetTestXmlDocument(string filePath)
        {
            var xmlDocument = new XmlDocument();
            xmlDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, filePath));
            return xmlDocument;
        }
    }

    public class XmlCompare : IXmlComparer
    {
        private readonly IGetXmls getControlXmls;
        private readonly IGetXmls getTargetXmls;
        private List<IAlterXml> joinXmlAlterations;
        private List<IAlterXml> controlOnlyAlterations;
        private List<IAlterXml> targetOnlyAlterations;

        private List<XmlDocument> controlXmlDocuments;
        private List<XmlDocument> targetXmlDocuments;

        public XmlCompare(IGetXmls getControlXmls, IGetXmls getTargetXmls)
        {
            this.getControlXmls = getControlXmls;
            this.getTargetXmls = getTargetXmls;
            this.joinXmlAlterations = new List<IAlterXml>();
            this.controlOnlyAlterations = new List<IAlterXml>();
            this.targetOnlyAlterations = new List<IAlterXml>();
            this.controlXmlDocuments = new List<XmlDocument>();
            this.targetXmlDocuments = new List<XmlDocument>();
        }

        public XmlCompare AddJointAlteration(IAlterXml alterXml)
        {
            this.joinXmlAlterations.Add(alterXml);
            return this;
        }

        public XmlCompare AddControlAlteration(IAlterXml alterXml)
        {
            this.controlOnlyAlterations.Add(alterXml);
            return this;
        }

        public XmlCompare AddTargetAlteration(IAlterXml alterXml)
        {
            this.targetOnlyAlterations.Add(alterXml);
            return this;
        }

        public void ProcessAlterations(string id)
        {
            var controlXmlDocument = this.getControlXmls.GetXml(id);
            var targetXmlDocument = this.getTargetXmls.GetXml(id);

            foreach (var xmlAlteration in joinXmlAlterations.Union(controlOnlyAlterations))
            {
                xmlAlteration.Alter(controlXmlDocument);
            }

            foreach (var xmlAlteration in joinXmlAlterations.Union(targetOnlyAlterations))
            {
                xmlAlteration.Alter(targetXmlDocument);
            }

            this.controlXmlDocuments.Add(controlXmlDocument);
            this.targetXmlDocuments.Add(targetXmlDocument);
        }

        public List<XmlDocument> GetControlXmlDocuments()
        {
            return this.controlXmlDocuments;
        }

        public List<XmlDocument> GetTargetXmlDocuments()
        {
            return this.targetXmlDocuments;
        }
    }

    public interface IXmlComparer
    {
    }

    public static class XmlDoc
    {
        public static XmlDocument LoadFromString(string inputString)
        {
            var xmlDocument = new XmlDocument();

            xmlDocument.LoadXml(inputString);

            return xmlDocument;
        }
    }

    public class RemoveXPathFromXml : IAlterXml
    {
        private string xPathToRemove;

        public RemoveXPathFromXml(string xPath)
        {
            this.xPathToRemove = xPath;
        }

        public XmlDocument Alter(XmlDocument xml)
        {
            var nodes = xml.SelectNodes(this.xPathToRemove);

            foreach (XmlNode node in nodes)
            {
                if (node is XmlAttribute attribute)
                {
                    attribute.OwnerElement.Attributes.Remove(attribute);
                }
                else
                {
                    node?.ParentNode?.RemoveChild(node);
                }
            }

            return xml;
        }

        public string GetDescriptionForReport()
        {
            return "";
        }
    }

    public class GetXmlsFromFolder : IGetXmls
    {
        private string pathToFolder;

        public GetXmlsFromFolder(string pathToFolder)
        {
            this.pathToFolder = pathToFolder;
        }

        public XmlDocument GetXml(string id)
        {
            var file =
                Path.Combine(this.pathToFolder, $"{id}.xml");

            
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.Load(file);
            return xmlDocument;
            
        }
    }

    public interface IGetXmls
    {
        XmlDocument GetXml(string id);
    }
}
