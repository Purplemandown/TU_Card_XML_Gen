// See https://aka.ms/new-console-template for more information

using System.Xml.Linq;
using System.Xml.XPath;
using TU_Card_XML_Gen;
using TUComparatorLibrary;

XDocument config = XDocument.Parse(File.ReadAllText("config.xml"));

new Updater().Run(config.XPathSelectElement("//PathToCardXMLs").Value, config.XPathSelectElement("//PathToUpdateFile").Value, new ConfigStore(config.Root));

Console.WriteLine("End Results.  Press any key to end.");
Console.ReadKey();