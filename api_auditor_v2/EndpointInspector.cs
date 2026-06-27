using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace api_auditor_v2
{
    internal class ResultRow
    {
        public string Controller { get; set; }
        public string Action { get; set; }
        public string HttpMethod { get; set; }
        public string Url { get; set; }
        public string RequestBody { get; set; }
        public int? StatusCode { get; set; }
        public string Accessible { get; set; }
        public string TokenRequired { get; set; }
        public string Notes { get; set; }
        public long Ms { get; set; }
    }

    class EndpointInspector
    {
        //const string RutasExtraidas = "C:\\Users\\ProgramacionA\\Documents\\Proyectos\\url_tester\\endpoint_generator\\bin\\Debug\\net5.0\\rutas_generadas.txt";
        const string RutasExtraidas = "rutas_generadas.txt";
    
        public async Task Run()
        {
            var lines = await File.ReadAllLinesAsync(RutasExtraidas);
            var rows = new List<ResultRow>();
            var endpoints_accesibles = new List<ResultRow>();
            using var client = new HttpClient();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                var parts = lines[i].Split('|');
                if (parts.Length < 6) continue;

                var httpMethod = parts[2].Trim().ToUpperInvariant();
                var url = parts[4].Trim();
                var needsBody = parts[5].Trim().Equals("SI", StringComparison.OrdinalIgnoreCase);

                var row = new ResultRow
                {
                    Controller = parts[0],
                    Action = parts[1],
                    HttpMethod = httpMethod,
                    Url = url
                };

                var sw = Stopwatch.StartNew();

                try
                {
                    HttpResponseMessage resp;

                    if (needsBody && (httpMethod == "POST" || httpMethod == "PUT" || httpMethod == "PATCH"))
                    {
                        var bodyObj = BuildTestBody(parts[0], parts[1], url);
                        var json = JsonSerializer.Serialize(bodyObj);
                        row.RequestBody = json;

                        using var content = new StringContent(json, Encoding.UTF8, "application/json");
                        using var req = new HttpRequestMessage(new HttpMethod(httpMethod), url)
                        {
                            Content = content
                        };

                        resp = await client.SendAsync(req);
                    }
                    else
                    {
                        using var req = new HttpRequestMessage(new HttpMethod(httpMethod), url);
                        resp = await client.SendAsync(req);
                    }

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
                    row.Notes = ex.GetType().Name + ": " + ex.Message;
                }

                rows.Add(row);
                if (row.Accessible == "SI") endpoints_accesibles.Add(row);
            }

            using var swOut = new StreamWriter($"reporte_endpoints_{DateTime.Now:M-dd-m-ss}.csv");
            await swOut.WriteLineAsync("Controller,Action,HttpMethod,Url,RequestBody,StatusCode,Accessible,TokenRequired,Notes,Ms");
            foreach (var r in rows)
            {
                await swOut.WriteLineAsync(
                    $"\"{Escape(r.Controller)}\",\"{Escape(r.Action)}\",\"{Escape(r.HttpMethod)}\",\"{Escape(r.Url)}\",\"{Escape(r.RequestBody)}\",\"{r.StatusCode}\",\"{r.Accessible}\",\"{r.TokenRequired}\",\"{Escape(r.Notes)}\",\"{r.Ms}\"");
            }
            using var accesibleOut = new StreamWriter($"endpoints_accesibles_{DateTime.Now:M-dd-m-ss}.csv");
            await accesibleOut.WriteLineAsync("Controller,Action,HttpMethod,Url,RequestBody,StatusCode,Accessible,TokenRequired,Notes,Ms");
            foreach (var r in endpoints_accesibles)
            {
                await accesibleOut.WriteLineAsync(
                    $"\"{Escape(r.Controller)}\",\"{Escape(r.Action)}\",\"{Escape(r.HttpMethod)}\",\"{Escape(r.Url)}\",\"{Escape(r.RequestBody)}\",\"{r.StatusCode}\",\"{r.Accessible}\",\"{r.TokenRequired}\",\"{Escape(r.Notes)}\",\"{r.Ms}\"");
            }

            Console.WriteLine($"CSV generado con {rows.Count} resultados.");
            Console.WriteLine($"Endpoints Accesibles: {endpoints_accesibles.Count} .");
        }

        static object BuildTestBody(string controller, string action, string url)
        {
            if (controller.Contains("Cobrador", StringComparison.OrdinalIgnoreCase) &&
                action.Contains("CrearAveria", StringComparison.OrdinalIgnoreCase))
            {
                return new
                {
                    Codigo_cli = 1,
                    TipoServicio = "test",
                    Comentario = "prueba automatica",
                    persona_reporta = "tester",
                    origen = "script"
                };
            }

            return new
            {
                id = 1,
                codigo = 1,
                nombre = "test",
                descripcion = "prueba automatica",
                comentario = "test"
            };
        }

        static string Escape(string value)
            => (value ?? "").Replace("\"", "\"\"");

    }
}
