using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;


namespace CommonFunctions.Functions
{
    public class HTTPManager
    {

        public string SendPost(string url, string json)
        {
            url = "http://localhost:5488/api/report";
            json = "{ \"template\": { \"name\" : \"prueba\" },\"data\" : { \"to\": \"Pavel Sladek\",     \"from\": \"Jan Blaha\",  \"price\": 800000 }}";

            var request = (HttpWebRequest)WebRequest.Create(url);
            var postData = json;
            var data = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();
            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

            return responseString;
        }


        public void RequestPDF(string url, string json, string pdfPath)
        {                        
            var request = (HttpWebRequest)WebRequest.Create(url);
            var postData = json;
            //var data = Encoding.ASCII.GetBytes(postData);
            var data = Encoding.UTF8.GetBytes(postData); 

            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            var response = (HttpWebResponse)request.GetResponse();
            
            using (Stream output = File.OpenWrite(pdfPath))
            using (Stream input = response.GetResponseStream())
            {
                input.CopyTo(output);
            }
        
        }



    }
}
