using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TvRarCleanup
{
    class Program
    {
        const string EXTRACTED_FILE = "extracted.towatch";
        const string DELETE_WHEN_WATCH_FILE = "DeleteWhenWatched.towatch";

        static string tvStorageDir = string.Empty;
        static string deletionGround = string.Empty;
        static bool previewOnly = false;

        static void Main(string[] args)
        {
            if (args.Count() < 1)
            {
                Console.WriteLine("args: starting dir, tv storage dir, [opt] preview only, deletion ground ");
                return;
            }

            var parentDirectory = args[0];
            tvStorageDir = args[1];
            previewOnly = false;
            if (args.Count() > 2)
                previewOnly = bool.Parse(args[2]);
            if (args.Count() > 3)
                deletionGround = args[3];

            foreach (var directory in Directory.GetDirectories(parentDirectory))
            {
                //check that its a tv directory, is has SxxExx
                Regex regex = new Regex("[Ss][0-9][0-9][Ee][0-9][0-9]");
                if (!regex.IsMatch(directory))
                    continue;

                //should be in a folder with rars in it and/or an AVI
                var aviFiles = Directory.GetFiles(directory, "*.avi").ToList();
                aviFiles.AddRange(Directory.GetFiles(directory, "*.mkv"));
                var rarFiles = Directory.GetFiles(directory, "*.rar");

                //check to see if we extracted it
                bool weExtractedIt = Directory.GetFiles(directory, EXTRACTED_FILE).Any();
                bool waitingToWatch = Directory.GetFiles(directory, DELETE_WHEN_WATCH_FILE).Any();

                //we have both rar files and avi files, we've probably extracted and watched the show. 
                if (rarFiles.Any() && aviFiles.Any() && !waitingToWatch)
                {
                    CleanupDir(directory, aviFiles);
                    continue;
                }

                //just an avi file in a folder
                if (!rarFiles.Any() && aviFiles.Any())
                {
                    //they've deleted the delete when watched file, time to clean it up
                    if (!waitingToWatch && weExtractedIt)
                    {
                        CleanupDir(directory, aviFiles);
                        continue;
                    }

                    //weve not been here before but files are already extracted, add a to watch file 
                    if (!waitingToWatch && !weExtractedIt)
                    {
                        AddToWatchFiles(directory);
                        continue;
                    }
                }

                //fresh meat, extract the rars 
                if (rarFiles.Any() && !aviFiles.Any())
                {
                    ExtractRars(directory, rarFiles);
                    AddToWatchFiles(directory);
                    continue;
                }
            }

            OrganizeAvis();

        }

        /// <summary>
        /// extracts all rars in a folder
        /// </summary>
        private static void ExtractRars(string directory, string[] rarFiles)
        {
            Console.WriteLine("Extracting " + directory);
            if (previewOnly)
                return;

            foreach (string rarFile in rarFiles)
            {
                var process = new Process();
                var unrarCommand = String.Format("x {0} {1}", rarFile, directory);
                var startInfo = new ProcessStartInfo(@"C:\Program Files\WinRAR\UnRAR.exe", unrarCommand);
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo = startInfo;
                process.Start();
            }
        }

        /// <summary>
        /// Adds files indicating we've extracted it and it's waiting to be watched
        /// </summary>
        private static void AddToWatchFiles(string directory)
        {
            Console.WriteLine("Ready to watch: " + directory);
            if (previewOnly)
                return;

            var deleteWhenWatched = Path.Combine(directory, DELETE_WHEN_WATCH_FILE);
            var weExtracted = Path.Combine(directory, EXTRACTED_FILE);
            if (!File.Exists(deleteWhenWatched))
                File.Create(deleteWhenWatched);
            if (!File.Exists(weExtracted))
                File.Create(weExtracted);
            File.SetAttributes(weExtracted, FileAttributes.Hidden);
        }

        /// <summary>
        /// Moves the files and deletes the directory
        /// </summary>
        private static void CleanupDir(string directory, IEnumerable<string> aviFiles)
        {
            Console.WriteLine("Cleaning " + directory);
            if (previewOnly)
                return;
            try
            {
                foreach (var aviFile in aviFiles)
                {
                    FileInfo info = new FileInfo(aviFile);
                    var destPath = Path.Combine(tvStorageDir, info.Name);
                    if (File.Exists(destPath))
                        File.Delete(aviFile);
                    else
                        File.Move(aviFile, destPath);
                }

                //either move or delete depending on if flag is set
                var directoryInfo = new DirectoryInfo(directory);
                var pathToMoveTo = Path.Combine(deletionGround, directoryInfo.Name);
                if (!string.IsNullOrEmpty(deletionGround) && !Directory.Exists(pathToMoveTo))
                    Directory.Move(directory, pathToMoveTo);
                else
                    Directory.Delete(directory, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }


        private static void OrganizeAvis()
        {
            var aviFiles = Directory.GetFiles(tvStorageDir, "*.avi").ToList();
            aviFiles.AddRange(Directory.GetFiles(tvStorageDir, "*.mkv"));
            try
            {
                foreach (var file in aviFiles)
                {
                    Regex regex = new Regex("[Ss][0-9][0-9][Ee][0-9][0-9]");
                    if (!regex.IsMatch(file))
                        continue;
                    var match = regex.Match(file);
                    string showName = file.Substring(0, match.Index - 1).Replace(".", " ");
                    string season = match.Value.Substring(0, 3);

                    string directory = Path.Combine(tvStorageDir, showName, season);

                    Console.WriteLine("Moving {0} to {1}", file, directory);

                    if (previewOnly)
                        continue;

                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var fileInfo = new FileInfo(file);
                    File.Move(file, Path.Combine(directory, fileInfo.Name));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
