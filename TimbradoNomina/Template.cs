////usings necesarios
using QRCoder;
using System.Collections.Generic;
using System.Xml;
using System;
///////
/*
instalar QRCoder desde consola nuget: 

dentro de VS, menu Herramientas -> Administrador de paquetes NuGet -> Consola del administrador de paquetes

PM> Install-Package QRCoder
*/
///////



namespace TimbradoNomina
{


    public class Template
    {


        private string creaQR(string rutaQR, string nombreQR, string cadenaQR)
        {
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(cadenaQR, QRCodeGenerator.ECCLevel.H);
            QRCode qrCode = new QRCode(qrCodeData);
            System.Drawing.Bitmap qrCodeImage = qrCode.GetGraphic(20);

            qrCodeImage.Save(rutaQR + nombreQR, System.Drawing.Imaging.ImageFormat.Png);

            return rutaQR + nombreQR;
        }

        public string leerXML(string xml, string rutaQR, string nombreQR, string urlQR)
        {
            string pathQR = "";

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xml);

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("x", "http://www.sat.gob.mx/cfd/3");

            XmlElement root = xmlDoc.DocumentElement;

            //encabezado************************************************************
            string serie = "", folio = "", fechaCertificacion = "", fechaEmision = "", selloSAT = "", selloCFD = "", folioFiscal = "", version = "";

            List<string> atributosRoot = new List<string>();

            foreach (XmlAttribute atribute in root.Attributes)
                atributosRoot.Add(atribute.Name);

            XmlNode complemento = xmlDoc.SelectSingleNode("/x:Comprobante/x:Complemento", nsMgr);

            serie = atributosRoot.Contains("Serie") ? root.Attributes["Serie"].Value : "";
            folio = atributosRoot.Contains("Folio") ? root.Attributes["Folio"].Value : "";
            fechaEmision = atributosRoot.Contains("Fecha") ? root.Attributes["Fecha"].Value : "";
            version = atributosRoot.Contains("Version") ? root.Attributes["Version"].Value : "";

            XmlNode timbreFiscal = null;

            foreach (XmlNode nodo in complemento)
                if (nodo.Name == "tfd:TimbreFiscalDigital")
                    timbreFiscal = nodo;

            if (timbreFiscal != null)
            {
                if (timbreFiscal.Attributes["FechaTimbrado"] != null)
                    fechaCertificacion = timbreFiscal.Attributes["FechaTimbrado"].Value;
                if (timbreFiscal.Attributes["SelloCFD"] != null)
                    selloCFD = timbreFiscal.Attributes["SelloCFD"].Value;
                if (timbreFiscal.Attributes["SelloSAT"] != null)
                    selloSAT = timbreFiscal.Attributes["SelloSAT"].Value;
                if (timbreFiscal.Attributes["UUID"] != null)
                    folioFiscal = timbreFiscal.Attributes["UUID"].Value;
            }
            //encabezado************************************************************
            //emisor************************************************************
            string nombreEmisor = "", rfcEmisor = "", calle = "", noExterior = "", noInterior = "", colonia = "", municipio = "", codigoPostal = "", pais = "";
            string estado = "", regimenFiscal = "";

            XmlNode emisor = xmlDoc.SelectSingleNode("/x:Comprobante/x:Emisor", nsMgr);

            if (emisor.Attributes["Nombre"] != null)
                nombreEmisor = emisor.Attributes["Nombre"].Value;

            if (emisor.Attributes["Rfc"] != null)
                rfcEmisor = emisor.Attributes["Rfc"].Value;

            XmlNode domicilioFiscalEmisor = xmlDoc.SelectSingleNode("/x:Comprobante/x:Emisor/x:DomicilioFiscal", nsMgr);

            //LQMA ADD 12052017
            if (domicilioFiscalEmisor != null)
            {
                List<string> atributosDomicilioFiscalEmisor = new List<string>();

                foreach (XmlAttribute atribute in domicilioFiscalEmisor.Attributes)
                    atributosDomicilioFiscalEmisor.Add(atribute.Name);

                calle = atributosDomicilioFiscalEmisor.Contains("calle") ? domicilioFiscalEmisor.Attributes["calle"].Value : "";
                noExterior = atributosDomicilioFiscalEmisor.Contains("noExterior") ? domicilioFiscalEmisor.Attributes["noExterior"].Value : "";

                noInterior = atributosDomicilioFiscalEmisor.Contains("noInterior") ? domicilioFiscalEmisor.Attributes["noInterior"].Value : "";

                colonia = atributosDomicilioFiscalEmisor.Contains("colonia") ? domicilioFiscalEmisor.Attributes["colonia"].Value : "";
                municipio = atributosDomicilioFiscalEmisor.Contains("municipio") ? domicilioFiscalEmisor.Attributes["municipio"].Value : "";
                codigoPostal = atributosDomicilioFiscalEmisor.Contains("codigoPostal") ? domicilioFiscalEmisor.Attributes["codigoPostal"].Value : "";
                pais = atributosDomicilioFiscalEmisor.Contains("pais") ? domicilioFiscalEmisor.Attributes["pais"].Value : "";
                estado = atributosDomicilioFiscalEmisor.Contains("estado") ? domicilioFiscalEmisor.Attributes["estado"].Value : "";

            }
            XmlNode regimenFiscalEmisor = xmlDoc.SelectSingleNode("/x:Comprobante/x:Emisor/@RegimenFiscal", nsMgr);
            regimenFiscal = regimenFiscalEmisor.Value;

            //emisor************************************************************
            //receptor************************************************************
            string nombreReceptor = "", rfcReceptor = "", noEmpleado = "", nssReceptor = "", curpReceptor = "", salarioBase = "", departamento = "", diasTrabajados = "";
            string certificadoDigital = "", serieCertificadoSAT = "", periodoPagoInicial = "", periodoPagoFinal = "", cadenaOriginalCertificadoSAT = "";

            XmlNode receptor = xmlDoc.SelectSingleNode("/x:Comprobante/x:Receptor", nsMgr);

            if (receptor.Attributes["Nombre"] != null)
                nombreReceptor = receptor.Attributes["Nombre"].Value;
            if (receptor.Attributes["Rfc"] != null)
                rfcReceptor = receptor.Attributes["Rfc"].Value;

            XmlNode nomina = null;

            foreach (XmlNode nodo in complemento)
                if (nodo.Name == "nomina12:Nomina")
                    nomina = nodo;

            if (nomina != null)
            {
                List<string> atributosNomina = new List<string>();
                foreach (XmlAttribute atribute in nomina.Attributes)
                    atributosNomina.Add(atribute.Name);


                diasTrabajados = atributosNomina.Contains("NumDiasPagados") ? nomina.Attributes["NumDiasPagados"].Value : "";
                //folioFiscal = atributosNomina.Contains("") ? nomina.Attributes[""].Value : ""; ///??????????????
                periodoPagoInicial = atributosNomina.Contains("FechaInicialPago") ? nomina.Attributes["FechaInicialPago"].Value : "";
                periodoPagoFinal = atributosNomina.Contains("FechaFinalPago") ? nomina.Attributes["FechaFinalPago"].Value : "";
                certificadoDigital = atributosRoot.Contains("NoCertificado") ? root.Attributes["NoCertificado"].Value : "";
                //cadenaOriginalCertificadoSAT = atributosRoot.Contains("certificado") ? root.Attributes["certificado"].Value : "";
            }

            XmlNode nominaReceptor = null;

            if (nomina != null) //LQMA add 12052017 que no sea null
                foreach (XmlNode nodo in nomina)
                    if (nodo.Name == "nomina12:Receptor")
                        nominaReceptor = nodo;

            if (nominaReceptor != null)
            {
                List<string> atributosNominaReceptor = new List<string>();
                foreach (XmlAttribute atribute in nominaReceptor.Attributes)
                    atributosNominaReceptor.Add(atribute.Name);

                noEmpleado = atributosNominaReceptor.Contains("NumEmpleado") ? nominaReceptor.Attributes["NumEmpleado"].Value : "";
                nssReceptor = atributosNominaReceptor.Contains("NumSeguridadSocial") ? nominaReceptor.Attributes["NumSeguridadSocial"].Value : "";
                curpReceptor = atributosNominaReceptor.Contains("Curp") ? nominaReceptor.Attributes["Curp"].Value : "";
                departamento = atributosNominaReceptor.Contains("Departamento") ? nominaReceptor.Attributes["Departamento"].Value : "";
                salarioBase = atributosNominaReceptor.Contains("SalarioBaseCotApor") ? nominaReceptor.Attributes["SalarioBaseCotApor"].Value : "";
            }


            if (timbreFiscal != null)
                serieCertificadoSAT = timbreFiscal.Attributes["NoCertificadoSAT"].Value;

            //receptor************************************************************
            //percepciones********************************************************
            string totalPercepciones = "0", totalExentoPer = "0";

            List<Conceptos> Percepciones = new List<Conceptos>();
            XmlNode nodoPercepciones = null;

            if (nomina != null) //LQMA add 12052017 que no sea null
                foreach (XmlNode nodo in nomina)
                    if (nodo.Name == "nomina12:Percepciones")
                        nodoPercepciones = nodo;

            if (nodoPercepciones != null)
            {
                ////LQMA add 12052017 que no sea null
                if (nodoPercepciones.Attributes["TotalGravado"] != null)
                    totalPercepciones = nodoPercepciones.Attributes["TotalGravado"].Value;
                ////LQMA add 12052017 que no sea null
                if (nodoPercepciones.Attributes["TotalExento"] != null)
                    totalExentoPer = nodoPercepciones.Attributes["TotalExento"].Value;

                totalPercepciones = (Convert.ToDecimal(totalPercepciones) + Convert.ToDecimal(totalExentoPer)).ToString();

                foreach (XmlNode nodo in nodoPercepciones)
                {
                    if (nodo.Name == "nomina12:Percepcion")
                        Percepciones.Add(new Conceptos(nodo.Attributes["Clave"].Value, nodo.Attributes["Concepto"].Value, (Convert.ToDecimal(nodo.Attributes["ImporteGravado"].Value) + Convert.ToDecimal(nodo.Attributes["ImporteExento"].Value)).ToString()));
                }
            }

            //percepciones********************************************************
            //deducciones*********************************************************
            string totalDeducciones = "0", totalExentoDed = "0", TotalOtrasDeducciones = "0", TotalImpuestosRetenidos = "0";

            List<Conceptos> Deducciones = new List<Conceptos>();
            XmlNode nodoDeducciones = null;

            if (nomina != null) //LQMA add 12052017 que no sea null
                foreach (XmlNode nodo in nomina)
                    if (nodo.Name == "nomina12:Deducciones")
                        nodoDeducciones = nodo;

            if (nodoDeducciones != null)
            {
                if (nodoDeducciones.Attributes["TotalGravado"] != null)
                    totalDeducciones = nodoDeducciones.Attributes["TotalGravado"].Value;
                if (nodoDeducciones.Attributes["TotalExento"] != null)
                    totalExentoDed = nodoDeducciones.Attributes["TotalExento"].Value;
                if (nodoDeducciones.Attributes["TotalOtrasDeducciones"] != null)
                    TotalOtrasDeducciones = nodoDeducciones.Attributes["TotalOtrasDeducciones"].Value;
                if (nodoDeducciones.Attributes["TotalImpuestosRetenidos"] != null)
                    TotalImpuestosRetenidos = nodoDeducciones.Attributes["TotalImpuestosRetenidos"].Value;

                totalDeducciones = (Convert.ToDecimal(totalDeducciones) + Convert.ToDecimal(totalExentoDed) + Convert.ToDecimal(TotalOtrasDeducciones) + Convert.ToDecimal(TotalImpuestosRetenidos)).ToString();

                foreach (XmlNode nodo in nodoDeducciones)
                    Deducciones.Add(new Conceptos(nodo.Attributes["Clave"].Value, nodo.Attributes["Concepto"].Value, (Convert.ToDecimal(nodo.Attributes["Importe"].Value)).ToString()));
                // Deducciones.Add(new Conceptos(nodo.Attributes["Clave"].Value, nodo.Attributes["Concepto"].Value, (Convert.ToDecimal(nodo.Attributes["ImporteGravado"].Value) + Convert.ToDecimal(nodo.Attributes["ImporteExento"].Value)).ToString()));
            }
            //deducciones*********************************************************

            //LQMA ADD 18050217 SubsidioAlEmpleo
            //Otros pagos, OtroPago, SubsidioAlEmpleo*****************************
            //BEGIN
            XmlNode nodoOtrosPagos = null;

            if (nomina != null) //LQMA add 12052017 que no sea null
                foreach (XmlNode nodo in nomina)
                    if (nodo.Name == "nomina12:OtrosPagos")
                    {
                        nodoOtrosPagos = nodo;
                        foreach (XmlNode nodoOtros in nodoOtrosPagos)
                            if (nodoOtros.Name == "nomina12:OtroPago")
                            {
                                totalPercepciones = (Convert.ToDecimal(totalPercepciones) + Convert.ToDecimal(nodoOtros.Attributes["Importe"].Value)).ToString();
                                Percepciones.Add(new Conceptos(nodoOtros.Attributes["Clave"].Value, nodoOtros.Attributes["Concepto"].Value, (Convert.ToDecimal(nodoOtros.Attributes["Importe"].Value)).ToString()));
                            }
                    }
            //END
            //Otros pagos, OtroPago, SubsidioAlEmpleo*****************************

            //importes************************************************************
            string formaPago = "", metodoPago = "", numeroCuentaPago = "", lugarExpedicion = "", isr = "", totalPagar = "";

            //LQMA ADD 12052017
            if (root.Attributes["FormaPago"] != null)
                formaPago = root.Attributes["FormaPago"].Value;
            //LQMA ADD 12052017
            if (root.Attributes["MetodoPago"] != null)
                metodoPago = root.Attributes["MetodoPago"].Value;

            //LQMA ADD 12052017 
            if (root.Attributes["NumCtaPago"] != null)
                numeroCuentaPago = root.Attributes["NumCtaPago"].Value;
            //LQMA ADD 12052017
            if (root.Attributes["LugarExpedicion"] != null)
                lugarExpedicion = root.Attributes["LugarExpedicion"].Value;
            //LQMA ADD 12052017
            if (root.Attributes["Total"] != null)
                totalPagar = root.Attributes["Total"].Value;

            XmlNode impuestos = xmlDoc.SelectSingleNode("/x:Comprobante/x:Impuestos", nsMgr);

            if (impuestos != null)
            {
                //LQMA ADD 12052017
                if (impuestos.Attributes["TotalImpuestosRetenidos"] != null)
                    isr = impuestos.Attributes["TotalImpuestosRetenidos"].Value;
            }
            else
                isr = TotalImpuestosRetenidos;

            /*XmlNode retenciones = xmlDoc.SelectSingleNode("/x:Comprobante/x:Impuestos/x:Retenciones", nsMgr);

            foreach (XmlNode nodo in retenciones)
                if (nodo.Attributes["impuesto"].Value == "ISR")
                    isr = nodo.Attributes["importe"].Value;
                    */

            //importes************************************************************
            //QR******************************************************************

            pathQR = creaQR(rutaQR, nombreQR, "?re=" + rfcEmisor + "&rr=" + rfcReceptor + "&tt=" + totalPagar + "&id=" + folioFiscal);

            cadenaOriginalCertificadoSAT = "||" + version + "|" + folioFiscal + "|" + fechaCertificacion + "|" + selloCFD + "|" + selloSAT + "|" + serieCertificadoSAT;
            //QR******************************************************************

            string json = "{ " + ((char)34) + "encabezado" + ((char)34) +
                                                     " : [ { " +
                                                             ((char)34) + "nombreEmisor" + ((char)34) + " : " + ((char)34) + nombreEmisor + ((char)34) + "," +
                                                             ((char)34) + "calle" + ((char)34) + " : " + ((char)34) + calle + ((char)34) + "," +
                                                             ((char)34) + "noExterior" + ((char)34) + " : " + ((char)34) + noExterior + ((char)34) + "," +
                                                             ((char)34) + "noInterior" + ((char)34) + " : " + ((char)34) + noInterior + ((char)34) + "," +
                                                             ((char)34) + "colonia" + ((char)34) + " : " + ((char)34) + colonia + ((char)34) + "," +
                                                             ((char)34) + "municipio" + ((char)34) + " : " + ((char)34) + municipio + ((char)34) + "," +
                                                             ((char)34) + "codigoPostal" + ((char)34) + " : " + ((char)34) + codigoPostal + ((char)34) + "," +
                                                             ((char)34) + "pais" + ((char)34) + " : " + ((char)34) + pais + ((char)34) + "," +
                                                             ((char)34) + "estado" + ((char)34) + " : " + ((char)34) + estado + ((char)34) + "," +
                                                             ((char)34) + "rfcEmisor" + ((char)34) + " : " + ((char)34) + rfcEmisor + ((char)34) + "," +
                                                             ((char)34) + "regimenFiscal" + ((char)34) + " : " + ((char)34) + regimenFiscal + ((char)34) + "," +

                                                             ((char)34) + "serie" + ((char)34) + " : " + ((char)34) + serie + ((char)34) + "," +
                                                             ((char)34) + "folio" + ((char)34) + " : " + ((char)34) + folio + ((char)34) + "," +
                                                             ((char)34) + "fechaCertificacion" + ((char)34) + " : " + ((char)34) + fechaCertificacion + ((char)34) + "," +
                                                             ((char)34) + "fechaEmision" + ((char)34) + " : " + ((char)34) + fechaEmision + ((char)34) +
                                                       "} ]," +
                                  ((char)34) + "receptorFiscal" + ((char)34) +
                                                     " : [ { " +
                                                             ((char)34) + "nombreReceptor" + ((char)34) + " : " + ((char)34) + nombreReceptor + ((char)34) + "," +
                                                             ((char)34) + "noEmpleado" + ((char)34) + " : " + ((char)34) + noEmpleado + ((char)34) + "," +
                                                             ((char)34) + "nssReceptor" + ((char)34) + " : " + ((char)34) + nssReceptor + ((char)34) + "," +
                                                             ((char)34) + "rfcReceptor" + ((char)34) + " : " + ((char)34) + rfcReceptor + ((char)34) + "," +
                                                             ((char)34) + "curpReceptor" + ((char)34) + " : " + ((char)34) + curpReceptor + ((char)34) + "," +
                                                             ((char)34) + "salarioBase" + ((char)34) + " : " + ((char)34) + salarioBase + ((char)34) + "," +
                                                             ((char)34) + "departamento" + ((char)34) + " : " + ((char)34) + departamento + ((char)34) + "," +
                                                             ((char)34) + "diasTrabajados" + ((char)34) + " : " + ((char)34) + diasTrabajados + ((char)34) + "," +
                                                             ((char)34) + "folioFiscal" + ((char)34) + " : " + ((char)34) + folioFiscal + ((char)34) + "," +
                                                             ((char)34) + "certificadoDigital" + ((char)34) + " : " + ((char)34) + certificadoDigital + ((char)34) + "," +
                                                             ((char)34) + "serieCertificadoSAT" + ((char)34) + " : " + ((char)34) + serieCertificadoSAT + ((char)34) + "," +
                                                             ((char)34) + "periodoPagoInicial" + ((char)34) + " : " + ((char)34) + periodoPagoInicial + ((char)34) + "," +
                                                             ((char)34) + "periodoPagoFinal" + ((char)34) + " : " + ((char)34) + periodoPagoFinal + ((char)34) +
                                                       "} ],";
            if (Percepciones.Count > 0)
            {
                json += ((char)34) + "percepciones" + ((char)34) +
                                                         " : [ ";

                foreach (Conceptos percepcion in Percepciones)
                    json += "{ " + ((char)34) + "clave" + ((char)34) + " : " + ((char)34) + percepcion.Clave + ((char)34) + "," +
                                   ((char)34) + "concepto" + ((char)34) + " : " + ((char)34) + percepcion.Concepto + ((char)34) + "," +
                                   ((char)34) + "ImporteGravado" + ((char)34) + " : " + ((char)34) + percepcion.Importe + ((char)34) +
                             "},";
                json = json.Remove(json.LastIndexOf(","));

                json += "],";
            }

            if (Deducciones.Count > 0)
            {
                json += ((char)34) + "deducciones" + ((char)34) +
             " : [ ";

                foreach (Conceptos deduccion in Deducciones)
                    json += "{ " + ((char)34) + "clave" + ((char)34) + " : " + ((char)34) + deduccion.Clave + ((char)34) + "," +
                                   ((char)34) + "concepto" + ((char)34) + " : " + ((char)34) + deduccion.Concepto + ((char)34) + "," +
                                   ((char)34) + "ImporteGravado" + ((char)34) + " : " + ((char)34) + deduccion.Importe + ((char)34) +
                             "},";

                json = json.Remove(json.LastIndexOf(","));

                json += "],";
            }

            json += ((char)34) + "importe" + ((char)34) +
         " : [ { " +
                 ((char)34) + "formaPago" + ((char)34) + " : " + ((char)34) + formaPago + ((char)34) + "," +
                 ((char)34) + "metodoPago" + ((char)34) + " : " + ((char)34) + metodoPago + ((char)34) + "," +
                 ((char)34) + "numeroCuentaPago" + ((char)34) + " : " + ((char)34) + numeroCuentaPago + ((char)34) + "," +
                 ((char)34) + "lugarExpedicion" + ((char)34) + " : " + ((char)34) + lugarExpedicion + ((char)34) + "," +
                 ((char)34) + "totalPercepciones" + ((char)34) + " : " + ((char)34) + totalPercepciones + ((char)34) + "," +
                 ((char)34) + "totalDeducciones" + ((char)34) + " : " + ((char)34) + totalDeducciones + ((char)34) + "," +
                 ((char)34) + "departamento" + ((char)34) + " : " + ((char)34) + departamento + ((char)34) + "," +
                 ((char)34) + "isr" + ((char)34) + " : " + ((char)34) + isr + ((char)34) + "," +
                 ((char)34) + "totalPagar" + ((char)34) + " : " + ((char)34) + totalPagar + ((char)34) +
           "} ]," +

((char)34) + "sellos" + ((char)34) +
         " : [ { " +
                //((char)34) + "urlQr" + ((char)34) + " : " + ((char)34) + pathQR + ((char)34) + "," +
                 ((char)34) + "urlQr" + ((char)34) + " : " + ((char)34) + urlQR + nombreQR + ((char)34) + "," +
                 ((char)34) + "cadenaOriginalSAT" + ((char)34) + " : " + ((char)34) + cadenaOriginalCertificadoSAT + ((char)34) + "," +
                 ((char)34) + "selloDigitalCFDI" + ((char)34) + " : " + ((char)34) + selloCFD + ((char)34) + "," +
                 ((char)34) + "selloDigitalSAT" + ((char)34) + " : " + ((char)34) + selloSAT + ((char)34) +
           "} ] }";

            return json;
            // To convert JSON text contained in string json into an XML node
            //XmlDocument doc = JsonConvert.DeserializeXmlNode(json);
        }
    }

    public class Conceptos
    {
        private string clave;
        private string concepto;
        private string importe;

        public Conceptos(string claveS, string conceptoS, string importeS)
        {
            this.clave = claveS;
            this.concepto = conceptoS;
            this.importe = importeS;
        }

        public string Clave
        {
            get { return clave; }
            set { clave = value; }
        }

        public string Concepto
        {
            get { return concepto; }
            set { concepto = value; }
        }

        public string Importe
        {
            get { return importe; }
            set { importe = value; }
        }
    }

}
