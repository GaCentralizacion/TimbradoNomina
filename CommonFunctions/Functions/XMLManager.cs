using System;
using System.Xml.Schema;
using System.Xml.XPath;
using System.Xml.Xsl;
using System.Xml;
using System.IO;
using System.Xml.Linq;

namespace CommonFunctions.Functions
{
    public class XMLManager
    {
        public string XMLToLayout(string xmlPath, string xsltPath)
        {
            using (StreamReader reader = new StreamReader(xmlPath))
            {
                XPathDocument XPathDocument = new XPathDocument(reader);

                XslCompiledTransform XslTransform = new XslCompiledTransform();
                XslTransform.Load(xsltPath);

                using (StringWriter str = new StringWriter())
                {
                    XmlTextWriter XmlWriter = new XmlTextWriter(str);

                    XslTransform.Transform(XPathDocument, null, XmlWriter);
                    return str.ToString();
                }
            }
        }



        public bool Validate(string xmlFilePath, string xsdFilePath, string xsdNameSpace)
        {
            var xdoc = XDocument.Load(xmlFilePath);
            var schemas = new XmlSchemaSet();
            schemas.Add(xsdNameSpace, xsdFilePath);

            try
            {
                xdoc.Validate(schemas, null);
            }
            catch (XmlSchemaValidationException)
            {
                return false;
            }

            return true;
        }



        public void ReadXml(string xmlPath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlPath);
        }



    }

}
