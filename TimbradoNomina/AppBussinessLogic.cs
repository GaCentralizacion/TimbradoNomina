using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using CommonFunctions.Functions;
using CommonFunctions.DataAccess;
using System.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;
using TimbradoNomina.NominaObjects;
using System.Net;


namespace TimbradoNomina
{
    class AppBussinessLogic
    {
        private static string GlobalLogFilePath = "";
        private static string directory = "\\XMLCancelaciones\\";
        private static string originPath = "";
        private static string cancelOriginPath = "C:\\TimbradoNominaParaRH" + directory + "\\CancelacionOrigen\\";
        private static string correctlyProcessedFiles = "C:\\TimbradoNominaParaRH" + directory + "\\ArchivosProcesados\\Creados\\";
        private static string incorrectlyProcessedFiles = "C:\\TimbradoNominaParaRH" + directory + "\\ArchivosProcesados\\NoCreados\\";
        private static string cancelledFiles = "C:\\TimbradoNominaParaRH" + directory + "\\Cancelados\\";
        private static string nonCancelledFiles = "C:\\TimbradoNominaParaRH" + directory + "\\NoCancelados\\";
        private static string errorPathFiles = "C:\\TimbradoNominaParaRH" + directory + "\\ArchivosConError\\";
        private static string acusseFileName = "";
        private static string pfxPath = ConfigurationManager.AppSettings["requiredPath"] + "prueba.pfx";


        private static string ProcessedFiles = ConfigurationManager.AppSettings["ProcessedFiles"];



        //CargaXML
        string ConnectionString = ConfigurationManager.AppSettings["ConnectionString"];
        string InsertarEnBD = ConfigurationManager.AppSettings["InsertarEnBD"];
        string ID_BLOQUE = ConfigurationManager.AppSettings["ID_BLOQUE"];

        Dictionary<string, string> dicConceptos = new Dictionary<string, string>(); //Se guardan los conceptos de Percepciones
        Dictionary<string, string> dicConceptosOP = new Dictionary<string, string>(); //Se guardan los conceptos de OtrosPagos
        Dictionary<string, string> dicConceptosDed = new Dictionary<string, string>(); //Se guardan los conceptos de Deducciones

        ConexionBD objDB = null;
        //CargaXML

        string data;

        public string Data
        {
            get { return data; }
            set { data = value; }
        }

        private string _LogPath = string.Empty;
        private string _LogCancel = string.Empty;
        private int _PDFError = 0;
        private int _Error = 0;
        private int _Timbrado = 0;
        private int _NoTimbrado = 0;
        private int _Iteraciones = 0;


        public void StartProcessDirectory()
        {
            Console.WriteLine(data);

            if (!JsonIsvalid(data)) return;
            try { StartStamp(data); }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void SetConfigurationFromFile(Configuracion conf)
        {
            conf.XsltPath = ConfigurationManager.AppSettings["xsltPath"];
            conf.XsdPath = ConfigurationManager.AppSettings["xsdPath"];
            conf.NameSpace = ConfigurationManager.AppSettings["xsdNameSpace"];
            conf.QrPath = ConfigurationManager.AppSettings["qrSavePath"];
            conf.ReportUrl = ConfigurationManager.AppSettings["reportUrl"];
            conf.RequiredPath = ConfigurationManager.AppSettings["requiredPath"];
            conf.OpenSSLStartPath = ConfigurationManager.AppSettings["opensslAppPath"];
            conf.QrUrl = ConfigurationManager.AppSettings["qrUrl"];
            conf.CFDINameSpace = ConfigurationManager.AppSettings["cfdiNameSpace"];
            WriteLog("Parametros asignados webconfig");
        }

        private void SetConfigurationDataBase(Configuracion conf, Carpeta folder)
        {
            SqlParameter[] parameterList = { new SqlParameter("@idEmpresa", folder.IDEmpresa) };

            SqlServer BaseNomina = new SqlServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();
            System.Data.DataSet ds = BaseNomina.ExecuteQueryProcedure("SEL_CERTIFICADOS_SP", parameterList);

            if (ds.Tables[0].Rows.Count > 0)
            {
                conf.KeyPemPath = conf.RequiredPath + ds.Tables[0].Rows[0][0].ToString();
                conf.SuscriptorRFC = ds.Tables[0].Rows[0][1].ToString();
                conf.AgenteTI = ds.Tables[0].Rows[0][2].ToString();
                conf.CertB64Content = ds.Tables[0].Rows[0][3].ToString();
                conf.CertificateNumber = ds.Tables[0].Rows[0][4].ToString();
            }

            WriteLog("Parametros asignados Base de Datos");
        }

        private void SetConfigurationDataBase(Configuracion conf, string IdEmpresa)
        {
            SqlParameter[] parameterList = { new SqlParameter("@idEmpresa", IdEmpresa) };

            SQLServer BaseNomina = new SQLServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();
            System.Data.DataSet ds = BaseNomina.ExecuteQueryProcedure("SEL_CERTIFICADOS_SP", parameterList);
            conf.OpenSSLStartPath = ConfigurationManager.AppSettings["opensslAppPath"];
            conf.RequiredPath = ConfigurationManager.AppSettings["requiredPath"];

            if (ds.Tables[0].Rows.Count > 0)
            {
                conf.KeyPemPath = conf.RequiredPath + ds.Tables[0].Rows[0][0].ToString();
                conf.SuscriptorRFC = ds.Tables[0].Rows[0][1].ToString();
                conf.AgenteTI = ds.Tables[0].Rows[0][2].ToString();
                conf.CertB64Content = ds.Tables[0].Rows[0][3].ToString();
                conf.CertificateNumber = ds.Tables[0].Rows[0][4].ToString();
                conf.CSD = conf.RequiredPath + ds.Tables[0].Rows[0][5].ToString();
                conf.CSDPASSWORD = ds.Tables[0].Rows[0][6].ToString();
            }
        }

        private void ProcessFile(Carpeta carpetaTimbrar, Configuracion configuraTimbrado, ref string[] files, int current)
        {
            if (current >= files.Length)
            {
                string resultado = string.Format("Carpeta:{0}|TotalDocs:{1}|Timbrados:{2}|NoTimbrados:{3}|Error:{4}",
                       carpetaTimbrar.NombreDirectorio, carpetaTimbrar.NoDocumentos, _Timbrado, _NoTimbrado, _Error);
                
                Console.WriteLine(resultado);
                Console.WriteLine("PDFs no generados:" + _PDFError.ToString());
                WriteLog(resultado, "FNP");

                if (!carpetaTimbrar.HuboError)
                    UpdateProcessedDirectory(carpetaTimbrar);

                    DeleteQrFiles(configuraTimbrado);

                return;
            }

            string metodo = string.Empty;

            Documento documento = new Documento();
            documento.NombreXml = Path.GetFileNameWithoutExtension(files[current]);
            documento.RutaDirectorio = carpetaTimbrar.RutaDirectorio;
            documento.RutaDirectorioError = carpetaTimbrar.BadPath;
            documento.RutaDirectorioOk = carpetaTimbrar.OkPath;
            documento.IDNomina = carpetaTimbrar.IDNomina;
            documento.RutaXml = files[current];
            documento.CodigoResultado = "-1";
            documento.XmlResultString = "";
            documento.Description = "NA";
            documento.IDEmpresa = carpetaTimbrar.IDEmpresa;

            int procNumber = _Timbrado + _Error + _NoTimbrado + 1;

            Console.Write(string.Format("{0} DE {1}|{2}|{3}|", procNumber.ToString().PadLeft(4, ' '),
                carpetaTimbrar.NoDocumentos.ToString(), DateTime.Now.ToString(), documento.NombreXml));

            WriteLog("Comienza Archivo:" + documento.NombreXml, "INI");

            try
            {
                metodo = "RemoveNode"; RemoveNode(documento, configuraTimbrado);
                metodo = "CreateLayoutString"; CreateLayoutString(documento, configuraTimbrado);
                metodo = "SignLayoutString"; SignLayoutString(documento, configuraTimbrado);
                metodo = "StampFile"; StampFile(documento, configuraTimbrado);
                metodo = "LoadDocument"; documento.XmlOrigenString = LoadDocument(documento.RutaXml);
                metodo = "EncodeToBase64"; documento.XmlOrigenBase64 = EncodeToBase64(documento.XmlOrigenString);
                metodo = "Timbrar"; Timbrar(documento, configuraTimbrado);
                //metodo = "SimularTimbrar"; SimularTimbrar(documento, configuraTimbrado);
                metodo = "DeleteStampFile"; DeleteStampFile(documento);

                if (documento.CodigoResultado == "100")
                {
                    metodo = "SaveStampFile"; SaveStampFile(documento, configuraTimbrado, carpetaTimbrar);
                    metodo = "SetNewXMLParams"; SetNewXMLParams(documento, configuraTimbrado);
                    Console.Write("Timbrado");
                    _Timbrado++;

                    SetPDFValues(documento, configuraTimbrado, carpetaTimbrar);
                }
                else
                {
                    metodo = "SetEmptyInsertParams"; SetEmptyInsertParams(documento);
                    metodo = "SaveUnstampFile"; SaveUnstampFile(documento, carpetaTimbrar);
                    Console.WriteLine("NoTimbrado");
                    _NoTimbrado++;
                }

            }
            catch (PDFException pdfex)
            {
                _PDFError++;
                WriteLog(string.Format("DOC:{0}|MET:{1}|DES:{2}", documento.NombreXml, "SetPDFValues", pdfex.Message), "PDF");
            }
            catch(Exception ex)
            {
                _Error++;
                Console.WriteLine("ERROR");
                carpetaTimbrar.HuboError = true;
                SetEmptyInsertParams(documento);
                WriteLog(string.Format("DOC:{0}|MET:{1}|DES:{2}", documento.NombreXml, metodo, ex.Message), "ERR");
            }
            finally
            {
                WriteLog(string.Format("DOC:{0}|COD:{1}|DESC:{2}", documento.NombreXml, documento.CodigoResultado, documento.Description), "TIM");
                WriteLog("DOC: " + documento.NombreXml, "FIN");

                if (_Iteraciones >= 50)
                {
                    System.Threading.Thread.Sleep(3000);
                    _Iteraciones = 0;
                }

                _Iteraciones++;

                InsertProcessedFile(carpetaTimbrar, documento);
                ProcessFile(carpetaTimbrar, configuraTimbrado, ref  files, ++current);
            }

        }

        private void SetPDFValues(Documento documento, Configuracion configuraTimbrado, Carpeta carpetaTimbrar)
        {
            try
            {
                string json = GetJson(documento, configuraTimbrado, carpetaTimbrar);
                CreatePDF(json, documento, configuraTimbrado);
                Console.WriteLine("|PDFOK");
            }
            catch (Exception ex)
            {
                Console.WriteLine("|PDFNO");
                throw new PDFException(ex.Message);
            }
        }

        public string CreateLogDirectory()
        {
            string logPath = string.Format("{0}\\Logs\\", "C:\\TimbradoNominaParaRH");

            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
                Console.WriteLine(string.Format("Se creo el directorio: '{0}'", logPath));
            }

            return logPath;
        }

        public void CreateLogFileCancelacion(string logPath)
        {
            string logFilePath = String.Format("Log{0}.txt", DateTime.Now.ToString("Mddyyyy"));
            GlobalLogFilePath = logPath + logFilePath;

            if (!File.Exists(GlobalLogFilePath))
            {
                using (FileStream fs = File.Create(GlobalLogFilePath))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes("Inicio de archivo Log\n");
                    fs.Write(info, 0, info.Length);
                    byte[] newline = Encoding.ASCII.GetBytes(Environment.NewLine);
                    fs.Write(newline, 0, newline.Length);
                }
                WriteLog("Archivo Log creado correctamente.");
            }
        }

        public void CreatePaths()
        {
            CreateDirectory(cancelOriginPath);
            CreateDirectory(correctlyProcessedFiles);
            CreateDirectory(incorrectlyProcessedFiles);
            CreateDirectory(cancelledFiles);
            CreateDirectory(nonCancelledFiles);
            CreateDirectory(errorPathFiles);
            Console.WriteLine("Se Crean las rutas de los archivos");
            Console.WriteLine(cancelOriginPath);
            Console.WriteLine(correctlyProcessedFiles);
            Console.WriteLine(incorrectlyProcessedFiles);
            Console.WriteLine(cancelledFiles);
            Console.WriteLine(nonCancelledFiles);
            Console.WriteLine(errorPathFiles);
         }

        private string GetJson(Documento documento, Configuracion configuraTimbrado, Carpeta carpetaTimbrar)
        {
            return new Template().leerXML(documento.RutaArchivoTimbrado, configuraTimbrado.QrPath, documento.NombreXml + ".png", configuraTimbrado.QrUrl);
        }

        private void SaveStampFile(Documento documento, Configuracion configuraTimbrado, Carpeta carpetaTimbrar)
        {
            if (File.Exists(documento.RutaArchivoOk))
            {
                string tempName = "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".tmp";
                File.Move(documento.RutaArchivoOk, documento.RutaArchivoRepetido + tempName);
            }
            File.WriteAllText(documento.RutaArchivoTimbrado, documento.XmlResultString);
            File.Move(documento.RutaXml, documento.RutaArchivoOk);

            WriteLog(string.Format("Ruta Archivo timbrado:{0}", documento.RutaArchivoTimbrado));
        }

        private void SaveUnstampFile(Documento documento, Carpeta carpetaTimbrar)
        {

            if (File.Exists(documento.RutaArchivoError))
            {
                string tempName = "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".tmp";
                File.Move(documento.RutaArchivoError, documento.RutaArchivoRepetidoError + tempName);
            }

            File.Move(documento.RutaXml, documento.RutaArchivoError);
            WriteLog(string.Format("Archivo: {0} |Result:{1} |Desc:{2}", documento.NombreXml, documento.CodigoResultado, documento.Description));

        }

        private void SetNewXMLParams(Documento doc, Configuracion conf)
        {
            //xmlDoc.Load(doc.RutaArchivoTimbrado);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(doc.RutaArchivoOk);

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("cfdi", conf.CFDINameSpace);
            nsMgr.AddNamespace("nomina12", "http://www.sat.gob.mx/nomina12");

            XmlNode nodoComprobante = xmlDoc.SelectSingleNode("/cfdi:Comprobante", nsMgr);
            XmlNode nodoReceptor = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Receptor", nsMgr);
            XmlNode nodoConcepto = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Conceptos/cfdi:Concepto", nsMgr);
            XmlNode nodoNomina = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Complemento", nsMgr).FirstChild;
            XmlNode nodoNominaReceptor = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Complemento/nomina12:Nomina/nomina12:Receptor", nsMgr);

            InsertParams param = new InsertParams();

            param.Fecha = (nodoComprobante.Attributes["fecha"] != null) ? DateTime.Parse(nodoComprobante.Attributes["fecha"].Value) : (DateTime?)null;
            param.Total = (nodoComprobante.Attributes["total"] != null) ? decimal.Parse(nodoComprobante.Attributes["total"].Value) : 0;
            param.Nombre = (nodoReceptor.Attributes["nombre"] != null) ? nodoReceptor.Attributes["nombre"].Value : string.Empty;
            param.Descripcion = (nodoConcepto.Attributes["descripcion"] != null) ? nodoConcepto.Attributes["descripcion"].Value : string.Empty;
            param.FechaPago = (nodoNomina.Attributes["FechaPago"] != null) ? DateTime.Parse(nodoNomina.Attributes["FechaPago"].Value) : (DateTime?)null;
            param.Departamento = (nodoNominaReceptor.Attributes["Departamento"] != null) ? nodoNominaReceptor.Attributes["Departamento"].Value : string.Empty;
            param.NumEmpleado = (nodoNominaReceptor.Attributes["NumEmpleado"] != null) ? nodoNominaReceptor.Attributes["NumEmpleado"].Value : string.Empty;
            param.Puesto = (nodoNominaReceptor.Attributes["Puesto"] != null) ? nodoNominaReceptor.Attributes["Puesto"].Value : string.Empty;

            doc.Parametros = param;
        }

        private void RemoveNode(Documento docOrigen, Configuracion conf)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.PreserveWhitespace = false;
            xmlDoc.Load(docOrigen.RutaXml);

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsMgr.AddNamespace("cfdi", conf.CFDINameSpace);

            XmlNode nodoComprobante = xmlDoc.SelectSingleNode("/cfdi:Comprobante", nsMgr);
            XmlNode nodoImpuestos = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Impuestos", nsMgr);
            XmlNode nodoConceptos = xmlDoc.SelectSingleNode("/cfdi:Comprobante/cfdi:Conceptos", nsMgr);

            if (nodoImpuestos != null) nodoComprobante.RemoveChild(nodoImpuestos);

            //XmlElement elemImpuestos = xmlDoc.CreateElement("cfdi", "Impuestos", conf.CFDINameSpace);
            //nodoComprobante.InsertAfter(elemImpuestos, nodoConceptos);
            xmlDoc.Save(docOrigen.RutaXml);

            WriteLog("Nodo Impuesto Renombrado:" + docOrigen.NombreXml);
        }

        private void StampFile(Documento docOrigen, Configuracion conf)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(docOrigen.RutaXml);
            XmlElement root = xmlDoc.DocumentElement;

            string stamp = LoadDocument(docOrigen.RutaArchivoCadenaOriginalB64);

            root.SetAttribute("Sello", stamp);
            root.SetAttribute("Certificado", conf.CertB64Content);
            root.SetAttribute("NoCertificado", conf.CertificateNumber);

            xmlDoc.Save(docOrigen.RutaXml);

            WriteLog("Archivo sellado:" + docOrigen.NombreXml);
        }

        private void CreateLayoutString(Documento docOrigen, Configuracion conf)
        {
            string cadenaOriginal = TransformXml(docOrigen.RutaXml, conf.XsltPath);
            File.WriteAllText(docOrigen.RutaArchivoCadenaOriginal, cadenaOriginal);
        }

        private void SignLayoutString(Documento docOrigen, Configuracion conf)
        {
            string argsString = string.Format("dgst -sha256 -out \"{0}\" -sign \"{1}\" \"{2}\" ", docOrigen.RutaArchivoFirma, conf.KeyPemPath, docOrigen.RutaArchivoCadenaOriginal);
            ExcuteCommandLine(conf.OpenSSLStartPath, argsString);
            argsString = string.Format("enc -in \"{0}\" -a -A -out \"{1}\" ", docOrigen.RutaArchivoFirma, docOrigen.RutaArchivoCadenaOriginalB64);
            ExcuteCommandLine(conf.OpenSSLStartPath, argsString);
            WriteLog("Archivo Firmado:" + docOrigen.NombreXml);
            System.Threading.Thread.Sleep(500);
        }

        private void InsertProcessedFile(Carpeta carpeta, Documento doc)
        {
            SqlParameter[] parameterList = {
                new SqlParameter("@idEmpresa",doc.IDEmpresa),
                new SqlParameter("@idTipo",carpeta.IDTipo),
                new SqlParameter("@idUsuario",carpeta.IDUsuario ),
                new SqlParameter("@NombreArchivo",doc.NombreXml),
                new SqlParameter("@estatus", doc.CodigoResultado),
                new SqlParameter("@idNomina",doc.IDNomina),
                new SqlParameter("@descripcionEstatus",doc.Description),
                new SqlParameter("@totalPago",doc.Parametros.Total),
                new SqlParameter("@nombreEmpleado",doc.Parametros.Nombre),
                new SqlParameter("@IdEmpleado",doc.Parametros.NumEmpleado),
                new SqlParameter("@Departamentoxml",doc.Parametros.Departamento),
                new SqlParameter("@Puestoxml",doc.Parametros.Puesto )
                };

            SqlServer BaseNomina = new SqlServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();
            BaseNomina.ExecuteNonQueryProcedure("INS_TIMBRADO_SP", parameterList);

        }

        private void ConvertJsonToClass(string json, Carpeta carpeta)
        {
            dynamic obj = JsonConvert.DeserializeObject(json);

            carpeta.RutaDirectorio = obj.path + "\\";
            carpeta.IDEmpresa = obj.idEmpresa;
            carpeta.IDTipo = obj.idTipo;
            carpeta.IDUsuario = obj.idUsuario;
            carpeta.NombreDirectorio = obj.nombreCarpeta;
            carpeta.NoDocumentos = "0";
        }

        private void Timbrar(Documento doc, Configuracion conf)
        {
            //WSCFDI.timbrarCFDI wsTimbrar = new WSCFDI.timbrarCFDI();
            //WSCFDI.respuestaTimbrado result = wsTimbrar.CalltimbrarCFDI(conf.SuscriptorRFC, conf.AgenteTI, doc.XmlOrigenBase64);

            WSEdifact.timbrarCFDI wsTimbrar = new WSEdifact.timbrarCFDI();
            WSEdifact.respuestaTimbrado result = wsTimbrar.CalltimbrarCFDI(conf.SuscriptorRFC, conf.AgenteTI, doc.XmlOrigenBase64);

            doc.Description = result.codigoDescripcion;
            doc.XmlResultBase64 = result.documentoTimbrado;
            doc.CodigoResultado = result.codigoResultado;
            doc.XmlResultString = DecodeFromBase64(doc.XmlResultBase64);

        }

        private void SimularTimbrar(Documento doc, Configuracion conf)
        {
            doc.Description = "timbrado";
            doc.XmlResultBase64 = EncodeToBase64(LoadDocument(@"C:\Users\Hp\Documents\aaaa\archivotim.xml"));
            doc.CodigoResultado = "100";
            doc.XmlResultString = DecodeFromBase64(doc.XmlResultBase64);
            WriteLog("Archivo Timbrado:" + doc.NombreXml);
        }

        private void CreatePDF(string json, Documento doc, Configuracion conf)
        {
            string jsonTemplate = "{ \"template\": { \"name\" : \"timbrado_rpt\" },\"data\" : " + json + "}";

            HTTPManager pdf = new HTTPManager();
            pdf.RequestPDF(conf.ReportUrl, jsonTemplate, doc.RutaArchivoPDF);
            WriteLog("Archivo PDF Creado para:" + doc.NombreXml);
        }

        private void UpdateProcessedDirectory(Carpeta carpeta)
        {
            SqlParameter[] parameterList = { new SqlParameter("@idNomina", carpeta.IDNomina) };

            SqlServer BaseNomina = new SqlServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();
            BaseNomina.ExecuteQueryProcedure("UPD_TIMBRE_EXITO_SP", parameterList);
        }

        private void DeleteStampFile(Documento docOrigen)
        {
            File.Delete(docOrigen.RutaArchivoCadenaOriginalB64);
            File.Delete(docOrigen.RutaArchivoFirma);
            File.Delete(docOrigen.RutaArchivoCadenaOriginal);
        }

        private void SetEmptyInsertParams(Documento doc)
        {
            InsertParams param = new InsertParams();

            param.Fecha = (DateTime?)null;
            param.Total = 0;
            param.Nombre = string.Empty;
            param.Descripcion = string.Empty;
            param.FechaPago = null;
            param.Departamento = string.Empty;
            param.NumEmpleado = string.Empty;
            param.Puesto = string.Empty;

            doc.Parametros = param;
        }


        private string InsertProcessedDirectory(Carpeta carpeta)
        {
            SqlParameter[] parameterList = { 
            new SqlParameter("@idEmpresa",carpeta.IDEmpresa ),
            new SqlParameter("@idTipo ", carpeta.IDTipo),
            new SqlParameter("@idUsuario",carpeta.IDUsuario),
            new SqlParameter("@nombre",carpeta.NombreDirectorio ),
            new SqlParameter("@ruta",carpeta.RutaDirectorio),
            new SqlParameter("@recibos",carpeta.NoDocumentos)
                                           };
            SqlServer BaseNomina = new SqlServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();

            return BaseNomina.ExecuteQueryProcedure("INS_TIMBRADO_CARPETA_SP", parameterList).Tables[0].Rows[0][0].ToString();
        }

        private bool ValidateXML(string xmlPath, Configuracion conf)
        {
            XMLManager xml = new XMLManager();
            return xml.Validate(xmlPath, conf.XsdPath, conf.NameSpace);
        }

        private string TransformXml(string xmlPath, string xsltPath)
        {
            XMLManager xml = new XMLManager();
            return xml.XMLToLayout(xmlPath, xsltPath);
        }

        private int ExcuteCommandLine(string programPath, string argsString)
        {
            var process = Process.Start(programPath, argsString);
            process.WaitForExit();
            return process.ExitCode;
        }

        private bool IsAlreadyProcessed(string filePath)
        {
            SqlServer BaseNomina = new SqlServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();
            SqlParameter[] parameterList = { new SqlParameter("@ruta", filePath) };
            return bool.Parse(BaseNomina.ExecuteQueryProcedure("SEL_TIMBRADO_SP", parameterList).Tables[0].Rows[0][0].ToString());
        }

        private string GetLastDirectory(string directoryPath)
        {
            string[] splitPath = directoryPath.Split('\\');
            return splitPath[splitPath.Length - 1];
        }

        private bool JsonIsvalid(string json)
        {
            bool isValid = false;
            try
            {
                dynamic obj = JsonConvert.DeserializeObject(json);
                isValid = true;
            }
            catch { isValid = false; }

            return isValid;
        }

        private string LoadDocument(string xmlPath)
        {
            IOManager document = new IOManager();
            document.Path = xmlPath;
            document.SetContent();
            return document.Content.Replace("<cfdi:Impuestos />", "<cfdi:Impuestos/>");
        }

        private string EncodeToBase64(string document)
        {
            StringManager encode = new StringManager();
            return encode.ToBase64Encode(document);
        }

        private string DecodeFromBase64(string document)
        {
            StringManager encode = new StringManager();
            return encode.ToBase64Decode(document);
        }

        private void WriteLog(string content, string status)
        {
            string path = _LogPath;
            using (StreamWriter file = new StreamWriter(path, true))
            {
                file.WriteLine("{0}|{1}|{2}", status, DateTime.Now.ToString(), content);
            }
        }

        private void WriteLog(string content)
        {
            string path = _LogPath;
            using (StreamWriter file = new StreamWriter(path, true))
            {
                file.WriteLine("MSG|{0}|{1}", DateTime.Now.ToString(), content);
            }
        }

        public void WriteLogCancel(string content, string status)
        {
            string path = _LogCancel;
            using (StreamWriter file = new StreamWriter(path, true))
            {
                file.WriteLine("{0}|{1}|{2}", status, DateTime.Now.ToString(), content);
            }
        }

        private void WriteLogCancel(string content)
        {
            string path = _LogCancel;
            using (StreamWriter file = new StreamWriter(path, true))
            {
                file.WriteLine("MSG|{0}|{1}", DateTime.Now.ToString(), content);
            }
        }

        public void CreateLogFile(string path)
        {
            if (!File.Exists(path))
            {
                using (FileStream fs = File.Create(path))
                {
                    Byte[] info = new UTF8Encoding(true).GetBytes("Inicio archivo log \n");
                    fs.Write(info, 0, info.Length);
                }
            }
            Console.WriteLine("Creando log........");
            System.Threading.Thread.Sleep(3000);
        }

        private void StartStamp(string data)
        {
            Carpeta carpetaTimbrar = new Carpeta();
            ConvertJsonToClass(data, carpetaTimbrar);
            //int respuesta = 0;

            //Cancelacion
            if (carpetaTimbrar.IDTipo == "Cancelacion")
            {
                _LogPath = string.Format("{0}\\Logs\\log{1}{2}.txt", Directory.GetCurrentDirectory(),
                    "", "Cancelacion");
                _LogCancel = string.Format("{0}\\Log\\log{1}{2}.txt", 
                    "C:\\TimbradoNominaParaRH\\XMLCancelaciones",
                    "", "Cancelacion");
                
                CreateLogFile(_LogPath);

                CreateLogFile(_LogCancel);

                StartCancellation(data);
            }
            else if (carpetaTimbrar.IDTipo == "Conciliacion")
            {
                CreaArchivoCSV(carpetaTimbrar);
                Console.WriteLine("Termino el proceso de carga de archivos en la BD");
            }

            else //if (respuesta == 1)
            {
                if (!Directory.Exists(carpetaTimbrar.RutaDirectorio))
                {
                    Console.WriteLine("No existe la carpeta:" + carpetaTimbrar.RutaDirectorio);
                    return;
                }

                _LogPath = string.Format("{0}\\Logs\\log{1}{2}.txt", Directory.GetCurrentDirectory(),
                    carpetaTimbrar.IDEmpresa.ToString(), carpetaTimbrar.NombreDirectorio);

                CreateLogFile(_LogPath);

                if (IsAlreadyProcessed(carpetaTimbrar.RutaDirectorio))
                {
                    string msg = string.Format("La carpeta {0} ya fue procesada", carpetaTimbrar.NombreDirectorio);
                    Console.WriteLine(msg);
                    WriteLog(msg);
                    return;
                }


                Configuracion configuraTimbrado = new Configuracion();
                SetConfigurationFromFile(configuraTimbrado);
                SetConfigurationDataBase(configuraTimbrado, carpetaTimbrar);

                CreateDirectory(carpetaTimbrar.OkPath);
                CreateDirectory(carpetaTimbrar.OkPathSelf);
                CreateDirectory(carpetaTimbrar.BadPath);

              
                string[] fileEntries = Directory.GetFiles(carpetaTimbrar.RutaDirectorio, "*.xml");
                carpetaTimbrar.NoDocumentos = fileEntries.Count().ToString();
                carpetaTimbrar.IDNomina = InsertProcessedDirectory(carpetaTimbrar);

                Console.WriteLine("Comienza timbrado de carpeta:" + carpetaTimbrar.NombreDirectorio);
                WriteLog("Comienza timbrado de carpeta:" + carpetaTimbrar.NombreDirectorio, "STR");
                WriteLog("Carpeta contiene:" + carpetaTimbrar.NoDocumentos + " archivos.");
                ProcessFile(carpetaTimbrar, configuraTimbrado, ref fileEntries, 0);
                //Se copia la carpeta de una ruta a otra cuando se termina de ejecutar el proceso
                Console.WriteLine("Carpeta Origen: " + carpetaTimbrar.OkPath);
                Console.WriteLine("Carpeta Destino: " + carpetaTimbrar.OkPathSelf);


                string fileName = "";
                string destFile = "";
                if (System.IO.Directory.Exists(carpetaTimbrar.OkPath))
                {
                    string[] files = System.IO.Directory.GetFiles(carpetaTimbrar.OkPath);

                    // Copy the files and overwrite destination files if they already exist.
                    foreach (string s in files)
                    {
                        // Use static Path methods to extract only the file name from the path.
                        fileName = System.IO.Path.GetFileName(s);
                        destFile = System.IO.Path.Combine(carpetaTimbrar.OkPathSelf, fileName);
                        System.IO.File.Copy(s, destFile, true);
                    }
                }

                Console.WriteLine("termina  timbrado de carpeta:" + carpetaTimbrar.NombreDirectorio);
                WriteLog("Termina  timbrado de carpeta:" + carpetaTimbrar.NombreDirectorio, "END");
            }
        }



        public string CreaArchivoCSV(Carpeta Datos)
        {
            string RutaArchivosXML = Datos.RutaDirectorio.TrimEnd('\\');

            //Se inicializa el objeto de la base de datos
            this.objDB = new ConexionBD(this.ConnectionString.Trim());

            string res = "";

            //this.ID_BLOQUE = this.txtIdBloque.Text.Trim() == "" ? this.ID_BLOQUE : this.txtIdBloque.Text.Trim();
            this.ID_BLOQUE = "001-201705";//este es el valor de un bloque que se asigna en la winform

            string hora = DateTime.Now.ToString("HH:mm:ss");
            hora = hora.Replace(":", "");
            string fecha = DateTime.Now.ToString("yyyyMMdd");
            //string NombreArchivoCSV = string.Format("{0}_{1}.csv", fecha, hora);
            //NombreArchivoCSV = Application.StartupPath + "\\" + NombreArchivoCSV;
            //NombreArchivoCSV = RutaArchivosXML.Trim() + "\\" + NombreArchivoCSV;
            string Q = "";
            int cont = 0;
            string auxArch = "";
           
           
            #region Encabezado para insert
            string Qenc = "INSERT INTO [dbo].[TIMBRADOS_NOMINA] ";
            Qenc += "([RFC_EMISOR]";
            Qenc += ",[NOMBRE_EMISOR]";
            Qenc += ",[RFC_RECEPTOR]";
            Qenc += ",[ID_RH]";
            Qenc += ",[NOMBRE]";
            Qenc += ",[PERIODICIDAD_PAGO]";
            Qenc += ",[FECHA_DE_PAGO]";
            Qenc += ",[NETO_A_PAGAR]";
            Qenc += ",[PERCEPCIONES]";
            Qenc += ",[DEDUCCIONES]";
            Qenc += ",[DEPARTAMENTO]";
            Qenc += ",[ARCHIVO]";
            Qenc += ",[VERSION]";
            Qenc += ",[UUID]";
            Qenc += ",[SUMATORIA_PERCEPCIONES]";
            Qenc += ",[SUMATORIA_DEDUCCIONES]";
            Qenc += ",[DIFERENCIA_PER-DED]";
            Qenc += ",[FH_REGISTRO]";
            Qenc += ",[ID_BLOQUE]";
            Qenc += ",[FECHATIMBRADO]";
            Qenc += ",[FECHAINICIALPAGO]";
            Qenc += ",[FECHAFINALPAGO]";
            Qenc += ",[ISR_INDEMNIZACION]";
            Qenc += ",[EXCESO_SUBSIDIO_EMPLEO]";
            Qenc += ")";
            Qenc += " VALUES ";
            #endregion

            //FileStream fs = null;
            //StreamWriter sw = null;

            try
            {
                string[] filters = new[] { "*@1000000000XX0.xml", "*tim.xml" };
                string[] archivos = filters.SelectMany(f => Directory.GetFiles(RutaArchivosXML, f)).ToArray();

                //string ArchivoBuscar = "*.xml";
                //string[] archivos = Directory.GetFiles(RutaArchivosXML, ArchivoBuscar);

               if (archivos.Length > 0)     
                   {
                    string Q1 = "INSERT INTO [dbo].[CONCILIACION] " + "([ConciliacionDescripcion]" + ",[Usuario]" + ",[FechaCreacion]" + ",[TotalConciliacion]" + ")" + " VALUES " + 
                   " (" + "'" + Datos.NombreDirectorio + "'" + ",'" + Datos.IDUsuario + "'" + ",getdate()" + ",'" + archivos.Count() + "')";
                    objDB.EjecUnaInstruccion("TRUNCATE TABLE [GA_Nomina].dbo.TIMBRADOS_NOMINA");
                    objDB.EjecUnaInstruccion(Q1);
                    foreach (string Archivo in archivos)
                    {

                        StringBuilder contenido = null;
                        //lo abrimos y lo cargamos en un string con UTF-8
                        contenido = new StringBuilder(File.ReadAllText(Archivo, Encoding.UTF8));

                        XmlDocument contenidoxml = new XmlDocument();
                        contenidoxml.LoadXml(contenido.ToString());
                        string AuxContenido = contenido.ToString();

                        string id_rh = "";
                        string fecha_pago = "";
                        string neto_a_pagar = "";
                        string percepciones = "";
                        string deducciones = "";

                        string isr_retener = "";
                        string subsidio_empleo = "";
                        string exceso_subsidio_empleo = "";
                        string isr_indemnizacion = "";

                        string departamento = "";
                        string rfcemisor = "";
                        string nombreemisor = "";
                        string rfc_receptor = "";
                        string nombre_receptor = "";
                        string PeriodicidadPago = "";
                        string uuid = "";

                        string fechatimbrado = "";
                        string fechainicialpago = "";
                        string fechafinalpago = "";

                        string version = "";
                        string versiontimbrado = "";

                        //auxArch = Archivo.Replace(Application.StartupPath + "\\", "");
                        auxArch = Archivo.Replace(RutaArchivosXML + "\\", "");

                        LimpiaConceptos();
                        try
                        {
                            try
                            {
                                versiontimbrado = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["version"].Value.ToString();
                            }
                            catch (Exception exversion)
                            {
                                versiontimbrado = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["Version"].Value.ToString();
                            }
                            if (versiontimbrado == "3.3")
                            {
                                neto_a_pagar = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["Total"].Value.ToString();
                                percepciones = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["SubTotal"].Value.ToString();
                                deducciones = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["Descuento"].Value.ToString();
                                fechatimbrado = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["Fecha"].Value.ToString();

                                rfcemisor = contenidoxml.GetElementsByTagName("cfdi:Emisor").Item(0).Attributes["Rfc"].Value.ToString();
                                nombreemisor = contenidoxml.GetElementsByTagName("cfdi:Emisor").Item(0).Attributes["Nombre"].Value.ToString();

                                rfc_receptor = contenidoxml.GetElementsByTagName("cfdi:Receptor").Item(0).Attributes["Rfc"].Value.ToString();
                                nombre_receptor = contenidoxml.GetElementsByTagName("cfdi:Receptor").Item(0).Attributes["Nombre"].Value.ToString();
                            }
                            else
                            {
                                neto_a_pagar = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["total"].Value.ToString();
                                percepciones = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["subTotal"].Value.ToString();
                                deducciones = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["descuento"].Value.ToString();
                                fechatimbrado = contenidoxml.GetElementsByTagName("cfdi:Comprobante").Item(0).Attributes["fecha"].Value.ToString();

                                rfcemisor = contenidoxml.GetElementsByTagName("cfdi:Emisor").Item(0).Attributes["rfc"].Value.ToString();
                                nombreemisor = contenidoxml.GetElementsByTagName("cfdi:Emisor").Item(0).Attributes["nombre"].Value.ToString();

                                rfc_receptor = contenidoxml.GetElementsByTagName("cfdi:Receptor").Item(0).Attributes["rfc"].Value.ToString();
                                nombre_receptor = contenidoxml.GetElementsByTagName("cfdi:Receptor").Item(0).Attributes["nombre"].Value.ToString();
                            }
                            try
                            { //en ocasiones el archivo no está timbrado
                                uuid = contenidoxml.GetElementsByTagName("tfd:TimbreFiscalDigital").Item(0).Attributes["UUID"].Value.ToString();
                            }
                            catch (Exception exSinTimb)
                            {
                                uuid = "";
                                //ESCRIBIR EN EL LOG (" Estructura XML no tiene UUID ")
                            }

                            if (AuxContenido.IndexOf("nomina12:Nomina") > 0)
                            {
                                version = "1.2";

                                id_rh = contenidoxml.GetElementsByTagName("nomina12:Receptor").Item(0).Attributes["NumEmpleado"].Value.ToString().Trim();
                                //si trae periodo se lo quitamos.
                                if (id_rh.IndexOf(" ") > 0)
                                    id_rh = id_rh.Substring(0, id_rh.IndexOf(" "));
                                departamento = contenidoxml.GetElementsByTagName("nomina12:Receptor").Item(0).Attributes["Departamento"].Value.ToString().Trim();
                                fecha_pago = contenidoxml.GetElementsByTagName("nomina12:Nomina").Item(0).Attributes["FechaPago"].Value.ToString();
                                fechainicialpago = contenidoxml.GetElementsByTagName("nomina12:Nomina").Item(0).Attributes["FechaInicialPago"].Value.ToString();
                                fechafinalpago = contenidoxml.GetElementsByTagName("nomina12:Nomina").Item(0).Attributes["FechaFinalPago"].Value.ToString();

                                PeriodicidadPago = contenidoxml.GetElementsByTagName("nomina12:Receptor").Item(0).Attributes["PeriodicidadPago"].Value.ToString();
                                if (AuxContenido.IndexOf("nomina12:SubsidioAlEmpleo") > 0)
                                    subsidio_empleo = contenidoxml.GetElementsByTagName("nomina12:SubsidioAlEmpleo").Item(0).Attributes["SubsidioCausado"].Value.ToString();
                            }
                            else
                            {
                                version = "1.1";
                                id_rh = contenidoxml.GetElementsByTagName("nomina:Nomina").Item(0).Attributes["NumEmpleado"].Value.ToString().Trim();
                                //si trae periodo se lo quitamos.
                                if (id_rh.IndexOf(" ") > 0)
                                    id_rh = id_rh.Substring(0, id_rh.IndexOf(" "));
                                departamento = contenidoxml.GetElementsByTagName("nomina:Nomina").Item(0).Attributes["Departamento"].Value.ToString().Trim();
                                fecha_pago = contenidoxml.GetElementsByTagName("nomina:Nomina").Item(0).Attributes["FechaPago"].Value.ToString();
                                fechainicialpago = contenidoxml.GetElementsByTagName("nomina:Nomina").Item(0).Attributes["FechaInicialPago"].Value.ToString();
                                fechafinalpago = contenidoxml.GetElementsByTagName("nomina:Nomina").Item(0).Attributes["FechaFinalPago"].Value.ToString();
                                PeriodicidadPago = contenidoxml.GetElementsByTagName("nomina:Nomina").Item(0).Attributes["PeriodicidadPago"].Value.ToString();
                            }

                            string AuxTagName = "";
                            string clave = "";

                            #region Sumarizando
                            //------------------SUMATORIA OTROS PAGOS---------------------------------
                            try
                            {

                                if (version.Trim() == "1.2")
                                    AuxTagName = "nomina12:OtroPago";
                                if (version.Trim() == "1.1")
                                    AuxTagName = "nomina:OtroPago";

                                XmlNodeList xmlotrospagos = contenidoxml.GetElementsByTagName(AuxTagName);
                                decimal sumaotrospagos = 0;
                                decimal acumuladootrospagos = 0;

                                foreach (XmlNode otropago in xmlotrospagos)
                                {
                                    string monto = "0";
                                    string montoexe = "0";

                                    clave = otropago.Attributes["Clave"].Value.ToString();
                                    clave = clave.Replace(",", "");
                                    clave += "|" + otropago.Attributes["Concepto"].Value.ToString();

                                    if (version == "1.2")
                                    {
                                        monto = otropago.Attributes["Importe"].Value.ToString();
                                        if (otropago.Attributes["Concepto"].Value.ToString() == "Exceso de Subsidio al empleo")
                                            exceso_subsidio_empleo = monto;
                                    }
                                    else
                                    {
                                        monto = otropago.Attributes["ImporteGravado"].Value.ToString();
                                        montoexe = otropago.Attributes["ImporteExento"].Value.ToString();
                                    }

                                    acumuladootrospagos += Convert.ToDecimal(monto) + Convert.ToDecimal(montoexe);
                                    sumaotrospagos = Convert.ToDecimal(monto) + Convert.ToDecimal(montoexe);

                                    if (!this.dicConceptosOP.ContainsKey(clave.Trim()))
                                        this.dicConceptosOP.Add(clave.Trim(), sumaotrospagos.ToString());
                                    else
                                        this.dicConceptosOP[clave.Trim()] = sumaotrospagos.ToString();
                                }
                            }
                            catch (Exception exotrospagos)
                            {
                                //ESCRIBIR EN EL LOG ("CreaArchivoCSV-otrospagos")
                            }

                            //-------------------SUMATORIA DE PERCEPCIONES--------------------------------
                            if (version.Trim() == "1.2")
                                AuxTagName = "nomina12:Percepcion";
                            if (version.Trim() == "1.1")
                                AuxTagName = "nomina:Percepcion";

                            XmlNodeList xmlpercepciones = contenidoxml.GetElementsByTagName(AuxTagName);
                            decimal sumapercepciones = 0;
                            decimal acumuladopercepciones = 0;

                            foreach (XmlNode perc in xmlpercepciones)
                            {
                                string cantidad = perc.Attributes["ImporteGravado"].Value.ToString();
                                string cantexe = perc.Attributes["ImporteExento"].Value.ToString();
                                acumuladopercepciones += Convert.ToDecimal(cantidad) + Convert.ToDecimal(cantexe);
                                sumapercepciones = Convert.ToDecimal(cantidad) + Convert.ToDecimal(cantexe);

                                clave = perc.Attributes["Clave"].Value.ToString();
                                clave = clave.Replace(",", "");
                                clave += "|" + perc.Attributes["Concepto"].Value.ToString();

                                if (!this.dicConceptos.ContainsKey(clave.Trim()))
                                    this.dicConceptos.Add(clave.Trim(), sumapercepciones.ToString());
                                else
                                    this.dicConceptos[clave.Trim()] = sumapercepciones.ToString();
                            }

                            //-----------------------SUMATORIA DEDUCCIONES-----------------------------------
                            if (version.Trim() == "1.2")
                                AuxTagName = "nomina12:Deduccion";
                            if (version.Trim() == "1.1")
                                AuxTagName = "nomina:Deduccion";

                            XmlNodeList xmldeducciones = contenidoxml.GetElementsByTagName(AuxTagName);
                            decimal sumadeducciones = 0;
                            decimal acumuladodeducciones = 0;

                            foreach (XmlNode dedu in xmldeducciones)
                            {
                                string monto = "0";
                                string montoexe = "0";

                                clave = dedu.Attributes["Clave"].Value.ToString();
                                clave = clave.Replace(",", "");
                                clave += "|" + dedu.Attributes["Concepto"].Value.ToString();

                                if (version == "1.2")
                                {
                                    monto = dedu.Attributes["Importe"].Value.ToString();
                                    if (dedu.Attributes["Concepto"].Value.ToString() == "ISR a retener")
                                        isr_retener = monto;
                                    if (dedu.Attributes["Concepto"].Value.ToString() == "ISR Indemnización")
                                        isr_indemnizacion = monto;
                                }
                                else
                                {
                                    monto = dedu.Attributes["ImporteGravado"].Value.ToString();
                                    montoexe = dedu.Attributes["ImporteExento"].Value.ToString();
                                    if (dedu.Attributes["Concepto"].Value.ToString() == "ISR")
                                        isr_retener = monto;
                                }

                                acumuladodeducciones += Convert.ToDecimal(monto) + Convert.ToDecimal(montoexe);
                                sumadeducciones = Convert.ToDecimal(monto) + Convert.ToDecimal(montoexe);

                                if (!this.dicConceptosDed.ContainsKey(clave.Trim()))
                                    this.dicConceptosDed.Add(clave.Trim(), sumadeducciones.ToString());
                                else
                                    this.dicConceptosDed[clave.Trim()] = sumadeducciones.ToString();
                            }

                            decimal diferencia = acumuladopercepciones - acumuladodeducciones;

                            string sumperaux = acumuladopercepciones.ToString("c").Replace("$", "");
                            sumperaux = sumperaux.Replace(",", "");

                            string sumdeduaux = acumuladodeducciones.ToString("c").Replace("$", "");
                            sumdeduaux = sumdeduaux.Replace(",", "");

                            string difaux = diferencia.ToString("c").Replace("$", "");
                            difaux = difaux.Replace(",", "");

                            nombreemisor = nombreemisor.Replace(",", "");
                            nombre_receptor = nombre_receptor.Replace(",", "");

                            #endregion
                            //string auxArch = Archivo.Replace(Application.StartupPath + "\\", "");

                            string linea = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23}", rfcemisor, nombreemisor, rfc_receptor, id_rh, nombre_receptor, PeriodicidadPago, fecha_pago, neto_a_pagar, percepciones, deducciones, isr_retener, isr_indemnizacion, subsidio_empleo, exceso_subsidio_empleo, departamento, auxArch, versiontimbrado, uuid, sumperaux, sumdeduaux, difaux, fechatimbrado, fechainicialpago, fechafinalpago);

                            #region Registro en BD
                            //if (this.InsertarEnBD.ToUpper().Trim() == "SI")
                            if (true)//SE VALIDABA EL PASWORD DE BONET AL PARECER SOLO EL PODIA USARLO
                            {
                                Q = Qenc;
                                Q += " (";
                                //string valores = string.Format("'{0}','{1}','{2}','{3}','{4}',{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16}", rfcemisor, nombreemisor, rfc_receptor, id_rh, nombre_receptor, PeriodicidadPago, fecha_pago, neto_a_pagar, percepciones, deducciones, departamento, auxArch,version, uuid ,sumperaux, sumdeduaux, difaux);
                                Q += "'" + rfcemisor.Trim() + "'"; //<RFC_EMISOR, nvarchar(20),>
                                Q += ",'" + nombreemisor.Trim() + "'"; //,<NOMBRE_EMISOR, nvarchar(700),>
                                Q += ",'" + rfc_receptor.Trim() + "'";             //,<RFC_RECEPTOR, nvarchar(20),>
                                Q += ",'" + id_rh.Trim() + "'"; //,<ID_RH, nvarchar(10),>
                                Q += ",'" + nombre_receptor.Trim() + "'"; //,<NOMBRE, nvarchar(500),>
                                Q += ",'" + PeriodicidadPago.Trim() + "'"; //,<PERIODICIDAD_PAGO, nvarchar(50),>
                                Q += ",'" + fecha_pago.Trim() + "'"; //,<FECHA_DE_PAGO, nvarchar(20),>
                                Q += "," + neto_a_pagar.Trim() + ""; //,<NETO_A_PAGAR, numeric(10,2),>
                                Q += "," + percepciones.Trim() + ""; //,<PERCEPCIONES, numeric(10,2),>
                                Q += "," + deducciones.Trim() + ""; //,<DEDUCCIONES, numeric(10,2),>
                                Q += ",'" + departamento.Trim() + "'"; //,<DEPARTAMENTO, nvarchar(500),>
                                Q += ",'" + auxArch.Trim() + "'"; //,<ARCHIVO, nvarchar(300),>
                                Q += ",'" + version.Trim() + "'"; //,<VERSION, nvarchar(10),>
                                Q += ",'" + uuid.Trim() + "'"; //,<UUID, nvarchar(80),>
                                Q += "," + sumapercepciones.ToString().Trim() + ""; //,<SUMATORIA_PERCEPCIONES, numeric(10,2),>
                                Q += "," + sumadeducciones.ToString().Trim() + ""; //,<SUMATORIA_DEDUCCIONES, numeric(10,2),>
                                Q += "," + diferencia.ToString().Trim() + ""; //,<DIFERENCIA_PER-DED, numeric(10,2),>
                                Q += ",getdate()";  //,<FH_REGISTRO, datetime,>)
                                Q += ",'" + this.ID_BLOQUE.Trim() + "'";
                                Q += ",'" + fechatimbrado.Trim() + "'";
                                Q += ",'" + fechainicialpago.Trim() + "'";
                                Q += ",'" + fechafinalpago.Trim() + "'";
                                Q += ",'" + (isr_indemnizacion != "" ? isr_indemnizacion.Trim() : "0") + "'";
                                Q += ",'" + (exceso_subsidio_empleo!= ""  ? exceso_subsidio_empleo.Trim() : "0") + "')";

                                string Qexiste = "Select Isnull(count(*),0) from [TIMBRADOS_NOMINA] where UUID='" + uuid.Trim() + "'";
                                if (this.objDB.ConsultaUnSoloCampo(Qexiste.Trim()) == "0")
                                {
                                    //Console.WriteLine(uuid.Trim());//Este console lo tengo que quitar por que escribiria mucho en consola
                                    this.objDB.EjecUnaInstruccion(Q);
                                }
                                else
                                {
                                    Q = "Update TIMBRADOS_NOMINA set FECHATIMBRADO='" + fechatimbrado.Trim() + "', FECHAINICIALPAGO='" + fechainicialpago.Trim() + "', FECHAFINALPAGO='" + fechafinalpago.Trim() + "'";
                                    Q += " where UUID='" + uuid.Trim() + "'";
                                    this.objDB.EjecUnaInstruccion(Q);
                                    //Utilerias.WriteToLog("Ya existe UUID:" + uuid + " nombre Archivo: " + auxArch + " se actualiza", "CreaArchivoCSV", Application.StartupPath + "\\Log.txt");
                                }
                            }
                            #endregion

                            //#region REgistro en CSV
                            //if (vez == 2)
                            //{
                            //    if (File.Exists(NombreArchivoCSV))
                            //    {   //ya existe vaciamos la linea en el
                            //        try
                            //        {
                            //            if (FileReadyToRead(NombreArchivoCSV, 5))
                            //            {
                            //                fs = new FileStream(NombreArchivoCSV, FileMode.Append, FileAccess.Write, FileShare.None);
                            //                sw = new StreamWriter(fs, Encoding.ASCII);
                            //                linea += AgregaValoresALinea();
                            //                sw.WriteLine(linea);
                            //                cont++;
                            //                sw.Close();
                            //                fs.Close();
                            //            }
                            //        }
                            //        catch (Exception ex1)
                            //        {
                            //            //DEBE ESCRIBIR EN EL LOG (CreaArchivo_ex1)
                            //            System.Threading.Thread.Sleep(60000);
                            //            fs = new FileStream(NombreArchivoCSV, FileMode.Append, FileAccess.Write, FileShare.None);
                            //            sw = new StreamWriter(fs, Encoding.ASCII);
                            //            linea += AgregaValoresALinea();
                            //            sw.WriteLine(linea);
                            //            cont++;
                            //            sw.Close();
                            //            fs.Close();
                            //        }
                            //        finally
                            //        {
                            //            LimpiaConceptos();
                            //            sw.Close();
                            //            fs.Close();
                            //        }
                            //    }
                            //    else
                            //    {//El archivo no existe por lo tanto lo creamos y escribimos en el 
                            //        res = NombreArchivoCSV;
                            //        string Encabezado = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23}", "RFC EMISOR", "NOMBRE EMISOR", "RFC RECEPTOR", "ID RH", " NOMBRE ", "PERIODICIDAD PAGO", "FECHA DE PAGO", "NETO A PAGAR", "PERCEPCIONES", "DEDUCCIONES", "ISR_RETENER", "ISR_INDEMNIZACION", "SUBSIDIO AL EMPLEO", "EXCESO SUBSIDIO AL EMPLEO", "DEPARTAMENTO", "Archivo", "version", "UUID", "SUMATORIA PERCEPCIONES", "SUMATORIA DEDUCCIONES", "DIFERENCIA PER-DED", "FECHA TIMBRADO", "FECHA INICIAL PAGO", "FECHA FINAL PAGO");
                            //        Encabezado += AgregaAEncabezado();
                            //        fs = new FileStream(NombreArchivoCSV, FileMode.Create, FileAccess.Write, FileShare.None);
                            //        sw = new StreamWriter(fs, Encoding.ASCII);
                            //        sw.WriteLine(Encabezado);
                            //        linea += AgregaValoresALinea();
                            //        sw.WriteLine(linea);
                            //        cont++;
                            //        sw.Close();
                            //        fs.Close();
                            //    }
                            //}
                            //#endregion

                        }
                        catch (Exception ex2)
                        {
                            //DEBE ESCRIBIR EN EL LOG(" Estructura XML desconocida ")
                        }


                    }
                }
                foreach (FileInfo file in new DirectoryInfo(RutaArchivosXML).GetFiles())
                {
                    if (File.Exists(ProcessedFiles + file.Name))
                    { File.Delete(ProcessedFiles + file.Name); }
                    File.Move(file.FullName, ProcessedFiles + file.Name);
                }
            }
            catch (Exception ex)
            {
            }
            finally
            {
            }
            return res;
        }

        private void LimpiaConceptos()
        {
            //Limpiamos las percepciones
            for (int index = 0; index < this.dicConceptos.Count; index++)
            {
                var item = this.dicConceptos.ElementAt(index);
                var itemKey = item.Key;
                this.dicConceptos[itemKey] = "0";
            }
            //Limpiamos las deducciones.
            for (int index = 0; index < this.dicConceptosDed.Count; index++)
            {
                var item = this.dicConceptosDed.ElementAt(index);
                var itemKey = item.Key;
                this.dicConceptosDed[itemKey] = "0";
            }
            //limpiamos otros pagos.
            for (int index = 0; index < this.dicConceptosOP.Count; index++)
            {
                var item = this.dicConceptosOP.ElementAt(index);
                var itemKey = item.Key;
                this.dicConceptosOP[itemKey] = "0";
            }
        }

        public void StartCancellation(string data)
        {
            Documento documento = new Documento();
            Carpeta carpeta = new Carpeta();
            string nombreUsuario = string.Empty;
            documento.CodigoResultado = "0";

            //Se obtienen los parametros del objeto json
            Carpeta carpetaTimbrar = new Carpeta();
            dynamic obj = JsonConvert.DeserializeObject(data);

            carpeta.RutaDirectorio = obj.path;
            carpeta.IDEmpresa = obj.idEmpresa;
            carpeta.IDTipo = obj.idTipo;
            carpeta.IDUsuario = obj.idUsuario;
            carpeta.NombreDirectorio = obj.nombreCarpeta;
            carpeta.NoDocumentos = "0";

            Configuracion configuracionBD = new Configuracion();
            nombreUsuario = ConsultaUsuarioCancela(configuracionBD, carpeta.IDUsuario);
            WriteLog("El Usuario: " + nombreUsuario + " Envío a cancelar los siguientes archivos.");
            //originPath = "C:\TimbradoNominaParaRH\";
            //originPath = carpeta.RutaDirectorio + carpeta.NombreDirectorio;//asignamos la ruta que viene del front a la aplicacion de consola
            originPath = carpeta.RutaDirectorio + "\\";
            string[] strArray = originPath.Split('\\');
            string Destino = strArray[3] + "\\" + strArray[4] + "\\" + strArray[5] + "\\";

            if (Directory.Exists(originPath))
            {
                DirectoryInfo di = new DirectoryInfo(originPath);
                foreach (var fi in di.GetFiles())
                {
                    WriteLog(fi.ToString());
                }
                WriteLog("Inicia el proceso de cancelación");
                while (!IsDirectoryEmpty(originPath))
                {
                    //Se realiza el proceso de cancelacion hasta que se termine con todas las facturas timbradas de la carpeta origen.
                    try
                    {

                        Configuracion configuracion = new Configuracion();
                        //carpeta.IDUsuario = "4";
                        //carpeta.RutaDirectorio = originPath;
                        string file = Directory.GetFileSystemEntries(originPath, "*.xml").Select(f => new FileInfo(f).Name).FirstOrDefault();
                        documento.NombreXml = Path.GetFileNameWithoutExtension(originPath + file);
                        Console.Write("Comienza el procesamiento del archivo: {0} |\t", file);
                        WriteLog(string.Format("Comienza el procesamiento del archivo: {0}", file));
                        //originPath = originPath + "\\";
                        carpeta.IDEmpresa = ObtainCompanyId(originPath + file, configuracion).ToString();
                        SetConfigurationDataBase(configuracion, carpeta.IDEmpresa);
                        XmlDocument document = CreateCancellationXml(originPath, file, documento, Destino);
                        byte[] pfxBlob = File.ReadAllBytes(configuracion.CSD);
                        string pfxPassword = configuracion.CSDPASSWORD;
                        XmlElement signatureElement = GenerateXmlSignature(document, pfxBlob, pfxPassword);
                        document.DocumentElement.AppendChild(document.ImportNode(signatureElement, true));
                        AgregateTestAttribute(document, file, Destino);
                        documento.CodigoResultado = SendCancellation(cancelOriginPath + Destino, file).ToString();

                        if (documento.CodigoResultado == "201")
                        {
                            Console.WriteLine("Cancelado");
                            WriteLog("Archivo cancelado correctamente");
                            WriteLog(String.Format("DOC: {0} | CODE: {1}", documento.NombreXml, documento.CodigoResultado));
                            if (!Directory.Exists(cancelledFiles + Destino))
                            { CreateDirectory(cancelledFiles + Destino); }
                                
                            MoveFileToCorrectlyProcessed(cancelOriginPath + acusseFileName, cancelledFiles + Destino + acusseFileName);
                            //MoveFileToCorrectlyProcessed(cancelOriginPath + acusseFileName, cancelledFiles + acusseFileName);
                        }
                        else
                        {
                            Console.WriteLine("No Cancelado");
                            WriteLog("Archivo No Cancelado.");
                            WriteLog(String.Format("DOC: {0} | CODE: {1}", documento.NombreXml, documento.CodigoResultado));
                            //MoveFileToIncorrectlyProcessed(cancelOriginPath + file, nonCancelledFiles + file);

                           
                            WriteLogCancel("Archivo No Cancelado.");
                            WriteLogCancel(String.Format("DOC: {0} | CODE: {1}", documento.NombreXml, documento.CodigoResultado));
                            if (!Directory.Exists(nonCancelledFiles + Destino))
                            { CreateDirectory(nonCancelledFiles + Destino); }                               
                            MoveFileToIncorrectlyProcessed(cancelOriginPath + file, nonCancelledFiles + Destino + file);

                        }

                        ///////////////////Estos pasos son los que se usaban antes para el sellado///////////////
                        //string cancellationFile = Directory.GetFileSystemEntries(cancelOriginPath, "*.xml").Select(f => new FileInfo(f).Name).FirstOrDefault();
                        //CreateLayoutString(cancelOriginPath, cancellationFile);
                        //SignLayoutString(cancelOriginPath, cancellationFile, configuracion);
                        //StampFile(cancelOriginPath, cancellationFile, configuracion);
                        /////////////////////////////////////////////////////////////////////////////////////////

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("No Cancelado | ERROR");
                        //MoveFileToCorrectlyProcessed(correctlyProcessedFiles + documento.NombreXml + ".xml", errorPathFiles + documento.NombreXml + ".xml");
                        WriteLog(string.Format("DOC:{0}|DES:{1}", documento.NombreXml, ex.Message), "ERR");

                        if (!Directory.Exists(nonCancelledFiles + Destino))
                        { CreateDirectory(nonCancelledFiles + Destino); }
                           
                        MoveFileToIncorrectlyProcessed(cancelOriginPath + Destino + documento.NombreXml + ".xml", nonCancelledFiles + Destino + documento.NombreXml + ".xml");
                        WriteLogCancel(string.Format("DOC:{0}|DES:{1}", documento.NombreXml, ex.Message), "ERR");

                    }
                    finally
                    {
                        WriteLog("DOC: " + documento.NombreXml, "FIN");
                        InsertProcessedFileCancel(carpeta, documento);
                    }

                }
                Console.WriteLine("Terminaron las cancelaciones...");
            }
        }

        private void InsertProcessedFileCancel(Carpeta carpeta, Documento documento)
        {
            SqlParameter[] parameterList = {
                new SqlParameter("@ClaveEmpresa", carpeta.IDEmpresa),
                new SqlParameter("@Usuario", carpeta.IDUsuario),
                new SqlParameter("@CarpetaOrigen", carpeta.RutaDirectorio),
                new SqlParameter("@NombreRecibo", documento.NombreXml),
                new SqlParameter("@UUID", documento.UUID),
                new SqlParameter("@IdEstatus", documento.CodigoResultado)
            };

            SQLServer BaseNomina = new SQLServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();

            BaseNomina.ExecuteQueryProcedure("INS_RECIBOS_CANCELADOS_SP", parameterList);
        }

        private void MoveFileToIncorrectlyProcessed(string origin, string destiny)
        {
            //Funcion para mover los archivos timbrados que no se pudieron procesar correctamente
            try
            {
                if (File.Exists(destiny))
                {
                    string tempName = "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".tmp";
                    File.Move(destiny, destiny + tempName);
                }
                File.Move(origin, destiny);
            }
            catch (Exception)
            {
                WriteLog("No se pudo mover el archivo a la carpeta de no creados.", "ERROR");
            }
        }

        private void MoveFileToCorrectlyProcessed(string origin, string destiny)
        {
            //Funcion para mover los archivos timbrados que ya fueron procesados correctamente
            try
            {
                if (File.Exists(destiny))
                {
                    string tempName = "_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".tmp";
                    File.Move(destiny, destiny + tempName);
                }
                File.Move(origin, destiny);
                WriteLog("Se movió el archivo a la carpeta destino correctamente.");
            }
            catch (Exception ex)
            {
                WriteLog("No se pudo mover el archivo a la carpeta de creados correctamente.", "ERROR");
            }
        }

        private int SendCancellation(string path, string file)
        {
            int resultCode = 0;
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(originPath + file);

                string acusseString = CreateXMLRequest(path, file);

                XmlDocument responseXml = new XmlDocument();

                responseXml.LoadXml(acusseString);
                XmlNamespaceManager nsMgr = new XmlNamespaceManager(responseXml.NameTable);
                nsMgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
                nsMgr.AddNamespace("ns1", "http://edifact.com.mx/xsd");


                string acusseNode = responseXml.SelectSingleNode("/soap:Envelope/soap:Body/ns1:enviaAcuseCancelacionResponse/ns1:return", nsMgr).InnerText;

                XmlDocument acusseXml = new XmlDocument();
                acusseXml.LoadXml(acusseNode);
                XmlNamespaceManager namespaceManager = new XmlNamespaceManager(acusseXml.NameTable);
                namespaceManager.AddNamespace("cancel", "http://cancelacfd.sat.gob.mx");

                XmlNode codeAcusse = acusseXml.SelectSingleNode("/Acuse/Folios/EstatusUUID", namespaceManager);

                if (codeAcusse != null)
                {
                    resultCode = Int32.Parse(codeAcusse.InnerText);
                    acusseFileName = fileName + "Cancelado.xml";
                    acusseXml.Save(cancelOriginPath + acusseFileName);
                }
                else
                {
                    resultCode = Int32.Parse(acusseXml.SelectSingleNode("/Acuse/@CodEstatus", namespaceManager).Value);
                }

            }
            catch (Exception ex)
            {
                WriteLog("Ocurrio un error al cancelar el CFDI", "ERROR");
                WriteLogCancel("Ocurrio un error al cancelar el CFDI", "ERROR");
                throw ex;
            }
            return resultCode;
        }

        private string CreateXMLRequest(string path, string file)
        {
            string fileName = Path.GetFileNameWithoutExtension(originPath + file);
            String xml = File.ReadAllText(path + fileName + "Sellado.xml");
            //String xml = "<?xml version=\"1.0\" encoding=\"UTF - 8\"?><Cancelacion xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" Fecha=\"2017-12-28T12:12:39\" RfcEmisor=\"TRA030228EP9\" xmlns=\"http://cancelacfd.sat.gob.mx\"><Folios><UUID>1BD4A237-CE07-41CD-AC96-873F82BA95D0</UUID></Folios><Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\"><SignedInfo><CanonicalizationMethod Algorithm=\"http://www.w3.org/TR/2001/REC-xml-c14n-20010315\" /><SignatureMethod Algorithm=\"http://www.w3.org/2000/09/xmldsig#rsa-sha1\" /><Reference URI=\"\"><Transforms><Transform Algorithm=\"http://www.w3.org/2000/09/xmldsig#enveloped-signature\" /></Transforms><DigestMethod Algorithm=\"http://www.w3.org/2000/09/xmldsig#sha1\" /><DigestValue>oBMrHJbGgd2FAuKDggq2bH50d1g=</DigestValue></Reference></SignedInfo><SignatureValue>R9VOA4I3CBbBW19db9jirZW0JT9VvwcF8rdZ+7CtrqMFtPKXGuvy6PAe4xzGEMymQ6FgxnTx70PyHcYdbx/bLSdgjOw+uqAShg3T5wBzRHj76b8XOOyFIwwk61SdHXvwEE+QLRPFqhmQ9/tSrSeOOu82m2uB+ZZN/XJYNucJEC7ymRwgzDxJHXIuy0c4hgNz740YItcPj2/K0gN5QNfoB1lS6tzKc3ZlZqGlUfLqrzmtGZI7JwI2bZqj2NomeocswJPXaSYUCcuK2FZj8Wew1av9WDzII3s8Uy4s5eoxvCQDl/AmZZbMz0DAvgw1mOD9O5rVzamFj//6EdWpvhmJ7A==</SignatureValue><KeyInfo><X509Data><X509IssuerSerial><X509IssuerName>OID.1.2.840.113549.1.9.2=Responsable: Administración Central de Servicios Tributarios al Contribuyente, OID.2.5.4.45=SAT970701NN3, L=Cuauhtémoc, S=Distrito Federal, C=MX, PostalCode=06300, STREET=\"Av. Hidalgo 77, Col. Guerrero\", E=acods@sat.gob.mx, OU=Administración de Seguridad de la Información, O=Servicio de Administración Tributaria, CN=A.C. del Servicio de Administración Tributaria</X509IssuerName><X509SerialNumber>275106190557734483187066766810933418863139173940</X509SerialNumber></X509IssuerSerial><X509Certificate>MIIGEjCCA/qgAwIBAgIUMDAwMDEwMDAwMDA0MDQwNjM3NjQwDQYJKoZIhvcNAQELBQAwggGyMTgwNgYDVQQDDC9BLkMuIGRlbCBTZXJ2aWNpbyBkZSBBZG1pbmlzdHJhY2nDs24gVHJpYnV0YXJpYTEvMC0GA1UECgwmU2VydmljaW8gZGUgQWRtaW5pc3RyYWNpw7NuIFRyaWJ1dGFyaWExODA2BgNVBAsML0FkbWluaXN0cmFjacOzbiBkZSBTZWd1cmlkYWQgZGUgbGEgSW5mb3JtYWNpw7NuMR8wHQYJKoZIhvcNAQkBFhBhY29kc0BzYXQuZ29iLm14MSYwJAYDVQQJDB1Bdi4gSGlkYWxnbyA3NywgQ29sLiBHdWVycmVybzEOMAwGA1UEEQwFMDYzMDAxCzAJBgNVBAYTAk1YMRkwFwYDVQQIDBBEaXN0cml0byBGZWRlcmFsMRQwEgYDVQQHDAtDdWF1aHTDqW1vYzEVMBMGA1UELRMMU0FUOTcwNzAxTk4zMV0wWwYJKoZIhvcNAQkCDE5SZXNwb25zYWJsZTogQWRtaW5pc3RyYWNpw7NuIENlbnRyYWwgZGUgU2VydmljaW9zIFRyaWJ1dGFyaW9zIGFsIENvbnRyaWJ1eWVudGUwHhcNMTYxMDI1MjMxMzMxWhcNMjAxMDI1MjMxMzMxWjCBsjEbMBkGA1UEAxMSVFJBU0lOTUVYIFNBIERFIENWMRswGQYDVQQpExJUUkFTSU5NRVggU0EgREUgQ1YxGzAZBgNVBAoTElRSQVNJTk1FWCBTQSBERSBDVjElMCMGA1UELRMcVFJBMDMwMjI4RVA5IC8gTUFHQTc4MTAyMEpENDEeMBwGA1UEBRMVIC8gTUFHQTc4MTAyME1ERlJSTDAyMRIwEAYDVQQLEwlUUkFTSU5NRVgwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCUmpaHljsHG7DWX8cKuvpaTjjxHtyKilZ73w5uzy6tCqaPQrOSkjjLvUqQwzI1flZa2j2RE5dQjFfQNLFt2pqzz/k/wBrdO3AlRpJBWY4ik1hHDq9mKoxmFCplGiamCs+sPOnJ5FRk+rLdyNham/MuyO3BEYn63JU8PJXinYqRtTThcisyBaCDhAdsUjpWKPGCYiLwex24hPkLIZ+Zo1wN901m9DOjCTa4u4gaGcKi3NCuUR1g3fZyk0oYrKLtMYO1XFKOpPs2rO843ZhO1VJKnwwxVn+gtaO+KR5auJi2HIx3JsvjqCw1CU9dL2uDj/qtimAoJV2HW3/FIUdkDIP7AgMBAAGjHTAbMAwGA1UdEwEB/wQCMAAwCwYDVR0PBAQDAgbAMA0GCSqGSIb3DQEBCwUAA4ICAQADq9P/Fex2WLx1sKRmiXjM2d9dY4c8QSDj32FBenGcbEgV+N3LiCdD9USFZNPmCyhA78+nklm3pb++3zzQa3zNr6Jsn7DS3hQXcdYBnKg8J+I/obUKl4ttuJDQM1rVNGJ4/rsGawFkbIsifHqGQg9vsZ/bQxI5ijeQKOPeDLIiM0KaSGM9doWX0hmqqHerJN9HLuKT8/8u1Cb6dBQxqP0ZN76KqRkK5sr7BW52vLWTOyBJXe239qZli089YXFPqSk109b944Hf3EIYg4Q51dFJttrRCtjVZYIiuzqFp2XRemiSut19uNDy06GeEZ9MKFNgq7Ex/+a1nMZJ4Hwyvsh9QePyceSJYyVXVam0F6MOwL2TiodhlNqKa8pSTsDfzze3HWu6jZBiE8z8Dd3PpfUoDB/Z9Gk5n2lz7m7vsQWEz2zsJ3y4DkjRle8gnJVX3N1LrHIjcgMOF6E7HPKZAnEliXCEf0+fa7abcsWCOtc75ewHe4LI54vVUKt6SXf0W8GcA2BLUsFpzTOjWxyPJeFQ6A2oIjKYk8J+TJyAlXPLtfAc0xFE4+iVW8NYemSr84thGzUNuOiWhLnt0sODPcqqj7MEaNzQcCb7TUZxM9ivpa+W56w83s2eMJzTfjKFbg2zcAa54eH6oFsAW+CsrxdDk1ztPPCKLkxnwI6F56ebgg==</X509Certificate></X509Data></KeyInfo></Signature></Cancelacion>";
            xml = xml.Replace("<", "&lt;");
            xml = xml.Replace(">", "&gt;");
            xml = xml.Replace("\n", "").Replace("\r", "");
            xml = xml.Replace("  ", "");

            String empaquetado = "<?xml version= \"1.0\" encoding=\"UTF-8\"?>" +
                "<SOAP-ENV:Envelope SOAP-ENV:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\" xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:SOAP-ENC=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                "<SOAP-ENV:Body>" +
                "<ns2:enviaAcuseCancelacion xmlns=\"http://dom.w3c.org/xsd\" xmlns:ns2=\"http://edifact.com.mx/xsd\">" +
                "<ns2:mensajeXml>" + xml + "</ns2:mensajeXml>" +
                "</ns2:enviaAcuseCancelacion>" +
                "</SOAP-ENV:Body></SOAP-ENV:Envelope>";

            //String urlService = "http://comprobantes-fiscales.com/service/cancelarCFDI.php?wsdl"; //URL PRUEBAS
            //String urlAction = "http://comprobantes-fiscales.com/service/cancelarCFDI.php?/enviaAcuseCancelacion"; //URL PRUEBAS
            String urlService = "https://www.edifactmx-pac.com/service/cancelarCFDI.php?wsdl";
            String urlAction = "https://www.edifactmx-pac.com/service/cancelarCFDI.php?wsdl/enviaAcuseCancelacion";


            return ATask(urlService, urlAction, empaquetado);
        }

        private string ATask(string uri, string soapOperation, string xmlRequestText)
        {
            string xmlResponseText = "";
            try
            {
                HttpWebRequest httpRequest = (HttpWebRequest)WebRequest.Create(uri);
                httpRequest.Headers.Add("SOAPAction", soapOperation);
                httpRequest.ContentType = "text/xml; charset=utf-8";
                httpRequest.Method = "POST";
                XmlDocument xmlRequestDocument = new XmlDocument();
                xmlRequestDocument.LoadXml(xmlRequestText);
                using (Stream stream = httpRequest.GetRequestStream())
                {
                    xmlRequestDocument.Save(stream);
                }
                using (WebResponse response = httpRequest.GetResponse())
                {
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        xmlResponseText = reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Ocurrio un error al enviar el REQUEST", "ERROR");
                throw ex;
            }

            return xmlResponseText;
        }

        private void AgregateTestAttribute(XmlDocument document, string file, string Destino)
        {
            try
            {
                XmlNamespaceManager nsMgr = new XmlNamespaceManager(document.NameTable);
                nsMgr.AddNamespace("cancel", "http://cancelacfd.sat.gob.mx");
                nsMgr.AddNamespace("xmldsig", "http://www.w3.org/2000/09/xmldsig#");

                //XmlNode node = document.SelectSingleNode("/cancel:Cancelacion/xmldsig:Signature/xmldsig:KeyInfo/xmldsig:X509Data/xmldsig:X509IssuerSerial/xmldsig:X509IssuerName", nsMgr);
                //string innerTextNode = node.InnerText;
                //string X509IssuerNameTesting = innerTextNode + ", CN=A.C. de pruebas";
                //node.InnerText = X509IssuerNameTesting;

                string fileName = Path.GetFileNameWithoutExtension(originPath + file);
                document.Save(cancelOriginPath + Destino + fileName + "Sellado.xml");

            }
            catch (Exception)
            {
                WriteLog("No se pudo modificar el atributo del sello para pruebas", "ERROR");
                WriteLogCancel("No se pudo modificar el atributo del sello para pruebas", "ERROR");
            }
        }

        private XmlElement GenerateXmlSignature(XmlDocument originalXmlDocument, byte[] pfx, string pfxPassword)
        {

            try
            {
                X509Certificate2 cert = new X509Certificate2(pfx, pfxPassword);
                RSACryptoServiceProvider key = cert.PrivateKey as RSACryptoServiceProvider;
                SignedXml signedXml = new SignedXml(originalXmlDocument) { SigningKey = key };
                Reference reference = new Reference() { Uri = String.Empty };
                XmlDsigEnvelopedSignatureTransform env = new XmlDsigEnvelopedSignatureTransform();
                reference.AddTransform(env);
                KeyInfoX509Data kdata = new KeyInfoX509Data(cert);
                kdata.AddIssuerSerial(cert.Issuer, cert.SerialNumber);
                KeyInfo keyInfo = new KeyInfo();
                keyInfo.AddClause(kdata);
                signedXml.KeyInfo = keyInfo;
                signedXml.AddReference(reference);
                signedXml.ComputeSignature();
                WriteLog("Se agrego correctamente el sello al archivo de cancelacion");
                return signedXml.GetXml();
            }
            catch (Exception)
            {
                WriteLog("Ocurrio un error al agregar el sello al archivo de cancelacion", "ERROR");
                WriteLogCancel("Ocurrio un error al agregar el sello al archivo de cancelacion", "ERROR");
                return new SignedXml().GetXml();
            }

        }

        private XmlDocument CreateCancellationXml(string xmlPath, string file, Documento documento, string Destino)
        {
            //Funcion para crear el xml de cancelacion a partir de los valores de la factura timbrada
            string[] data = ObtainXMLData(xmlPath + file);
            string uuid = data[0];
            string rfcEmisor = data[1];
            documento.UUID = uuid;
            XmlDocument cancellationXml = new XmlDocument() { PreserveWhitespace = false };

            try
            {
                XNamespace satCancelacionNamespace = "http://cancelacfd.sat.gob.mx";
                var xmlSolicitud = new XElement(satCancelacionNamespace + "Cancelacion",
                                                new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                                                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                                                new XAttribute("Fecha", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),
                                                new XAttribute("RfcEmisor", rfcEmisor),
                                                new XElement(satCancelacionNamespace + "Folios",
                                                new XElement(satCancelacionNamespace + "UUID", uuid.ToString().ToUpper()))
                                                );

                XmlNode docNode = cancellationXml.CreateXmlDeclaration("1.0", "UTF-8", null);
                cancellationXml.LoadXml(xmlSolicitud.ToString());
                cancellationXml.InsertBefore(docNode, cancellationXml.DocumentElement);

                if (!Directory.Exists(cancelOriginPath + Destino))
                { CreateDirectory(cancelOriginPath + Destino); }
                    
                cancellationXml.Save(cancelOriginPath + Destino + file);
                MoveFileToCorrectlyProcessed(xmlPath + file, cancelOriginPath + Destino + file);
                //MoveFileToCorrectlyProcessed(xmlPath + file, correctlyProcessedFiles + file);
                WriteLog(string.Format("Se creo correctamente el xml de cancelacion del archivo: {0}", file));

            }
            catch (Exception ex)
            {
                if (!Directory.Exists(incorrectlyProcessedFiles + Destino))
                { CreateDirectory(incorrectlyProcessedFiles + Destino); }
                    
                MoveFileToIncorrectlyProcessed(xmlPath + file, incorrectlyProcessedFiles + Destino + file);
                WriteLog(string.Format("No se pudo crear correctamente el xml de cancelacion, archivo: {0}", file));
                WriteLogCancel(string.Format("No se pudo crear correctamente el xml de cancelacion, archivo: {0}", file));
              
                //MoveFileToIncorrectlyProcessed(xmlPath + file, incorrectlyProcessedFiles + file);
                //WriteLog(string.Format("No se pudo crear correctamente el xml de cancelacion, archivo: {0}", file));
            }
            return cancellationXml;
        }

        private string[] ObtainXMLData(string xmlPath)
        {
            //Funcion para obtener los valores de rfc emisor y el uuid de la factura timbrada
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlPath);

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("cfdi", "http://www.sat.gob.mx/cfd/3");
            nsMgr.AddNamespace("tfd", "http://www.sat.gob.mx/TimbreFiscalDigital");
            string uuid = "";
            string rfcEmisor = "";
            XmlNode nodeToFind;
            try
            {
                uuid = doc.SelectSingleNode("/cfdi:Comprobante/cfdi:Complemento/tfd:TimbreFiscalDigital/@UUID", nsMgr).Value;
                nodeToFind = doc.SelectSingleNode("/cfdi:Comprobante/cfdi:Emisor/@rfc", nsMgr);
                rfcEmisor = nodeToFind == null ? doc.SelectSingleNode("/cfdi:Comprobante/cfdi:Emisor/@Rfc", nsMgr).Value : doc.SelectSingleNode("/cfdi:Comprobante/cfdi:Emisor/@rfc", nsMgr).Value;
            }
            catch (Exception)
            {

                WriteLog("Ocurrio un error al obtener el rfcEmisor o el uuid de la factura timbrada", "ERROR");
            }

            string[] xmlData = { uuid, rfcEmisor };

            return xmlData;
        }

        private int ObtainCompanyId(string path, Configuracion configuracion)
        {

            XmlDocument doc = new XmlDocument();
            doc.Load(path);
            int idEmpresa = 0;

            XmlNamespaceManager nsMgr = new XmlNamespaceManager(doc.NameTable);
            nsMgr.AddNamespace("cfdi", "http://www.sat.gob.mx/cfd/3");

            XmlNode nodoEmisor = doc.SelectSingleNode("/cfdi:Comprobante/cfdi:Emisor/@Rfc", nsMgr) != null ? doc.SelectSingleNode("/cfdi:Comprobante/cfdi:Emisor/@Rfc", nsMgr) : doc.SelectSingleNode("/cfdi:Comprobante/cfdi:Emisor/@rfc", nsMgr);
            string rfcEmisor = nodoEmisor.Value;

            SqlParameter[] parameterList = { new SqlParameter("@rfcEmisor", rfcEmisor) };

            SQLServer BaseNomina = new SQLServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNomina"].ToString();
            System.Data.DataSet ds = BaseNomina.ExecuteQueryProcedure("SEL_IDEMPRESA_SP", parameterList);

            if (ds.Tables[0].Rows.Count > 0)
            {
                idEmpresa = Int32.Parse(ds.Tables[0].Rows[0][0].ToString());
            }

            return idEmpresa;
        }

        private bool IsDirectoryEmpty(string path) => !Directory.EnumerateFileSystemEntries(path).Any();

        private string ConsultaUsuarioCancela(Configuracion conf, string IdEmpresa)
        {
            SqlParameter[] parameterList = { new SqlParameter("@idUsuario", IdEmpresa) };
            string nombreUsua = "";

            SQLServer BaseNomina = new SQLServer();
            BaseNomina.ConnectionString = ConfigurationManager.ConnectionStrings["cnxBaseNominaCancelacion"].ToString();
            System.Data.DataSet ds = BaseNomina.ExecuteQueryProcedure("[dbo].[SEL_USUARIO_SP]", parameterList);
            conf.OpenSSLStartPath = ConfigurationManager.AppSettings["opensslAppPath"];
            conf.RequiredPath = ConfigurationManager.AppSettings["requiredPath"];

            if (ds.Tables[0].Rows.Count > 0)
            {
                nombreUsua = ds.Tables[0].Rows[0][0].ToString();
            }
            return nombreUsua;
        }

        private void CreateDirectory(string originalPath)
        {
            StringBuilder path = new StringBuilder();
            string[] splitPath = originalPath.Split('\\');

            foreach (string s in splitPath)
            {
                try
                {
                    path.Append(s + "\\");

                    if (Directory.Exists(path.ToString())) continue;

                    DirectoryInfo di = Directory.CreateDirectory(path.ToString());
                    WriteLog("directorio creado:" + path.ToString());
                }
                catch (Exception e)
                {
                    WriteLog(string.Format("The process failed: {0}", e.ToString()));
                }

            }
        }

        private void ChangeDocumentDate(string directoryPath)
        {
            string[] fileEntries = Directory.GetFiles(directoryPath);

            foreach (string path in fileEntries)
            {
                try
                {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(path);
                    string fileName = Path.GetFileNameWithoutExtension(path);

                    XmlElement root = doc.DocumentElement;
                    root.SetAttribute("fecha", "2017-01-01T00:20:29");
                    Console.WriteLine("documento {0} Fecha Cambiada", fileName);
                    doc.Save(path);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("documento err: {0} ", ex.Message);
                }
            }
        }

        private void DeleteQrFiles(Configuracion conf)
        {
            string[] fileEntries = Directory.GetFiles(conf.QrPath);
            foreach (string fileName in fileEntries)
            {
                DateTime creation = File.GetCreationTime(fileName);
                if (creation < DateTime.Now.AddHours(-1))
                {
                    File.Delete(fileName);
                }
            }        
        }

    }
}