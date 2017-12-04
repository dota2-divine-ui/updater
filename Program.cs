using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace DivineUI_Updater
{
    class Program
    {
        /// <summary>
        /// Location of the text file that contains the most recent version.
        /// </summary>
        static string remoteLatestVersionFile = "https://raw.githubusercontent.com/dota2-divine-ui/divine-ui/master/version.txt";

        /// <summary>
        /// Location of the ZIP file that contains the latest version of Divine UI
        /// </summary>
        static string remotePackage = "https://github.com/dota2-divine-ui/divine-ui/archive/master.zip";

        /// <summary>
        /// Location where we will save the ZIP of the latest version
        /// </summary>
        static string packageSavePath;

        /// <summary>
        /// Current directory!
        /// </summary>
        static string currentDirectory = Directory.GetCurrentDirectory();

        /// <summary>
        /// Location of the installation directory
        /// </summary>
        static string installDirectory = currentDirectory + "/dota_divine_ui";

        /// <summary>
        /// Current version
        /// </summary>
        static string currentVersion = "1.0.0";

        /// <summary>
        /// Last version
        /// </summary>
        static string latestVersion;

        /// <summary>
        /// Location of the text file that contains the current version.
        /// </summary>
        static string versionFilePath = installDirectory + "/version.txt";
            
        /// <summary>
        /// Web Client, for download
        /// </summary>
        static WebClient client = new WebClient();

        /// <summary>
        /// 
        /// </summary>
        static ManualResetEvent reset;

        /// <summary>
        /// Returns if we have the latest version installed
        /// </summary>
        static bool HasLatestVersion()
        {
            // We read the file that contains the current version
            if (File.Exists(versionFilePath))
            {
                StreamReader stream = File.OpenText(versionFilePath);
                currentVersion = stream.ReadLine();
            }

            // Download the text of the latest version
            latestVersion = client.DownloadString(remoteLatestVersionFile);

            // Compare!
            return (latestVersion == currentVersion);
        }

        /// <summary>
        /// Program entry
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Console.Title = "Divine UI - Updater";

            // 
            if (!Directory.Exists(currentDirectory + "/dota/"))
            {
                Console.WriteLine("Oops!");
                Console.WriteLine("To install or update Divine UI you need to move this Updater to the /game/ folder of Dota 2.");
                Console.WriteLine();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("---------------------------------");
            Console.WriteLine("-- Divine UI - Updater");
            Console.WriteLine("---------------------------------");
            Console.WriteLine("-- Author:");
            Console.WriteLine("-- Iván Bravo (@Kolesias)");
            Console.WriteLine("---------------------------------");
            Console.WriteLine();
            Console.WriteLine("> Relax, you are doing fine.");
            Console.WriteLine();

            Console.WriteLine("Checking if there is a new version...");
            Console.WriteLine();

            bool hasLatest = HasLatestVersion();

            // We have the latest version!
            if (hasLatest)
            {
                Console.WriteLine("You have the latest version! (" + latestVersion + ") There's nothing to do :)");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // For me! I do not want my original folder to be deleted.
            if ( Directory.Exists(installDirectory + "/.git/") )
            {
                installDirectory = currentDirectory + "/" + "dota_divine_ui_test";
                Console.WriteLine("GIT directory detected! Running in testing mode...");
                Console.WriteLine();
            }

            Console.WriteLine("There is a new version!");
            Console.WriteLine("Downloading the version " + latestVersion + "...");
            Console.WriteLine();

            reset = new ManualResetEvent(false);
            packageSavePath = currentDirectory + "/" + latestVersion + ".zip";
            
            if ( File.Exists(packageSavePath) )
            {
                Console.WriteLine("The file of the latest version was found, omitting the download...");
                Console.WriteLine("If there was a problem related to the Divine UI download, please delete the file manually.");
                Console.WriteLine();
                ExtractAndFinish();
            }
            else
            {
                // Start the download
                client.DownloadProgressChanged += Client_DownloadProgressChanged;
                client.DownloadFileCompleted += Client_DownloadFileCompleted;
                client.DownloadFileAsync(new Uri(remotePackage), packageSavePath);

                // Waiting...
                reset.WaitOne();
            }

            Console.WriteLine();
            Console.WriteLine("Send your comments and suggestions to: r/Dota2DivineUI");
            Console.WriteLine();

            // Thanks!
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Move files and folders, from the folder generated by Github to the installation folder.
        /// </summary>
        /// <param name="folder"></param>
        private static void MoveMasterFiles(string folder)
        {
            // Get the list of files and folders
            string[] files = Directory.GetFileSystemEntries(folder);

            foreach (string file in files)
            {
                // This is a directory
                if ( Directory.Exists(file) )
                {
                    string destFolder = file.Replace("divine-ui-master", "");

                    // Make sure that the directory exists.
                    if (!Directory.Exists(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }

                    // Move all the files
                    MoveMasterFiles(file);
                    continue;
                }

                // This does not interest us
                if (Path.GetFileName(file) == ".gitignore")
                    continue;

                // Move!
                string srcFile = file;
                string destFile = file.Replace("divine-ui-master", "");
                File.Move(srcFile, destFile);
            }
        }

        /// <summary>
        /// Extract the files and finish
        /// </summary>
        private static void ExtractAndFinish()
        {
            Console.WriteLine("Extracting the files...");

            if (Directory.Exists(installDirectory))
            {
                // Delete the folder of the current version
                Directory.Delete(installDirectory, true);
            }

            // Fresh directory
            Directory.CreateDirectory(installDirectory);

            // ZIP extraction...
            ZipFile.ExtractToDirectory(packageSavePath, installDirectory);

            // Damn it Github!
            string masterFolder = installDirectory + "/divine-ui-master/";

            // We need to move the files...
            if (Directory.Exists(masterFolder))
            {
                MoveMasterFiles(masterFolder);
                Directory.Delete(masterFolder, true);
            }

            Console.WriteLine("Extraction completed!");

            // The ZIP file no longer interests us.
            //File.Delete(remoteSavePath);

            // Verify again
            bool hasLatest = HasLatestVersion();

            if (hasLatest)
            {
                Console.WriteLine("Verified! You already have the latest version, enjoy it!");
            }
            else
            {
                Console.WriteLine("Oh no! We have not been able to verify that you have the latest version, it can be a problem of the Updater, check manually.");
            }

            reset.Set();
        }

        private static void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            Console.WriteLine("Download completed!");
            ExtractAndFinish();
        }

        private static void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Title = "Divine UI - Updater - " + e.ProgressPercentage + "%";
            Console.Write("\r{0}%   ", e.ProgressPercentage);
        }
    }
}
