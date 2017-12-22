using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;
using KVLib;
using ByteSizeLib;

namespace DivineUI_Updater
{
    class Program
    {
        /// <summary>
        /// Location of the text file that contains the most recent version.
        /// </summary>
        static string remoteLatestVersionFile = "https://raw.githubusercontent.com/dota2-divine-ui/divine-ui/master/version.txt";

        /// <summary>
        /// Location of the file that contains the GameInfo to install Divine UI.
        /// </summary>
        static string remoteGameInfo = "https://raw.githubusercontent.com/dota2-divine-ui/updater/master/data/gameinfo.gi";

        /// <summary>
        /// 
        /// </summary>
        static string[] packages = {
            "https://github.com/dota2-divine-ui/divine-ui/archive/",
            "https://divineui.woots.xyz/releases/master.zip",
            "https://github.com/dota2-divine-ui/divine-ui/archive/master.zip"
        };

        /// <summary>
        /// 
        /// </summary>
        static int usePackage = 0;

        /// <summary>
        /// Location where we will save the ZIP of the latest version
        /// </summary>
        static string packageSavePath;

        /// <summary>
        /// Current directory!
        /// </summary>
        static string currentDirectory = Directory.GetCurrentDirectory();

        /// <summary>
        /// 
        /// </summary>
        static string gameDirectory = currentDirectory;

        /// <summary>
        /// Location of the installation directory
        /// </summary>
        static string installDirectory = gameDirectory + "\\dota_divine_ui";

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
        static string versionFilePath = installDirectory + "\\version.txt";

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
            if ( File.Exists(versionFilePath) ) {
                StreamReader stream = File.OpenText(versionFilePath);
                currentVersion = stream.ReadLine();
            }

            // Download the text of the latest version
            latestVersion = client.DownloadString(remoteLatestVersionFile);

            // Compare!
            return ( latestVersion == currentVersion );
        }

        /// <summary>
        /// 
        /// </summary>
        static void Abort( string message = "" )
        {
            if ( message.Length > 0 ) {
                Console.WriteLine();
                Console.WriteLine(message);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            Environment.Exit(0);
        }

        static KeyValue FindChildren( KeyValue values, string keyName )
        {
            if ( !values.HasChildren ) {
                return null;
            }

            for( int it = 0; it < values.Children.Count(); ++it ) {
                KeyValue child = values.Children.ElementAt(it);

                if ( child.Key == keyName ) {
                    return child;
                }
            }

            return null;
        }

        /// <summary>
        /// Try to find the route where Steam and Dota 2 are installed.
        /// </summary>
        /// <returns></returns>
        static void FindGameDirectory()
        {
            // Look for the Steam installation path in the registry.
            string steamDirectory = (string)Registry.GetValue("HKEY_CURRENT_USER\\Software\\Valve\\Steam", "SteamPath", null);

            // So... you dont have it in the registry huh.
            if ( steamDirectory == null || !Directory.Exists(steamDirectory) ) {
                Console.WriteLine("The Steam installation path could not be found from the registry. Do you have Steam installed?");
                goto unableToObtain;
            }

            string configFile = steamDirectory + "/config/config.vdf";

            // So... you dont have Steam Configuration file...
            if ( !File.Exists(configFile) ) {
                Console.WriteLine("The Steam installation path has been found, but the configuration file dont exist!");
                goto unableToObtain;
            }

            try {
                // Read and Parse the Steam configuration file.
                String configData = File.ReadAllText(configFile);
                KeyValue values = KVParser.ParseKeyValueText(configData);

                KeyValue software = FindChildren(values, "Software");
                KeyValue valve = FindChildren(software, "Valve");
                KeyValue steam = FindChildren(valve, "Steam");

                KeyValue installFolder1 = FindChildren(steam, "BaseInstallFolder_1");
                KeyValue installFolder2 = FindChildren(steam, "BaseInstallFolder_2");

                // Try with 2 routes
                string baseInstallFolder = null;
                string baseInstallFolder2 = null;

                if ( installFolder1 != null ) {
                    baseInstallFolder = installFolder1.GetString();
                }

                if ( installFolder2 != null ) {
                    baseInstallFolder2 = installFolder2.GetString();
                }

                String gamePathExt = "\\steamapps\\common\\dota 2 beta\\game";

                if ( baseInstallFolder != null && Directory.Exists(baseInstallFolder + gamePathExt) ) {
                    gameDirectory = baseInstallFolder + gamePathExt;
                }
                else if ( baseInstallFolder2 != null && Directory.Exists(baseInstallFolder2 + gamePathExt) ) {
                    gameDirectory = baseInstallFolder2 + gamePathExt;
                }
                else if ( Directory.Exists(steamDirectory + gamePathExt) ) {
                    gameDirectory = steamDirectory + gamePathExt;
                }
                else {
                    goto unableToObtain;
                }

                goto setupPaths;
            }
            catch( Exception why ) {
                Console.WriteLine("There was a problem reading the Steam configuration file, please report the following message: " + why.Message);
                goto unableToObtain;
            }

        unableToObtain:
            Console.WriteLine("Unable to obtain the location of the Dota 2 folder. Checking if we are already in the /game/ folder...");

        setupPaths:
            // ?
            if ( !Directory.Exists(gameDirectory + "\\dota\\") ) {
                Console.WriteLine();
                Console.WriteLine(gameDirectory);
                Abort("We are sorry but the Dota 2 installation folder could not be found. Try placing the Updater files in the /game/ folder of Dota 2.");
            }

            installDirectory = gameDirectory + "\\dota_divine_ui";
            versionFilePath = installDirectory + "\\version.txt";
        }

        /// <summary>
        /// Program entry
        /// </summary>
        /// <param name="args"></param>
        static void Main( string[] args )
        {
            Console.Title = "Divine Updater";

            Console.WriteLine("---------------------------------");
            Console.WriteLine("-- Divine Updater");
            Console.WriteLine("---------------------------------");
            Console.WriteLine("-- Version: {0}", FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            Console.WriteLine("---------------------------------");
            Console.WriteLine("-- Author:");
            Console.WriteLine("-- Iván Bravo (@Kolesias)");
            Console.WriteLine("---------------------------------");
            Console.WriteLine();
            Console.WriteLine("> Relax, you are doing fine.");
            Console.WriteLine();

            foreach ( var process in Process.GetProcessesByName("vconsole2") ) {
                process.Kill();
            }

            foreach ( var process in Process.GetProcessesByName("dota2") ) {
                process.Kill();
            }

            // We try to find the game folder of Dota 2
            FindGameDirectory();
            Console.WriteLine("Game Directory: {0}", gameDirectory);

            Console.WriteLine();
            Console.WriteLine("Checking if there is a new version...");
            Console.WriteLine();

            bool hasLatest = HasLatestVersion();

            // We have the latest version!
            if ( hasLatest ) {
                Console.WriteLine("You have the latest version! ({0}) There's nothing to do :)", latestVersion);
                Abort();
            }

            // For me! I do not want my original folder to be deleted.
            if ( Directory.Exists(installDirectory + "\\.git\\") ) {
                installDirectory = gameDirectory + "\\dota_divine_ui_test";
                Console.WriteLine("GIT directory detected! Running in testing mode...");
                Console.WriteLine();
            }

            Console.WriteLine("There is a new version!");
            Console.WriteLine("Downloading the version {0}...", latestVersion);
            Console.WriteLine();

            String remotePackage = "";

            if( usePackage == 0 ) {
                remotePackage = packages[0] + latestVersion + ".zip";
            }
            else {
                remotePackage = packages[usePackage];
            }

            reset = new ManualResetEvent(false);
            packageSavePath = Path.GetTempPath() + "\\divine-ui-" + latestVersion + ".zip";

            if ( File.Exists(packageSavePath) ) {
                File.Delete(packageSavePath);
            }

            Console.WriteLine("Mirror: {0}", remotePackage);

            // Start the download
            client.DownloadProgressChanged += Client_DownloadProgressChanged;
            client.DownloadFileCompleted += Client_DownloadFileCompleted;
            client.DownloadFileAsync(new Uri(remotePackage), packageSavePath);

            // Waiting...
            reset.WaitOne();

            Console.WriteLine();
            Console.WriteLine("Send your comments and suggestions to: r/Dota2DivineUI");
            Console.WriteLine();

            // Start Dota 2
            Process.Start("steam://rungameid/570");

            // Thanks!
            Abort();
        }

        /// <summary>
        /// Move files and folders, from the folder generated by Github to the installation folder.
        /// </summary>
        /// <param name="folder"></param>
        private static void MoveMasterFiles( string folder )
        {
            // Get the list of files and folders
            string[] files = Directory.GetFileSystemEntries(folder);

            foreach ( string file in files ) {
                // This is a directory
                if ( Directory.Exists(file) ) {
                    string destFolder = file.Replace("divine-ui-master", "");

                    // Make sure that the directory exists.
                    if ( !Directory.Exists(destFolder) ) {
                        Directory.CreateDirectory(destFolder);
                    }

                    // Move all the files
                    MoveMasterFiles(file);
                    continue;
                }

                // This does not interest us
                if ( Path.GetFileName(file) == ".gitignore" )
                    continue;

                // Move!
                string srcFile = file;
                string destFile = file.Replace("divine-ui-master", "");
                File.Move(srcFile, destFile);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static void InstallGameInfo()
        {
            // Download!
            string gameInfoData = client.DownloadString(remoteGameInfo);
            File.WriteAllText(gameDirectory + "\\dota\\gameinfo.gi", gameInfoData);
        }

        /// <summary>
        /// Extract the files and finish
        /// </summary>
        private static void ExtractAndFinish()
        {
            Console.WriteLine("Extracting the files...");

            if ( Directory.Exists(installDirectory) ) {
                // Delete the folder of the current version
                Directory.Delete(installDirectory, true);
            }

            // Empty directory
            Directory.CreateDirectory(installDirectory);

            try {
                // ZIP extraction...
                ZipFile.ExtractToDirectory(packageSavePath, installDirectory);
            }
            catch ( Exception why ) {
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("A problem occurred while trying to extract the ZIP file!");
                Console.WriteLine(why.Message);
                Console.WriteLine();

                if ( usePackage < (packages.Length-1) ) {
                    usePackage++;

                    Console.WriteLine("Trying to download the file from an alternative location ({0})...", usePackage);
                    Thread.Sleep(5000);
                    Console.Clear();

                    string[] args = { };
                    Main(args);
                    return;
                }
                else {
                    Console.WriteLine("> We are sorry! But we could not install Divine UI");
                    Console.WriteLine("> Try running the Updater with administrator permissions or disable any program that may interfere with the download (Antivirus, firewall)");
                    Console.WriteLine("> If all else fails, please proceed with the manual installation:");
                    Console.WriteLine("> https://redd.it/7hi2vc");
                    Abort();
                }
            }

            // Damn it Github!
            string masterFolder = installDirectory + "\\divine-ui-master";

            // We need to move the files...
            if ( Directory.Exists(masterFolder) ) {
                MoveMasterFiles(masterFolder);
                Directory.Delete(masterFolder, true);
            }

            // 
            InstallGameInfo();

            Console.WriteLine("Extraction completed!");
            Console.WriteLine();

            // The ZIP file no longer interests us.
            File.Delete(packageSavePath);

            // Verify again
            bool hasLatest = HasLatestVersion();

            if ( hasLatest ) {
                Console.WriteLine("Verified! You already have the latest version, enjoy it!");
            }
            else {
                Console.WriteLine("Oh no! We have not been able to verify that you have the latest version, it can be a problem of the Updater, check manually.");
            }

            reset.Set();
        }

        private static void Client_DownloadFileCompleted( object sender, System.ComponentModel.AsyncCompletedEventArgs e )
        {
            Console.WriteLine("Download completed!");
            Console.WriteLine();
            Console.WriteLine();

            ExtractAndFinish();
        }

        private static void Client_DownloadProgressChanged( object sender, DownloadProgressChangedEventArgs e )
        {
            double MegaBytesReceived = Math.Round(ByteSize.FromBytes(e.BytesReceived).MegaBytes, 1);
            double MegaBytesToReceive = Math.Round(ByteSize.FromBytes(e.TotalBytesToReceive).MegaBytes, 1);

            if ( MegaBytesToReceive > 0 ) {
                Console.Title = "Divine Updater - " + e.ProgressPercentage + "%";
                Console.Write("\rDownload Progress: {0}% ({1}/{2} MB)   ", e.ProgressPercentage, MegaBytesReceived, MegaBytesToReceive);
            }
            else {
                Console.Title = "Divine Updater - " + MegaBytesReceived + " MB";
                Console.Write("\rDownload Progress: {0} MB   ", MegaBytesReceived);
            }
        }
    }
}
