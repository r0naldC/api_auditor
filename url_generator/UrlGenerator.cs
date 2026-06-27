using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;


namespace url_generator
{
    class UrlGenerator
    {
        //static async Task Main(string[] args)
        //{
        //    var inputFile = args.Length > 0 ? args[0] : "urls.txt";
        //    var outputAll = "url_check_results.csv";
        //    var output200 = "url_check_results_200.csv";

        //    if (!File.Exists(inputFile))
        //    {
        //        Console.WriteLine($"No existe el archivo: {inputFile}");
        //        return;
        //    }

        //    var urls = File.ReadAllLines(inputFile)
        //        .Select(x => x.Trim())
        //        .Where(x => !string.IsNullOrWhiteSpace(x))
        //        .Distinct()
        //        .ToList();

        //    using var handler = new HttpClientHandler
        //    {
        //        AllowAutoRedirect = false,
        //        UseCookies = false
        //    };

        //    using var client = new HttpClient(handler)
        //    {
        //        Timeout = TimeSpan.FromSeconds(15)
        //    };

        //    var allLines = new List<string> { "Url,StatusCode,AccessibleWithoutToken,Notes" };
        //    var okLines = new List<string> { "Url,StatusCode,AccessibleWithoutToken,Notes" };

        //    foreach (var url in urls)
        //    {
        //        try
        //        {
        //            //using var request = new HttpRequestMessage(HttpMethod.Get, url);
        //            using var request = new HttpRequestMessage(HttpMethod.Head, url);
        //            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");

        //            using var response = await client.SendAsync(request);

        //            var status = (int)response.StatusCode;
        //            bool accessible = response.IsSuccessStatusCode;

        //            string notes =
        //                response.StatusCode == HttpStatusCode.Unauthorized ? "401 Unauthorized" :
        //                response.StatusCode == HttpStatusCode.Forbidden ? "403 Forbidden" :
        //                response.StatusCode == HttpStatusCode.Redirect ||
        //                response.StatusCode == HttpStatusCode.MovedPermanently ||
        //                response.StatusCode == HttpStatusCode.Found ? "Redirect (possible login)" :
        //                "";

        //            var line = $"{Csv(url)},{status},{accessible.ToString().ToLowerInvariant()},{Csv(notes)}";
        //            allLines.Add(line);

        //            if (status == 200)
        //                okLines.Add(line);

        //            Console.WriteLine($"{url} -> {status} {response.ReasonPhrase}");
        //        }
        //        catch (TaskCanceledException)
        //        {
        //            var line = $"{Csv(url)},TIMEOUT,false,{Csv("Timeout")}";
        //            allLines.Add(line);
        //            Console.WriteLine($"{url} -> TIMEOUT");
        //        }
        //        catch (Exception ex)
        //        {
        //            var line = $"{Csv(url)},ERROR,false,{Csv(ex.Message)}";
        //            allLines.Add(line);
        //            Console.WriteLine($"{url} -> ERROR: {ex.Message}");
        //        }
        //    }

        //    File.WriteAllLines(outputAll, allLines);
        //    File.WriteAllLines(output200, okLines);

        //    Console.WriteLine($"Generado: {outputAll}");
        //    Console.WriteLine($"Generado: {output200}");
        //}

        //static string Csv(string value)
        //    => "\"" + value.Replace("\"", "\"\"") + "\"";


        static void Main(string[] args)
        {
            string inputPath = args.Length > 0 ? args[0] : "urls.txt";
            string outputPath = args.Length > 1 ? args[1] : "urls_generadas.txt";
            string prefix = "http://localhost:50618/";

            string text = File.ReadAllText(inputPath);

            var matches = Regex.Matches(text, @"<DependentUpon>([A-Za-z0-9_\-]+\.aspx)", RegexOptions.IgnoreCase);

            var urls = matches
                .Select(m => m.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => prefix + name)
                .ToList();

            File.WriteAllLines(outputPath, urls);

            Console.WriteLine($"Generadas {urls.Count} URLs");
            Console.WriteLine($"Archivo creado: {outputPath}");
        }
    }
}


