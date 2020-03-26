using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CommonFunctions.Functions
{
    public class IOManager
    {
        string path = string.Empty;

        public string Path
        {
            get { return path; }
            set { path = value; }
        }

        string content = string.Empty;

        public string Content
        {
            get { return content; }
        }

        public void SetContent()
        {
            content = File.ReadAllText(path);
        }


        public void CompleteFolders(string originalPath)
        {
            StringBuilder path = new StringBuilder();
            string[] splitPath = originalPath.Split('\\');

            foreach (string s in splitPath)
            {
                try
                {
                    path.Append(s + "\\");

                    if (Directory.Exists(path.ToString()))
                    {
                        continue;
                    }

                    DirectoryInfo di = Directory.CreateDirectory(path.ToString());
                    Console.WriteLine("The directory was created successfully at {0}.", Directory.GetCreationTime(path.ToString()));
                }
                catch (Exception e)
                {
                    Console.WriteLine("The process failed: {0}", e.ToString());
                }

            }
        }




    }
}
