using System.Xml;

namespace XmlComparer.Tests
{
    public interface IXmlGetter
    {
        XmlDocument GetControl(string id);
        XmlDocument GetTarget(string id);
    }
}