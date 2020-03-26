using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TimbradoNomina.NominaObjects
{
    public class Configuracion
    {
        public string XsltPath { get; set; }
        public string XsdPath { get; set; }
        public string NameSpace { get; set; }
        public string SuscriptorRFC { get; set; }
        public string AgenteTI { get; set; }
        public string QrPath { get; set; }
        public string ReportUrl { get; set; }
        public string KeyPemPath { get; set; }
        public string OpenSSLStartPath { get; set; }
        public string RequiredPath { get; set; }
        public string CertB64Content { get; set; }
        public string QrUrl { get; set; }
        public string CertificateNumber { get; set; }
        public string CSD { get; set; }//Se agrega para cancelacion
        public string CSDPASSWORD { get; set; }//Se agrega para cancelacion
        public string CFDINameSpace { get; set; }
    }

    public class Carpeta
    {
        public string IDTipo { get; set; }
        public string IDUsuario { get; set; }
        public string IDEmpresa { get; set; }
        public string IDNomina { get; set; }
        public string NombreDirectorio { get; set; }
        public string RutaDirectorio { get; set; }
        public string NoDocumentos { get; set; }
        public string OkPath { get { return RutaDirectorio.Replace("\\Origen", "\\Timbrados"); } }
        //public string OkPathSelf { get { return RutaDirectorio.Replace("\\Origen", "\\Timbrado_Nomina"); } }//Desarrollo
        //Orginal
        //public string OkPathSelf { get { return RutaDirectorio.Replace("\\TimbradoNominaParaRH\\Origen", "\\Timbrado_Nomina"); } }//Produccion
        public string OkPathSelf { get { return RutaDirectorio.Replace("\\Timbrado_Nomina\\Origen", "\\TimbradoNominaParaRH\\Timbrados"); } }//Produccion
        public string BadPath { get { return RutaDirectorio.Replace("\\Origen", "\\SinTimbrar"); } }
        public bool HuboError = false;
    }

    public class Documento
    {
        public string IDNomina { get; set; }
        public string IDGrupo { get; set; }
        public string IDSucursal { get; set; }
        public string IDDepartamento { get; set; }
        public string IDEmpresa { get; set; }
        public string XmlOrigenString { get; set; }
        public string XmlOrigenBase64 { get; set; }
        public string XmlResultString { get; set; }
        public string XmlResultBase64 { get; set; }
        public string CodigoResultado { get; set; }
        public string Description { get; set; }
        public string RutaDirectorio { get; set; }
        public string RutaDirectorioOk { get; set; }
        //
        public string RutaDirectorioOkSelft { get; set; }
        //
        public string RutaDirectorioError { get; set; }
        public string RutaXml { get; set; }
        public string UUID { get; set; }//Se agrego para Cancelacion

        public string RutaArchivoOk { get { return RutaDirectorioOk + NombreXml + ".xml"; } }
        //Cambio
        public string RutaArchivoOkSelft { get { return RutaDirectorioOkSelft + NombreXml + ".xml"; } }
        //cambio
        public string RutaArchivoTimbrado { get { return RutaDirectorioOk + NombreXml + "tim.xml"; } }
        public string RutaArchivoPDF { get { return RutaDirectorioOk + NombreXml + ".pdf"; } }
        public string RutaArchivoError { get { return RutaDirectorioError + NombreXml + ".xml"; } }
        public string RutaArchivoRepetido { get { return RutaDirectorioOk + NombreXml; } }
        public string RutaArchivoRepetidoError { get { return RutaDirectorioError + NombreXml; } }

        public string NombreXml { get; set; }
        public string RutaArchivoFirma { get { return RutaDirectorio + NombreXml + "sign.bin"; } }
        public string RutaArchivoCadenaOriginal { get { return RutaDirectorio + NombreXml + ".txt"; } }
        public string RutaArchivoCadenaOriginalB64 { get { return RutaDirectorio + NombreXml + "b64.txt"; } }

        public InsertParams Parametros { get; set; }
    }


    public class InsertParams
    {
        public DateTime? Fecha { get; set; }
        public decimal Total { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public DateTime? FechaPago { get; set; }
        public string Departamento { get; set; }
        public string NumEmpleado { get; set; }
        public string Puesto { get; set; }
    }
}
