using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace dmtipacs_uploader
{
    class Program
    {
        static void copyDirectory(string strSource, string strDestination)
        {
            if (!Directory.Exists(strDestination))
            {
                Directory.CreateDirectory(strDestination);
            }

            DirectoryInfo dirInfo = new DirectoryInfo(strSource);
            FileInfo[] files = dirInfo.GetFiles();
            foreach(FileInfo tempfile in files )
            {
                tempfile.CopyTo(Path.Combine(strDestination,tempfile.Name),true);
            }

            DirectoryInfo[] directories = dirInfo.GetDirectories();
            foreach(DirectoryInfo tempdir in directories)
            {
                copyDirectory(Path.Combine(strSource, tempdir.Name), Path.Combine(strDestination, tempdir.Name));
            }

        }

        private static void writeJSON(string dicomFilename, string destinationDirectory)
        {
            JavaScriptSerializer js = new JavaScriptSerializer();
            bool found = false;

            List<string> files = new List<string>(Directory.EnumerateFiles(destinationDirectory + "\\output"));
            foreach (var file in files)
            {
                // Open output file
                string line;
                System.IO.StreamReader f = new System.IO.StreamReader(file);
                Procedure p = new Procedure();

                // Read each line
                found = false;
                while ((line = f.ReadLine()) != null)
                {
                    if (line.IndexOf("DICOM file:") > -1) {
                        p.DICOMFileName = line.Substring(12);
                        if (p.DICOMFileName.IndexOf(dicomFilename) > -1)
                        {
                            found = true;
                            p.TransactionNumber = "";
                            p.PatientName = "";
                            p.PatientAddress = "";
                            p.Gender = "";
                            p.DateOfBirth = "";
                            p.Age = "";
                            p.Particulars = "";
                            p.BodyPart = "";
                            p.StudyDate = "";
                            p.ReferringPhysician = "";
                            p.HospitalNumber = "";
                            p.HospitalWardNumber = "";
                        }
                    }
                    if (found == true)
                    {
                        if (line.IndexOf("(0008),(0050)") > -1) p.TransactionNumber = line.Substring(74).Trim();
                        if (line.IndexOf("(0010),(0010)") > -1) p.PatientName = line.Substring(74).Trim().Replace("^", " ");
                        if (line.IndexOf("(0019),(10d4)") > -1) p.PatientAddress = line.Substring(74).Trim().Replace("^", " ");
                        if (line.IndexOf("(0010),(0040)") > -1) p.Gender = line.Substring(74).Trim();
                        if (line.IndexOf("(0010),(0030)") > -1) p.DateOfBirth = line.Substring(74).Trim();
                        if (line.IndexOf("(0019),(10d3)") > -1) p.Particulars = line.Substring(74).Trim().Replace("^"," ");
                        if (line.IndexOf("(0018),(0015)") > -1) p.BodyPart = line.Substring(74).Trim();
                        if (line.IndexOf("(0008),(0020)") > -1) p.StudyDate = line.Substring(74).Trim();
                        if (line.IndexOf("(0008),(0090)") > -1) p.ReferringPhysician = line.Substring(74).Trim().Replace("^", " ");
                        if (line.IndexOf("(0010),(0020)") > -1) p.HospitalNumber = line.Substring(74).Trim().Replace("^", " ");
                        if (line.IndexOf("(0038),(0300)") > -1) p.HospitalWardNumber = line.Substring(74).Trim().Replace("^", " ");

                        p.UserId = 24; // margosatubig
                    }
                }

                // Close output file
                f.Close();

                // Create json file
                if(found==true) {
                    string json = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(p);
                    string jsonFilename = destinationDirectory + "\\json\\" + dicomFilename + ".json";

                    File.WriteAllText(jsonFilename, json);
                    break;
                }
            }
        }

        private static void sendJSON(string destinationDirectory)
        {
            try
            {
                List<string> files = new List<string>(Directory.EnumerateFiles(destinationDirectory + "\\json"));
                foreach (var file in files)
                {
                    // Read json file
                    string json;
                    using (StreamReader r = new StreamReader(file))
                    {
                        json = r.ReadToEnd();
                    }

                    // Send json to server
                    var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://www.dmtipacs.com/api/procedureExternal/add");
                    httpWebRequest.ContentType = "application/json";
                    httpWebRequest.Method = "POST";
                    using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                    {
                        var json_serializer = new JavaScriptSerializer();
                        Procedure p = json_serializer.Deserialize<Procedure>(json);
                        streamWriter.Write(new JavaScriptSerializer().Serialize(p));
                    }
                    var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                    // Process response
                    using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        if (result.Trim() == "\"Success\"")
                        {
                            File.Delete(file);
                            Console.WriteLine(result);
                        }
                        else
                        {
                            Console.WriteLine(result);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("DMTIPACS Uploader v1.20170331");

            int i = 0;
            string source = "x", destination = "x";
            foreach (var arg in args)
            {
                if (i == 0) source = arg;
                else if (i == 1) destination = arg;
                i++;
            }

            //Console.Write("DICOM Source Path : ");
            //string DICOMSourcePath = Console.ReadLine();
            //DICOMSourcePath = String.IsNullOrEmpty(DICOMSourcePath) ? source : DICOMSourcePath;
            string DICOMSourcePath = source;

            //Console.Write("DICOM Destination Path : ");
            //string DICOMDestinationPath = Console.ReadLine();
            //DICOMDestinationPath = String.IsNullOrEmpty(DICOMDestinationPath) ? destination : DICOMDestinationPath;
            string DICOMDestinationPath = destination;

            while (true)
            {
                Console.WriteLine("DMTIPACS Uploader v1.20170323: Checking...");

                Console.WriteLine("Source: " + DICOMSourcePath);
                Console.WriteLine("Destination: " + DICOMDestinationPath);

                List<string> dirs = new List<string>(Directory.EnumerateDirectories(DICOMSourcePath));

                foreach (var dir in dirs)
                {
                    string sourceFile = DICOMSourcePath + "\\" +  dir.Substring(dir.LastIndexOf("\\") + 1);
                    string destinationFile = DICOMDestinationPath + "\\" +  dir.Substring(dir.LastIndexOf("\\") + 1);

                    // Copies the directory to destination then deletes source once done copying
                    Console.WriteLine("Copying {0}", dir.Substring(dir.LastIndexOf("\\") + 1));
                    copyDirectory(sourceFile, destinationFile);
                    Directory.Delete(sourceFile, true);

                    // Run DicomParser at the destination 
                    List<string> dicoms = new List<string>(Directory.EnumerateDirectories(destinationFile));
                    foreach (var dicom in dicoms)
                    {
                        Console.WriteLine("Processing DICOM {0}", dicom.Substring(dir.LastIndexOf("\\") + 1));
                        string cmd = DICOMDestinationPath + "\\DICOMParser.exe";
                        string arg = "-f" + dicom +  " -s -o" + DICOMDestinationPath + "\\output";
                        Process P2 = Process.Start(cmd, arg);
                        P2.WaitForExit();
                        int result2 = P2.ExitCode;

                        string dicomFilename = dicom.Replace(destinationFile + "\\", "");
                        writeJSON(dicomFilename, DICOMDestinationPath);
                    }
                }

                Thread.Sleep(5000);

                sendJSON(DICOMDestinationPath);
            }
        }
    }

    public class Procedure
    {
        public string TransactionNumber;

        public string DICOMFileName;

        public string PatientName;
        public string PatientAddress;
        public string Gender;
        public string DateOfBirth;
        public string Age;

        public string Particulars;
        public string BodyPart;

        public string StudyDate;
        public string ReferringPhysician;
        public string HospitalNumber;
        public string HospitalWardNumber;

        public int UserId;
    }
}
