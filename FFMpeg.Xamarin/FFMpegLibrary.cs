using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Threading.Tasks;

namespace FFMpeg.Xamarin
{
    public class FFMpegLibrary
    {

        public string CDNHost { get; set; } = "cdn.rawgit.com";


        public static FFMpegLibrary Instance = new FFMpegLibrary();

        private bool _initialized = false;

        private Java.IO.File ffmpegFile;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Init(
            Context context, 
            string cdn = null, 
            string downloadTitle = null, 
            string downloadMessage = null)
        {
            if (_initialized)
                return;

            if (cdn != null) {
                CDNHost = cdn;
            }

            // do all initialization...
            var filesDir = context.FilesDir;

            ffmpegFile = new Java.IO.File(filesDir + "/ffmpeg");

            FFMPEGSource source = FFMPEGSource.Get();

            if (ffmpegFile.Exists())
            {

                if (source.IsHashMatch(System.IO.File.ReadAllBytes(ffmpegFile.CanonicalPath)))
                {
                    _initialized = true;
                    return;
                }

                // file is not same...
                
                // delete the file...

                if (ffmpegFile.CanExecute())
                    ffmpegFile.SetExecutable(false);
                ffmpegFile.Delete();
                System.Diagnostics.Debug.WriteLine($"ffmpeg file deleted at {ffmpegFile.AbsolutePath}");
            }

            // lets try to download

            using (var c = new System.Net.Http.HttpClient())
            {
                using (var fout = System.IO.File.OpenWrite(ffmpegFile.AbsolutePath))
                {
                    var s = await c.GetStreamAsync(source.Url);

                    var dlg = ProgressDialog.Show(
                        context, 
                        downloadTitle ?? "Downloading", 
                        downloadMessage ?? "Downloading Video Converter");

                    var buffer = new byte[5120];

                    long total = s.Length;

                    int count = 0;

                    int progress = 0;

                    dlg.Max = (int)total;

                    while ((count = await s.ReadAsync(buffer, 0, buffer.Length)) > 0) {

                        await fout.WriteAsync(buffer, 0, count);

                        progress += count;

                        dlg.Progress = progress;

                    }

                    dlg.Hide();


                    
                    
                }
            }

            System.Diagnostics.Debug.WriteLine($"ffmpeg file copied at {ffmpegFile.AbsolutePath}");

            if (!ffmpegFile.CanExecute())
            {
                ffmpegFile.SetExecutable(true);
                System.Diagnostics.Debug.WriteLine($"ffmpeg file made executable");
            }

            _initialized = true;
        }

        /// <summary>
        /// This must be called from main ui thread only...
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cmd"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static async Task<int> Run(Context context, string cmd, Action<string> logger = null) {

            TaskCompletionSource<int> source = new TaskCompletionSource<int>();

            
            
            await Instance.Init(context);

            await Task.Run(() => {
                try {


                    int n = _Run(context, cmd, logger);
                    source.SetResult(n);
                } catch (Exception ex) {
                    source.SetException(ex);
                }
            });

            return await source.Task;
        }

        private static int _Run(
            Context context,
            string cmd,
            Action<string> logger = null)
        {

            TaskCompletionSource<int> task = new TaskCompletionSource<int>();



            System.Diagnostics.Debug.WriteLine($"ffmpeg initialized");

            //var process = Java.Lang.Runtime.GetRuntime().Exec( Instance.ffmpegFile.CanonicalPath + " " + cmd );

            var startInfo = new System.Diagnostics.ProcessStartInfo(Instance.ffmpegFile.CanonicalPath, cmd);

            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            var process = new System.Diagnostics.Process();

            process.StartInfo = startInfo;


            bool finished = false;

            string error = null;

            process.Start();


            Task.Run(() =>
            {
                using (var reader = process.StandardError)
                {
                    string processOutput = "";
                    do
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                            break;
                        logger?.Invoke(line);
                        processOutput += line;
                    } while (!finished);
                    error = processOutput;
                }
            });

            process.WaitForExit();

            return process.ExitCode;


        }


    }


    public class FFMPEGSource {


        public static FFMPEGSource Get() {

            string osArchitecture = Java.Lang.JavaSystem.GetProperty("os.arch");

            foreach (var source in Sources) {
                if (source.IsArch(osArchitecture))
                    return source;
            }

            throw new NotImplementedException();
        }



        


        public FFMPEGSource(string arch, Func<string,bool> isArch, string hash)
        {
            this.Arch = arch;
            this.IsArch = isArch;
            this.Hash = hash;
        }

        public static string FFMPEGVersion { get;  } = "3.0.1";

        public static FFMPEGSource[] Sources = new FFMPEGSource[] {
            new FFMPEGSource("arm", x=> x.Contains("arm"), "4nzzxDKxIYlvyK8tFH7/iNMHTdU="),
            new FFMPEGSource("x86", x=> x.EndsWith("86"), "DdTbrTBf8Zeh6p5hWL0ggdIp5w4=")
        };

        public string Arch { get;  }

        public string Hash { get; }

        public string Url => $"https://{FFMpegLibrary.Instance.CDNHost}/neurospeech/xamarin-android-ffmpeg/binary/{FFMPEGVersion}/{Arch}/ffmpeg";

        public Func<string, bool> IsArch { get;  }

        public bool IsHashMatch(byte[] data) {
            var sha = System.Security.Cryptography.SHA1.Create();
            string h = Convert.ToBase64String(sha.ComputeHash(data));
            return h == Hash;
        }

    }
}