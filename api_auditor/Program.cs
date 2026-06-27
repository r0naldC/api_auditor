using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace api_auditor
{
    class ResultRow
    {
        public string Controller { get; set; }
        public string Action { get; set; }
        public string HttpMethod { get; set; }
        public string Url { get; set; }
        public int? StatusCode { get; set; }
        public string Accessible { get; set; }
        public string TokenRequired { get; set; }
        public string Notes { get; set; }
        public long Ms { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            //if (args.Length < 1)
            //{
            //    Console.WriteLine("Uso: dotnet run <rutas_generadas.txt>");
            //    return;
            //}

            //var lines = await File.ReadAllLinesAsync(args[0]);
            var lines = await File.ReadAllLinesAsync("C:\\Users\\ProgramacionA\\Documents\\Proyectos\\url_tester\\endpoint_generator\\bin\\Debug\\net5.0\\rutas_generadas.txt");
            var rows = new List<ResultRow>();
            using var client = new HttpClient();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var parts = lines[i].Split('|');
                if (parts.Length < 6) continue;

                var row = new ResultRow
                {
                    Controller = parts[0],
                    Action = parts[1],
                    HttpMethod = parts[2],
                    Url = parts[4]
                };

                var sw = Stopwatch.StartNew();
                try
                {
                    using var req = new HttpRequestMessage(new HttpMethod(row.HttpMethod), row.Url);
                    var resp = await client.SendAsync(req);
                    sw.Stop();

                    row.StatusCode = (int)resp.StatusCode;
                    row.Ms = sw.ElapsedMilliseconds;
                    row.Accessible = resp.IsSuccessStatusCode ? "SI" : "NO";
                    row.TokenRequired = resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Forbidden ? "SI" : "NO";
                    row.Notes = resp.ReasonPhrase ?? "";
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    row.Ms = sw.ElapsedMilliseconds;
                    row.Accessible = "NO";
                    row.TokenRequired = "DESCONOCIDO";
                    row.Notes = ex.GetType().Name;
                }

                rows.Add(row);
            }

            using var swOut = new StreamWriter("resultados_endpoints_3.csv");
            await swOut.WriteLineAsync("Controller,Action,HttpMethod,Url,StatusCode,Accessible,TokenRequired,Notes,Ms");
            foreach (var r in rows)
            {
                await swOut.WriteLineAsync($"\"{r.Controller}\",\"{r.Action}\",\"{r.HttpMethod}\",\"{r.Url}\",\"{r.StatusCode}\",\"{r.Accessible}\",\"{r.TokenRequired}\",\"{r.Notes.Replace("\"", "'")}\",\"{r.Ms}\"");
            }

            Console.WriteLine($"CSV generado con {rows.Count} resultados.");
        }
    }
}


