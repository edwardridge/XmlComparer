using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Org.XmlUnit.Builder;
using Org.XmlUnit.Diff;

namespace XmlComparer
{
    public class RavenMessageXmlCreator : IMessageXmlCreator
    {
        private readonly XPathDifferences xPaths;
        private readonly IAlterXml[] xmlAlterations;

        public RavenMessageXmlCreator(XPathDifferences xPaths, params IAlterXml[] xmlAlterations)
        {
            this.xPaths = xPaths;
            this.xmlAlterations = xmlAlterations;
        }

        public XmlAndFilename CreateXml(string dealCode)
        {
            return new XmlAndFilename();
        }

        private string CreateAndModifyXmlFile(string dealMessage, string fileName)
        {
            File.Delete(fileName);

            var xmlMessage = new XmlDocument();
            xmlMessage.LoadXml(dealMessage);

            foreach (var alteration in this.xmlAlterations)
            {
                xmlMessage = alteration.Alter(xmlMessage);
            }

            foreach (var xpathDiff in this.xPaths.Differences)
            {
                var xpath = xpathDiff.XPath;
                var nodes = xmlMessage.SelectNodes(xpath);

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
            }

            xmlMessage.Save(fileName);

            return xmlMessage.OuterXml;
        }

        private static void DeleteAndRecreateFolder(string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
            }

            Directory.CreateDirectory(folderPath);
        }
    }

    public class ReportComparer : IComparer, IWriteComparisonToFile
    {
        private List<DealDifferences> results;
        private StreamWriter csvWriter;
        private DealDifferenceSummaries dealDifferenceSummaries;

        public ReportComparer(List<DifferenceAndCount> knownDifferences)
        {
            this.results = new List<DealDifferences>();
            this.dealDifferenceSummaries = new DealDifferenceSummaries(knownDifferences);
        }

        public string Shortcut => "r";

        public void Open(XmlAndFilename rh1FilePath, XmlAndFilename rh2FilePath, string dealCode)
        {
            var dealDifferences = new DealDifferences()
            {
                DealCode = dealCode
            };

            this.results.Add(dealDifferences);

            if (rh1FilePath == null)
            {
                var difference = "Cannot do comparison - no RH1 file";
                dealDifferences.DifferenceAndXPaths.Add(new DifferenceAndXPath()
                {
                    Difference = difference
                });

                dealDifferenceSummaries.AddError(difference);

                return;
            }

            if (rh2FilePath == null)
            {
                var difference = "Cannot do comparison - no RH2 file";
                dealDifferences.DidNotPubllishInRH2 = true;
                dealDifferences.DifferenceAndXPaths.Add(new DifferenceAndXPath()
                {
                    Difference = difference
                });

                dealDifferenceSummaries.AddError(difference);

                return;
            }

            var rh1Input = Input.FromFile(rh1FilePath.Filename).Build();
            var rh2Input = Input.FromFile(rh2FilePath.Filename).Build();

            DiffBuilder
                .Compare(rh1Input)
                .WithTest(rh2Input)
                .WithNodeMatcher(new DefaultNodeMatcher(ElementSelectors.ByNameAndAllAttributes))
                .WithDifferenceEvaluator(IgnoreCaseDifferenceEvaluator)
                .WithDifferenceListeners(DiffListener(dealDifferences.DifferenceAndXPaths, dealDifferenceSummaries))
                .CheckForSimilar()
                .Build();
        }

        private static ComparisonListener DiffListener(List<DifferenceAndXPath> results, DealDifferenceSummaries dealDifferenceSummaries)
        {
            return (comparison, outcome) =>
            {
                string difference;
                if (comparison.Type == ComparisonType.TEXT_VALUE || comparison.Type == ComparisonType.CHILD_LOOKUP)
                {
                    var rh1XML = comparison.ControlDetails?.Target?.OuterXml;
                    var rh2XML = comparison.TestDetails?.Target?.OuterXml;

                    difference = $"RH1 {rh1XML} || RH2: {rh2XML}";
                }
                else if (comparison.Type == ComparisonType.CHILD_NODELIST_LENGTH || comparison.Type == ComparisonType.CHILD_NODELIST_SEQUENCE)
                {
                    difference = string.Empty;
                }
                else
                {
                    difference = comparison.ToString();
                }

                difference = difference.Replace(",", " comma ");

                if (!string.IsNullOrEmpty(difference))
                {
                    dealDifferenceSummaries.AddError(difference);

                    results.Add(new DifferenceAndXPath()
                    {
                        Difference = difference,
                        ComparisonXPath = comparison.ControlDetails?.XPath,
                        TargetXPath = comparison.TestDetails?.XPath
                    });
                }
            };
        }

        private ComparisonResult IgnoreCaseDifferenceEvaluator(Org.XmlUnit.Diff.Comparison comparison, ComparisonResult outcome)
        {
            if (outcome == ComparisonResult.EQUAL) return outcome;
            var controlNode = comparison.ControlDetails.Target?.OuterXml;
            var targetNode = comparison.TestDetails.Target?.OuterXml;

            if (string.Equals(controlNode, targetNode, StringComparison.InvariantCultureIgnoreCase))
            {
                return ComparisonResult.EQUAL;
            }

            return DifferenceEvaluators.Default(comparison, outcome);
        }

        public void Write(XPathDifferences xpathsToIgnore, IEnumerable<IAlterXml> xmlAlterations, IEnumerable<string> contractTypesToCheck, string messageType)
        {
            this.csvWriter = new StreamWriter($"differences_{messageType}.csv");
            csvWriter.WriteLine($"Deals: {results.Count}");

            if (contractTypesToCheck.Any())
            {
                csvWriter.WriteLine("Contract types");
                foreach (var contractType in contractTypesToCheck)
                {
                    csvWriter.WriteLine(contractType);
                }
            }
            else
            {
                csvWriter.WriteLine("No contract types selected");
            }
            csvWriter.WriteLine("---------------");
            csvWriter.WriteLine("Ignore XPaths");
            foreach (var xpath in xpathsToIgnore.Differences)
            {
                csvWriter.WriteLine($"{xpath.XPath}, {xpath.Reason}");
            }

            csvWriter.WriteLine("---------------");
            csvWriter.WriteLine("XML Alterations");
            foreach (var alteration in xmlAlterations)
            {
                csvWriter.WriteLine(alteration.GetDescriptionForReport());
            }

            csvWriter.WriteLine();
            csvWriter.WriteLine();
            csvWriter.WriteLine();
            csvWriter.WriteLine($"Unnacounted Differences, {dealDifferenceSummaries.UnaccountedForErrorAndCounts.Sum(s => s.Count)}");
            csvWriter.WriteLine("Difference, Count");
            foreach (var unaccountedDifferenceAndCount in dealDifferenceSummaries.UnaccountedForErrorAndCounts.OrderByDescending(s => s.Count))
            {
                csvWriter.WriteLine($"{unaccountedDifferenceAndCount.Difference}, {unaccountedDifferenceAndCount.Count}");
            }

            csvWriter.WriteLine();
            csvWriter.WriteLine();
            csvWriter.WriteLine();

            csvWriter.WriteLine($"Acounted Differences, {dealDifferenceSummaries.UnaccountedForErrorAndCounts.Sum(s => s.Count)}");
            csvWriter.WriteLine("Difference, Reason, Count");
            foreach (var accountedDifferenceAndCount in dealDifferenceSummaries.AccountedForErrorAndCounts.OrderByDescending(s => s.Count))
            {
                csvWriter.WriteLine($"{accountedDifferenceAndCount.Difference}, {accountedDifferenceAndCount.Reason}, {accountedDifferenceAndCount.Count}");
            }

            csvWriter.WriteLine();
            csvWriter.WriteLine();
            csvWriter.WriteLine();

            csvWriter.WriteLine();
            csvWriter.WriteLine("Difference, RH1, RH2");

            foreach (var result in this.results)
            {
                var dealCodeWithoutPrefixes = result.DealCode.Replace("_DC", "").Replace("_PR", "");
                csvWriter.WriteLine($"Deal code: {dealCodeWithoutPrefixes}");

                foreach (var difference in result.DifferenceAndXPaths)
                {
                    csvWriter.WriteLine($"{difference.Difference}, {difference.ComparisonXPath}, {difference.TargetXPath}");
                }

                csvWriter.WriteLine();
            }

            csvWriter.WriteLine();
            csvWriter.WriteLine();

            var allAccountedDifferences = dealDifferenceSummaries.AccountedForErrorAndCounts.Select(s => s.Difference).ToList();

            var allXpaths = this.results
                .SelectMany(s => s.DifferenceAndXPaths)
                .Where(w => allAccountedDifferences.Contains(w.Difference) == false)
                .Select(s => s.ComparisonXPath.IsNullOrEmpty() ? s.TargetXPath : s.ComparisonXPath)
                .Where(w => w.IsNullOrEmpty() == false);

            var countOfPaths = CountPaths(allXpaths).OrderBy(s => s.Count);
            csvWriter.WriteLine("Count Of Differences By Path");
            csvWriter.WriteLine("Path, Count");
            foreach (var pathAndCount in countOfPaths)
            {
                csvWriter.WriteLine($"{pathAndCount.Path}, {pathAndCount.Count}");
            }

            csvWriter.WriteLine();
            csvWriter.WriteLine();
            csvWriter.WriteLine("Deals that did not publish in RH2");
            foreach (var dealDidntPublishInRh2 in this.results.Where(s => s.DidNotPubllishInRH2))
            {
                csvWriter.Write(dealDidntPublishInRh2.DealCode);
                csvWriter.Write(" ");
            }
        }

        public void Dispose()
        {
            this.csvWriter.Dispose();
        }

        private static void AddToCountForPath(string path, List<PathAndCount> pathAndCounts)
        {
            var pathAndCount = pathAndCounts.SingleOrDefault(f => f.Path == path);

            if (pathAndCount == null)
            {
                pathAndCounts.Add(new PathAndCount()
                {
                    Path = path,
                    Count = 1
                });
            }
            else
            {
                pathAndCount.Count = pathAndCount.Count + 1;
            }
        }

        private static List<PathAndCount> CountPaths(IEnumerable<string> xmlPaths)
        {
            var pathAndCounts = new List<PathAndCount>();

            foreach (var xmlPath in xmlPaths)
            {
                var xmlPathToCheck = RemoveSquareBrackets(xmlPath);

                while (xmlPathToCheck != "")
                {
                    AddToCountForPath(xmlPathToCheck, pathAndCounts);

                    xmlPathToCheck = RemoveRightHandSlash(xmlPathToCheck);
                }
            }

            return pathAndCounts;
        }

        private static string RemoveSquareBrackets(string path)
        {
            var pathWithBracketsRemoved = path.Replace("[", "").Replace("]", "");
            var pathWithNumbersRemoved = Regex.Replace(pathWithBracketsRemoved, @"[\d-]", string.Empty);

            return pathWithNumbersRemoved;
            ;
        }

        private static string RemoveRightHandSlash(string path)
        {
            var indexOfLastSlash = path.LastIndexOf("/", StringComparison.Ordinal);

            var leftOfLastSlash = path.Substring(0, indexOfLastSlash);
            return leftOfLastSlash;
        }

        public class PathAndCount
        {
            public string Path { get; set; }

            public int Count { get; set; }
        }
    }

    public class XmlAndFilename
    {
        public string Filename { get; set; }

        public string Xml { get; set; }
    }

    public interface IMessageXmlCreator
    {
        XmlAndFilename CreateXml(string dealCode);
    }

    public interface IComparer
    {
        string Shortcut { get; }

        void Open(XmlAndFilename rh1FilePath, XmlAndFilename rh2FilePath, string dealCode);
    }

    public interface IAlterXml
    {
        XmlDocument Alter(XmlDocument xml);

        string GetDescriptionForReport();
    }

    public interface IWriteComparisonToFile : IDisposable
    {
        void Write(XPathDifferences xpathsToIgnore, IEnumerable<IAlterXml> xmlAlterations, IEnumerable<string> contractTypesToCheck, string messageType);
    }

    public class XPathDifferences
    {
        public XPathDifferences()
        {
            this.Differences = new List<XPathDifference>();
        }

        public List<XPathDifference> Differences { get; set; }
    }

    public class FindAndReplaceXml : IAlterXml
    {
        private readonly string AdditionalDescription;

        public FindAndReplaceXml(string from, string to, string additionalDescription = null)
        {
            this.AdditionalDescription = additionalDescription;
            this.From = from;
            this.To = to;
        }

        public string From { get; private set; }

        public string To { get; private set; }

        public XmlDocument Alter(XmlDocument xml)
        {
            var alteredXml = xml.OuterXml.Replace(this.From, this.To);

            var result = new XmlDocument();
            result.LoadXml(alteredXml);
            return result;
        }

        public string GetDescriptionForReport()
        {
            var addendumText = AdditionalDescription.IsNullOrEmpty() ? "" : $" - {AdditionalDescription}";
            return $"We replaced '{this.From}' with '{this.To}' in the whole xml {addendumText}";
        }
    }

    public class Rh1ToRh2DealMessageComparison
    {
        private readonly IComparer comparisonStrategy;
        private readonly IMessageXmlCreator rh1MessageCreator;
        private readonly IMessageXmlCreator rh2MessageCreator;

        public Rh1ToRh2DealMessageComparison(IComparer comparisonStrategy, IMessageXmlCreator rh1MessageCreator, IMessageXmlCreator rh2MessageCreator)
        {
            this.comparisonStrategy = comparisonStrategy;
            this.rh1MessageCreator = rh1MessageCreator;
            this.rh2MessageCreator = rh2MessageCreator;
        }

        public void Compare(IEnumerable<string> dealCodes, XPathDifferences xpathsToIgnore, IAlterXml[] xmlAlterations, IEnumerable<string> contractTypesToCheck, string messageType)
        {
            try
            {
                foreach (var dealCode in dealCodes)
                {
                    var rh1File = this.rh1MessageCreator.CreateXml(dealCode);
                    var rh2File = this.rh2MessageCreator.CreateXml(dealCode);
                    this.comparisonStrategy.Open(rh1File, rh2File, dealCode);
                }

                if (comparisonStrategy is IWriteComparisonToFile writeComparisonToFile)
                {
                    writeComparisonToFile.Write(xpathsToIgnore, xmlAlterations, contractTypesToCheck, messageType);
                    writeComparisonToFile.Dispose();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.ReadLine();
            }
        }
    }

    public class XPathDifference
    {
        public XPathDifference(string xPath, string reason)
        {
            XPath = xPath;
            Reason = reason;
        }

        public string XPath { get; set; }

        public string Reason { get; set; }
    }

    public class DifferenceAndCount
    {
        public string Difference { get; set; }

        public string Reason { get; set; }

        public int Count { get; set; }
    }

    public class DealDifferenceSummaries
    {


        public DealDifferenceSummaries()
        {
            this.UnaccountedForErrorAndCounts = new List<DifferenceAndCount>();
            this.AccountedForErrorAndCounts = new List<DifferenceAndCount>();
        }

        public DealDifferenceSummaries(List<DifferenceAndCount> knownDifferences)
        {
            this.UnaccountedForErrorAndCounts = new List<DifferenceAndCount>();
            this.AccountedForErrorAndCounts = knownDifferences;
        }

        public List<DifferenceAndCount> AccountedForErrorAndCounts { get; set; }

        public List<DifferenceAndCount> UnaccountedForErrorAndCounts { get; set; }

        public void AddError(string difference)
        {
            if (string.IsNullOrEmpty(difference))
            {
                return;
            }

            //Todo: Add check to see if it's in known difference list
            var differenceInAccountedForDifferences = this.AccountedForErrorAndCounts.FirstOrDefault(s => s.Difference == difference);
            if (differenceInAccountedForDifferences != null)
            {
                differenceInAccountedForDifferences.Count += 1;
            }
            else
            {
                var existingDifference = this.UnaccountedForErrorAndCounts.FirstOrDefault(s => s.Difference == difference);
                if (existingDifference == null)
                {
                    this.UnaccountedForErrorAndCounts.Add(new DifferenceAndCount()
                    {
                        Count = 1,
                        Difference = difference
                    });

                    return;
                }

                existingDifference.Count += 1;
            }
        }
    }

    public class DealDifferences
    {
        public DealDifferences()
        {
            this.DifferenceAndXPaths = new List<DifferenceAndXPath>();
        }

        public List<DifferenceAndXPath> DifferenceAndXPaths { get; set; }

        public string DealCode { get; set; }

        public bool DidNotPubllishInRH2 { get; set; }
    }

    public class DifferenceAndXPath
    {
        public string Difference { get; set; }

        public string ComparisonXPath { get; set; }

        public string TargetXPath { get; set; }
    }

    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string stringVal)
        {
            return string.IsNullOrEmpty(stringVal);
        }

        public static bool IsNullOrWhiteSpace(this string stringVal)
        {
            return string.IsNullOrWhiteSpace(stringVal);
        }
    }
}
