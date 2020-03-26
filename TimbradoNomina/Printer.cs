using System;
using System.Drawing.Printing;
using System.IO;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace TimbradoNomina
{
    public class Imprimir
    {
        public void enviarImprimir(string filePath, string namePrint, string rutaAdobe, string ipImpresora, List<string> documentos)
        {
            PrintDocument pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = namePrint;

            if (pd.PrinterSettings.IsValid)
            {
                DirectoryInfo di = new DirectoryInfo(filePath);

                if (documentos.Count > 0)
                {
                    foreach (var item in documentos)
                    {
                        Pdf.PrintPDFs(filePath + item, rutaAdobe, namePrint, ipImpresora);
                        Console.WriteLine("{0}", item);
                    }
                }
                else
                    foreach (var fi in di.GetFiles())
                    {
                        if (fi.Extension.ToLower() == ".pdf")
                        {
                            Console.WriteLine("Imprimendo archivo: " + filePath + fi.Name);
                            Pdf.PrintPDFs(filePath + fi.Name, rutaAdobe, namePrint, ipImpresora);
                            ////////imprimir(filePath + fi.Name, namePrint);
                            Console.WriteLine("Archivo impreso OK: " + filePath + fi.Name);
                        }
                    }
            }
            else
                Console.WriteLine("Impresora invalida!");

            Console.WriteLine("Fin del proceso.");

            //Console.ReadLine();
        }
    }

    public class Pdf
    {
        public static Boolean PrintPDFs(string pdfFileName, string rutaAdobe, string impresora, string ipImpresora)
        {
            try
            {
                Process proc = new Process();
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                proc.StartInfo.Verb = "print";

                //Define location of adobe reader/command line
                //switches to launch adobe in "print" mode
                proc.StartInfo.FileName = rutaAdobe;
                //proc.StartInfo.Arguments = String.Format(@"/p /h {0}", pdfFileName);
                proc.StartInfo.Arguments = String.Format(@"/t " + '\u0022' + pdfFileName + '\u0022' + " " + '\u0022' + impresora + '\u0022' + " " +'\u0022' + "dirver printer" + '\u0022' + " " + '\u0022' + ipImpresora + '\u0022' + "");
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.CreateNoWindow = false;

                proc.Start();
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                if (proc.HasExited == false)
                    proc.WaitForExit(10000);

                proc.EnableRaisingEvents = true;

                proc.Close();
                KillAdobe("AcroRd32");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        //For whatever reason, sometimes adobe likes to be a stage 5 clinger.
        //So here we kill it with fire.
        private static bool KillAdobe(string name)
        {
            foreach (Process clsProcess in Process.GetProcesses().Where(
                         clsProcess => clsProcess.ProcessName.StartsWith(name)))
            {
                clsProcess.Kill();
                return true;
            }
            return false;
        }
    }//END Class
}
