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
        
        [Test]
        public void ProcessXml_FindReplace_ReplacesText()
        {
            var preAlterationXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><SomeNode>I will be replaced</SomeNode></RootNode>";
            var expectedXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><SomeNode>I am replaced</SomeNode></RootNode>";
            var inputXml = XmlDoc.LoadFromString(preAlterationXmlString);
            var expectedXml = XmlDoc.LoadFromString(expectedXmlString);

            var removeXPath = new FindAndReplaceXml("I will be replaced", "I am replaced");
            inputXml = removeXPath.Alter(inputXml);

            inputXml.Should().BeEquivalentTo(expectedXml);
        }
       
        [Test]
        public void ProcessXml_ReorderNodes_Reorders()
        {
            var preAlterationXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><Order>B</Order><Order>C</Order><Order>A</Order></RootNode>";
            var expectedXmlString = $@"<?xml version='1.0' encoding='utf-8' ?><RootNode><Order>A</Order><Order>B</Order><Order>C</Order></RootNode>";
            var inputXml = XmlDoc.LoadFromString(preAlterationXmlString);
            var expectedXml = XmlDoc.LoadFromString(expectedXmlString);

            var removeXPath = new ReorderNodesAlphabetically("//RootNode", "Order");
            inputXml = removeXPath.Alter(inputXml);

            inputXml.Should().BeEquivalentTo(expectedXml);
        }
    }

    public class ReorderNodesAlphabetically : IAlterXml
    {
        private readonly string _baseXPath;
        private readonly string _childNodesXPath;

        public ReorderNodesAlphabetically(string baseXPath, string childNodesXPath)
        {
            _baseXPath = baseXPath;
            _childNodesXPath = childNodesXPath;
        }

        public XmlDocument Alter(XmlDocument xml)
        {
            var baseNodes = xml.SelectNodes(_baseXPath);

            foreach (XmlNode baseNode in baseNodes)
            {
                var childNodes = baseNode.SelectNodes(_childNodesXPath);

                var allNodes = new List<XmlNode>();

                foreach (XmlNode node in childNodes)
                {
                    allNodes.Add(node);
                }

                var sortedList = allNodes.OrderBy(s => s.InnerText);

                foreach (XmlNode node in childNodes)
                {
                    baseNode.RemoveChild(node);
                }

                foreach (XmlNode node in sortedList)
                {
                    baseNode.AppendChild(node);
                }
            }

            return xml;
        }

        public string GetDescriptionForReport()
        {
            return $"Reorder {_baseXPath} && {_childNodesXPath} selection alphabetically";
        }
    }

    public class XmlGetter : IXmlGetter
    {
        private readonly IGetXmls getControl;
        private readonly IGetXmls getTarget;

        public XmlGetter(IGetXmls getControl, IGetXmls getTarget)
        {
            this.getControl = getControl;
            this.getTarget = getTarget;
        }

        public XmlDocument GetControl(string id)
        {
            return this.getControl.GetXml(id);
        }

        public XmlDocument GetTarget(string id)
        {
            return this.getTarget.GetXml(id);
        }
    }

    public class XmlAlterater : IXmlComparer
    {
        private readonly IXmlGetter xmlGetter;
        private List<IAlterXml> joinXmlAlterations;
        private List<IAlterXml> controlOnlyAlterations;
        private List<IAlterXml> targetOnlyAlterations;

        private List<XmlDocument> controlXmlDocuments;
        private List<XmlDocument> targetXmlDocuments;

        public XmlAlterater(IXmlGetter xmlGetter)
        {
            this.xmlGetter = xmlGetter;
            this.joinXmlAlterations = new List<IAlterXml>();
            this.controlOnlyAlterations = new List<IAlterXml>();
            this.targetOnlyAlterations = new List<IAlterXml>();
            this.controlXmlDocuments = new List<XmlDocument>();
            this.targetXmlDocuments = new List<XmlDocument>();
        }

        public XmlAlterater AddJointAlteration(IAlterXml alterXml)
        {
            this.joinXmlAlterations.Add(alterXml);
            return this;
        }

        public XmlAlterater AddControlAlteration(IAlterXml alterXml)
        {
            this.controlOnlyAlterations.Add(alterXml);
            return this;
        }

        public XmlAlterater AddTargetAlteration(IAlterXml alterXml)
        {
            this.targetOnlyAlterations.Add(alterXml);
            return this;
        }

        public void ProcessAlterations(string id)
        {
            var controlXmlDocument = this.xmlGetter.GetControl(id);
            var targetXmlDocument = this.xmlGetter.GetTarget(id);

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
