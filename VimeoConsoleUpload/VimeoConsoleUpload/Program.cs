using System;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VimeoDotNet;
using VimeoDotNet.Authorization;
using VimeoDotNet.Constants;
using VimeoDotNet.Enums;
using VimeoDotNet.Exceptions;
using VimeoDotNet.Extensions;
using VimeoDotNet.Helpers;
using VimeoDotNet.Models;
using VimeoDotNet.Net;
using VimeoDotNet.Parameters;
using System.Net.Http;
using DotNetOpenAuth;
using DotNetOpenAuth.Messaging;
using DotNetOpenAuth.OAuth;
using DotNetOpenAuth.OAuth.ChannelElements;
using System.Net.Cache;
using System.Net.Mime;
using DotNetOpenAuth.OpenId.Extensions.SimpleRegistration;
using RestSharp.Extensions;

namespace VimeoConsoleUpload
{
    class Program
    {
        static string fileType = ".mp4";
        static string desktopPath = "";
        static string uploadFolder = "C:\\test/destination/";
        static string doneFolder = "C:\\test/uploaded/";
        static string groupNumber = "1";
        static string ip = "127.0.0.1:8080";

        static string userDataFile = "C:\\test/userdata.txt";

        public static string accessToken = "9a96db0eed0b820c74f68c856190dba5";
        public static string authorizeURL = "https://api.vimeo.com/oauth/authorize";
        public static string accessTokenURL = "https://api.vimeo.com/oauth/access_token";

        static string URL_add_local { get { return "http://" + ip + "/AddUser.php?"; } }
        static string URL_get_local { get { return "http://" + ip + "/GetUser.php?"; } }
        static string URL_update_local { get { return "http://" + ip + "/UpdateUser.php?"; } }
        static string URL_SyncDatabase { get { return "http://" + ip + "/SyncTable.php?table=1_" + groupNumber; } }

        static string secretKey = "mySecretKey";
        static string secretKeyHash = "";

        static string lastUserId = "";
        static string optionalUserId = "";
        static bool multiUserRequest;

        static void Main(string[] args)
        {
            desktopPath = "";

            secretKeyHash = Md5Sum(secretKey);

            if (args.Length > 0)
                groupNumber = args[0];
            if (args.Length > 1)
                uploadFolder = args[1] + "/";
            if (args.Length > 2)
                doneFolder = args[2] + "/";
            if (args.Length > 4)
                ip = args[4];

            Console.WriteLine("==============================");
            Console.WriteLine("Vimeo Upload:");
            Console.WriteLine("Video Path " + desktopPath + uploadFolder);
            Console.WriteLine("Uploaded Path " + desktopPath + doneFolder);
            Console.WriteLine("IP " + ip);
            Console.WriteLine("==============================");

            // set up folders
            if (!Directory.Exists(desktopPath + uploadFolder))
            {
                Directory.CreateDirectory(desktopPath + uploadFolder);
            }
            if (!Directory.Exists(desktopPath + doneFolder))
            {
                Directory.CreateDirectory(desktopPath + doneFolder);
            }


            while (true)
            {
                try
                {
                    new Program().Run().Wait();
                }
                catch (AggregateException ex)
                {
                    foreach (var e in ex.InnerExceptions)
                    {
                        Console.WriteLine("Error: " + e.Message);
                    }
                    Thread.Sleep(10000);
                }
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private async Task Run()
        {
            VimeoClient client = new VimeoClient(accessToken);
            User user = client.GetAccountInformation();

            while (true)
            {
                Thread.Sleep(5000);

                string[] files = Directory.GetFiles(desktopPath + uploadFolder);

                if (files.Length > 0)
                {
                    // s is the file with path and extenuation
                    foreach (string s in files)
                    {
                        //Parse for ID between parenthesis
                        if (s.Contains("(") && s.Contains(")"))
                        {
                            string lastThree = s.Substring(s.Length - 4);
                            if (string.Compare(lastThree, fileType, true) == 0)
                            {
                                multiUserRequest = false;

                                if (IsMultiUser(s))
                                {
                                    Console.Write("File name contains multiple users");
                                    lastUserId = GetMultiIdFromPath(s, true);
                                    optionalUserId = GetMultiIdFromPath(s, false);
                                    multiUserRequest = true;
                                }
                                else
                                {
                                    lastUserId = GetIdFromPath(s);
                                    multiUserRequest = false;
                                }

                                string name = GetNameFromPath(s);

                                // see if anyone is locking the file
                                bool fileLocked = false;
                                while (IsFileLocked(s))
                                {
                                    fileLocked = true;
                                    Thread.Sleep(5000);
                                }
                                if (fileLocked)
                                {

                                }

                                Console.WriteLine("\n= = = STARTING NEW UPLOAD - FILE NAME - " + name + " = = =\n");

                                // upload
                                bool uploaded = false;
                                IUploadRequest request;

                                VideoUpdateMetadata meta = new VideoUpdateMetadata();
                                using (var fileStream = new FileStream(s, FileMode.Open))
                                {
                                    BinaryContent bc = new BinaryContent(fileStream, "video/*");
                                    Console.WriteLine("1) UPLOADING TO VIMEO - " + name);
                                    request = await client.UploadEntireFileAsync(bc);

                                    // set meta data
                                    if (request.IsVerifiedComplete)
                                    {
                                        meta = new VideoUpdateMetadata();
                                        meta.Name = name;
                                        meta.Privacy = VideoPrivacyEnum.Disable;
                                        meta.Description = name;
                                        meta.ReviewLinkEnabled = true;
                                        meta.EmbedPrivacy = VideoEmbedPrivacyEnum.Public;

                                        await client.UpdateVideoMetadataAsync((long)request.ClipId, meta);
                                        Console.WriteLine("2) VIDEO SUCCESSFULLY UPLOADED TO VIMEO, LOOKING FOR USER IN TEXT FILE NEXT");
                                        uploaded = true;
                                    }
                                    else
                                    {
                                        uploaded = false;
                                        Console.WriteLine("2) VIDEO FAILED TO UPLOAD TO VIMEO");
                                    }
                                }

                                IUploadRequest picUpload;

                                client.UploadThumbnail((long)request.ClipId, "C:\\Bravo/BravoThumbnailTest.jpg");

                                if (uploaded)
                                {
                                    // set database video ID 
                                    Console.WriteLine("3) DONE - " + meta.Name + " - VIMEO ID - " + request.ClipId.ToString());

                                    SendVideoId(lastUserId, request.ClipId.ToString(), "");

                                    // move video to uploaded folder
                                    string file = GetFileFromPath(s);
                                    System.IO.File.Move(s, desktopPath + doneFolder + file);
                                }

                            } // filetype
                        } // has [ ]
                    } // foresch file
                } // files > 0

                
            } // while true
        } // Run

        public byte[] ImageToByteArray(Image image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, image.RawFormat);
                return ms.ToArray();
            }
        }

        public async void UploadThumbnail(string videoid)
        {
            WebClient wb = new WebClient();
            wb.Headers.Add("Authorization", "Bearer" + accessToken);
            var file = System.IO.File.Open("C:\\Bravo/BravoThumbnailTest.jpg", FileMode.Open);
            var asByteArrayContent = wb.UploadData(new Uri(new Uri("https://api.vimeo.com"), "/videos/" + videoid + "/pictures" + null), "PUT", file.ReadAsBytes());
            var asStringContent = Encoding.UTF8.GetString(asByteArrayContent);
            Console.WriteLine(asStringContent.ToString());

        }

        public static string[] GetUserData(string targetID)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            string[] userData = new string[0];
            bool foundID = false;

            using (var fileStream = new FileStream(userDataFile, FileMode.Open))
            {
                using (var streamReader = new StreamReader(fileStream, Encoding.UTF8, true))
                {
                    string line = "";
                    string id = "";

                    while ((line = streamReader.ReadLine()) != null)
                    {
                        id = line.Substring(0, line.IndexOf(','));


                        if (id == targetID)
                        {
                            Console.WriteLine("4) MATCH FOUND, ID - " + id);
                            userData = line.Split(',');
                            foundID = true;

                            break;
                        }
                        else
                        {
                            //Console.WriteLine("Couldn't find ID - " + id);
                        }
                    }

                    if (!foundID)
                    {
                        Console.WriteLine("4) MATCH NOT FOUND, ID - " + id);
                    }
                }

                Console.Write("5) DATA FIELDS PULLED FROM TEXT FILE - ");

                foreach (string s in userData)
                {
                    Console.Write(s + " | ");
                }

                Console.WriteLine();
            }
            stopWatch.Stop();
            Console.WriteLine("6) TIME IT TOOK TO FIND USER IN TEXT FILE - " + stopWatch.Elapsed);

            return userData;
        }

        // update database entry with video id
        public static void SendVideoId(string _userId, string _videoId, string _downloadUrl)
        {
            string[] userData = GetUserData(lastUserId);

            if (userData.Length > 0)
            {
                string _name = userData[1];
                string _email = userData[4];
                SendMMS(_name, _email, _videoId);
            }

            //Only if 2 user request
            if (multiUserRequest)
            {
                string[] optionalUserData = GetUserData(optionalUserId);

                if (optionalUserData.Length > 0)
                {
                    string _name = optionalUserData[1];
                    string _email = optionalUserData[4];
                    SendMMS(_name, _email, _videoId);
                }
            }
        }

        public async static void SendMMS(string _name, string _email, string _video_name)
        {
            using (var client = new HttpClient())
            {
                var values = new Dictionary<string, string>
                {
                    {"action", "create_user"},
                    {"key", "75idf.jnjKel"},
                    {"name", _name},
                    {"email", _email},
                    {"video_name", _video_name}
                };

                FormUrlEncodedContent content = new FormUrlEncodedContent(values);
                HttpResponseMessage response = await client.PostAsync("http://test.com/api/?", content);
                string responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine("7) USER SUCCESSFULLY CREATED ON EMAIL DATABASE, RESPONSE - " + responseString);
                Console.WriteLine("8) PROCESS COMPLETE - USER - " + _name + " SHOULD RECEIVE EMAIL AT - " + _email);
            }
        }

        static bool IsFileLocked(string _filePath)
        {
            FileStream flieStream = null;
            try
            {
                flieStream = new FileStream(_filePath, FileMode.Open);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (flieStream != null)
                    flieStream.Close();
            }
            return false;
        } // IsFileLocked

        // video name
        string GetNameFromPath(string _path)
        {
            int startOfString = _path.LastIndexOf(")") + 1;
            int endOfString = _path.Length - 4;
            int lengthOfString = endOfString - startOfString;
            return _path.Substring(startOfString, lengthOfString);
        } // 

        // video name
        bool IsMultiUser(string _path)
        {
            return _path.Contains('!');
        } // name

        // file from path
        string GetFileFromPath(string _path)
        {
            int startOfString = _path.LastIndexOf("(");
            int endOfString = _path.Length;
            int lengthOfString = endOfString - startOfString;
            return _path.Substring(startOfString, lengthOfString);
        } // file

        // return what's between '[' and ']'
        string GetIdFromPath(string _path)
        {
            int startOfString = _path.LastIndexOf("(") + 1;
            int endOfString = _path.LastIndexOf(")");
            int lengthOfString = endOfString - startOfString;
            return _path.Substring(startOfString, lengthOfString);
        } // ID

        // return what's between '[' and ']'
        string GetMultiIdFromPath(string _path, bool getfirstid)
        {
            int startOfString = _path.LastIndexOf("(") + 1;
            int endOfString = _path.LastIndexOf(")");
            int lengthOfString = endOfString - startOfString;

            string betweenBrackets = _path.Substring(startOfString, lengthOfString);
            string temp = "";

            if (getfirstid)
            {
                temp = betweenBrackets.Split('!')[0];
            }
            else
            {
                temp = betweenBrackets.Split('!')[1];
            }

            return temp;
        } // ID

        public static string Md5Sum(string strToEncrypt)
        {
            System.Text.UTF8Encoding ue = new System.Text.UTF8Encoding();
            byte[] bytes = ue.GetBytes(strToEncrypt);

            // Encrypt bytes
            System.Security.Cryptography.MD5CryptoServiceProvider md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] hashBytes = md5.ComputeHash(bytes);

            // Convert the encrypted bytes back to a string (base 16)
            string hashString = "";

            for (int i = 0; i < hashBytes.Length; i++)
            {
                hashString += System.Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
            }
            return hashString.PadLeft(32, '0');
        }

    } // program


}