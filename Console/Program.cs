using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace NYTimesFrontPageDownloader
{
    class Program
    {
        static HttpClient client = new HttpClient();

        static void Main(string[] args)
        {
            //Set start date to start downloading low-resolution front page scans to 09/18/1851; the first date available
            var lowResolutionStartDate = new DateTime(1851, 09, 18);

            ////Set start date to start downloading high-resolution front page scans to 07/12/2012; the first date available
            var highResolutionStartDate = new DateTime(2012, 07, 06);

            //Lets end today, feel free to change this to a different day
            var highResolutionEndDate = DateTime.Today;

            //Use LINQ to create a list of Uris for every day between the start and end dates
            var allLowResolutionScans = Enumerable
              .Range(0, int.MaxValue)
              .Select(index => new DateTime?(lowResolutionStartDate.AddDays(index)))
              .TakeWhile(date => date <= highResolutionEndDate)
              .Select(x => new Uri($"http://www.nytimes.com/images/{x.Value.Year:D4}/{x.Value.Month:D2}/{x.Value.Day:D2}/nytfrontpage/scan.jpg"));

            //Use LINQ to create a list of Uris for every day between the start and end dates
            var allHighResolutionScans = Enumerable
              .Range(0, int.MaxValue)
              .Select(index => new DateTime?(highResolutionStartDate.AddDays(index)))
              .TakeWhile(date => date <= highResolutionEndDate)
              .Select(x => new Uri($"http://www.nytimes.com/images/{x.Value.Year:D4}/{x.Value.Month:D2}/{x.Value.Day:D2}/nytfrontpage/scan.pdf"));

            //Combine the two lists so we only loop through one
            var allUris = allLowResolutionScans.Concat(allHighResolutionScans);

            ServicePointManager.DefaultConnectionLimit = 1024;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.ReusePort = true;

            //Loop through each link and download the front page
            Parallel.ForEach(allUris, new ParallelOptions { MaxDegreeOfParallelism = 8 }, singleLink => {
                Task.WaitAll(nyTimesHelper(singleLink));
            });
        }

        //Simple helper method to parse out the dates from the Uri so we can save them in a file naming convention that makes sense
        static async Task nyTimesHelper(Uri singleLink)
        {
            //Example low resolution jpg: http://www.nytimes.com/images/1851/09/18/nytfrontpage/scan.jpg
            //Example high resolution pdf: http://www.nytimes.com/images/2017/10/02/nytfrontpage/scan.pdf
            var year = singleLink.Segments[2].Replace("/", "");
            var month = singleLink.Segments[3].Replace("/", "");
            var day = singleLink.Segments[4].Replace("/", "");
            var extension = singleLink.Segments[6].Split('.').Last();

            //Download file in year\month\year_month_day.ext format
            await downloadFileAsync(singleLink, $"{year}\\{month}\\{year}_{month}_{day}.{extension}");
        }

        //Download the file asynchronously and write the result to the console
        static async Task downloadFileAsync(Uri singleLink, string fileSavePath)
        {
            //Use HttpRequest instead of WebClient for concurrency
            var request = new HttpRequestMessage(HttpMethod.Get, singleLink);

            //Make the call, but only read the headers
            var sendTask = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            var response = sendTask.Result;

            //If we cannot find the file on the server, lets write that to the console and move on
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Error downloading file {fileSavePath}: 404 - File Not Found");
            }
            else
            {
                //Otherwise, lets proceed with the download
                var httpStream = await response.Content.ReadAsStreamAsync();

                //Check to see if the directory in the file path exists
                if (!Directory.Exists(fileSavePath))
                {
                    //If not, create it
                    Directory.CreateDirectory(new FileInfo(fileSavePath).DirectoryName);
                }

                //Create the file on the file system and prep it for saving
                using (var fileStream = File.Create(fileSavePath, 4096, FileOptions.Asynchronous))
                {
                    using (var reader = new StreamReader(httpStream))
                    {
                        {
                            //Write the contents of the filesteam to the file
                            httpStream.CopyTo(fileStream);
                            fileStream.Flush();
                        }
                    }
                }

                //Basic validation
                if ((new FileInfo(fileSavePath).Exists) && new FileInfo(fileSavePath).Length > 0)
                {
                    Console.WriteLine($"File {fileSavePath} downloaded successfully, {GetBytesReadable(new FileInfo(fileSavePath).Length)} saved");
                }
                else
                {
                    Console.WriteLine($"Error validating file {fileSavePath}");
                }
            }

        }

        // Returns the human-readable file size for an arbitrary, 64-bit file size 
        // The default format is "0.### XB", e.g. "4.2 KB" or "1.434 GB"
        // Copied from https://stackoverflow.com/a/11124118
        static string GetBytesReadable(long i)
        {
            // Get absolute value
            long absolute_i = (i < 0 ? -i : i);
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);

            // Return formatted number with suffix
            return readable.ToString("0.## ") + suffix;
        }
    }
}