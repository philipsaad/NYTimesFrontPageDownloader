using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using ByteSizeLib;

namespace NYTimesFrontPageDownloader
{
  internal class Program
  {
    internal static readonly HttpClient client = new HttpClient();

    internal static async Task Main()
    {
      //Set start date to start downloading low-resolution front page scans to 09/18/1851; the first date available
      DateTime lowResolutionStartDate = new DateTime(1851, 09, 18);

      //For whatever reason, the NY Times doesn't make any of its Sunday Editions prior to 04/14/1861 available.
      DateTime lowResolutionSundayStartDate = new DateTime(1861, 04, 14);

      //Set start date to start downloading high-resolution front page scans to 07/12/2012; the first date available
      DateTime highResolutionStartDate = new DateTime(2012, 07, 06);

      //Lets end today, feel free to change this to a different day
      DateTime highResolutionEndDate = DateTime.Today;

      //Use LINQ to create a list of Low Resolution Uris for every day between the start and end dates, skipping the Sundays before lowResolutionSundayStartDate
      IEnumerable<Uri> lowResolutionScans = Enumerable
        .Range(0, int.MaxValue)
        .Select(index => new DateTime?(lowResolutionStartDate.AddDays(index)))
        .TakeWhile(date => date <= highResolutionEndDate)
        .Where(x => (x.Value < lowResolutionSundayStartDate && x.Value.DayOfWeek != DayOfWeek.Sunday) || x.Value > lowResolutionSundayStartDate)
        .Select(x => new Uri($"http://www.nytimes.com/images/{x.Value.Year:D4}/{x.Value.Month:D2}/{x.Value.Day:D2}/nytfrontpage/scan.jpg"));

      //Use LINQ to create a list of High Resolution Uris for every day between the start and end dates
      IEnumerable<Uri> highResolutionScans = Enumerable
        .Range(0, int.MaxValue)
        .Select(index => new DateTime?(highResolutionStartDate.AddDays(index)))
        .TakeWhile(date => date <= highResolutionEndDate)
        .Select(x => new Uri($"http://www.nytimes.com/images/{x.Value.Year:D4}/{x.Value.Month:D2}/{x.Value.Day:D2}/nytfrontpage/scan.pdf"));

      //Combine the two lists so we only loop through one
      IEnumerable<Uri> allUris = lowResolutionScans.Concat(highResolutionScans);

      //Loop through each link and download the front page. Got rid of the WaitAll() because it was not respecting any of the TCP/IP limits I was setting and exhausting out socket pool
      foreach (Uri singleUri in allUris)
      {
        await DownloadFileAsync(singleUri);
      }
    }

    //Simple helper method to parse out the dates from the Uri so we can save them in a file naming convention that makes sense
    internal static async Task DownloadFileAsync(Uri singleLink)
    {
      //Example low resolution jpg: http://www.nytimes.com/images/1851/09/18/nytfrontpage/scan.jpg
      //Example high resolution pdf: http://www.nytimes.com/images/2017/10/02/nytfrontpage/scan.pdf
      int year = Convert.ToInt32(singleLink.Segments[2].Replace("/", ""));
      int month = Convert.ToInt32(singleLink.Segments[3].Replace("/", ""));
      int day = Convert.ToInt32(singleLink.Segments[4].Replace("/", ""));
      DateTime date = new DateTime(year, month, day);

      //Grab file extension from URI using built-in .NET classes
      string extension = Path.GetExtension(singleLink.ToString());

      string fileSavePath = $@"{date.Year}\{date.Month}\{date.Year}_{date.Month}_{date.Day}{extension}";

      //Make the call, but only read the headers
      using (HttpResponseMessage response = await client.GetAsync(singleLink))
      {
        //Make sure we have a valid response
        if (response.IsSuccessStatusCode)
        {
          //Otherwise, lets proceed with the download
          byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();

          //Create a FileInfo with our future file, this simplifies several logic checks we will be doing below
          FileInfo newFileInfo = new FileInfo(fileSavePath);

          //DirectoryInfo.Create() internally checks if the directory already exists before creating it and will silently shorcut the call.
          newFileInfo.Directory.Create();

          //Create the file on the file system and write the contents of the byte array to the file
          await File.WriteAllBytesAsync(fileSavePath, responseBytes);

          //Basic validation
          if (newFileInfo.Exists && newFileInfo.Length > 0)
          {
            Console.WriteLine($"File {fileSavePath} downloaded successfully, {ByteSize.FromBytes(newFileInfo.Length)} saved");
          }
          else
          {
            WriteError($"Error validating file {fileSavePath}");
          }
        }
        //If we run into some other HTTP status code, lets write that to the console and move on
        else
        {
          WriteError($"Error downloading file {fileSavePath}: {(int)response.StatusCode} - {response.StatusCode}");
        }
      }
    }


    internal static void WriteError(string message)
    {
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine(message);
      Console.ResetColor();
    }
  }
}