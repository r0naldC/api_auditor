using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace EndpointTester
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // C# 7.3 alternative to Initialize()
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Starts your form
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private TextBox txtControllersPath = new TextBox();
        private TextBox txtBaseUrl = new TextBox();
        private TextBox txtCsvName = new TextBox();
        private Button btnBrowse = new Button();
        private Button btnRun = new Button();
        private DataGridView dgvResults = new DataGridView();
        private Label lblStatus = new Label();


        public MainForm()
        {
            Text = "Endpoint Tester";
            Width = 1200;
            Height = 800;
            StartPosition = FormStartPosition.CenterScreen;

            var lbl1 = new Label { Text = "Carpeta controladores:", Left = 20, Top = 20, Width = 180 };
            txtControllersPath.SetBounds(200, 16, 700, 25);
            btnBrowse.Text = "...";
            btnBrowse.SetBounds(910, 15, 40, 28);
            btnBrowse.Click += BtnBrowse_Click;

            var lbl2 = new Label { Text = "Base URL:", Left = 20, Top = 60, Width = 180 };
            txtBaseUrl.SetBounds(200, 56, 450, 25);
            txtBaseUrl.Text = "http://localhost:54340";

            var lbl3 = new Label { Text = "CSV salida:", Left = 20, Top = 100, Width = 180 };
            txtCsvName.SetBounds(200, 96, 450, 25);
            txtCsvName.Text = $"resultados_endpoints_tester_{DateTime.Now:M-d-mm-s}.csv";

            btnRun.Text = "Ejecutar";
            btnRun.SetBounds(200, 140, 120, 35);
            btnRun.Click += BtnRun_Click;

            lblStatus.SetBounds(340, 145, 700, 25);
            lblStatus.Text = "Listo.";

            dgvResults.SetBounds(20, 200, 1140, 540);
            dgvResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvResults.AutoGenerateColumns = true;
            dgvResults.ReadOnly = true;
            dgvResults.AllowUserToAddRows = false;

            Controls.AddRange(new Control[] { lbl1, txtControllersPath, btnBrowse, lbl2, txtBaseUrl, lbl3, txtCsvName, btnRun, lblStatus, dgvResults });
        }

        //private void BtnBrowse_Click(object? sender, EventArgs e)
        //{
        //    using var dialog = new FolderBrowserDialog
        //    {
        //        Description = "Selecciona la carpeta de controladores"
        //    };

        //    if (dialog.ShowDialog() == DialogResult.OK)
        //        txtControllersPath.Text = dialog.SelectedPath;
        //}
        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select Controllers Folder"; // Or your preferred description

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtControllersPath.Text = dialog.SelectedPath;
                }
            }
        }

        private async void BtnRun_Click(object sender, EventArgs e)
        {
            var controllersPath = txtControllersPath.Text.Trim();
            var baseUrl = txtBaseUrl.Text.Trim().TrimEnd('/');
            var csvName = txtCsvName.Text.Trim();

            if (string.IsNullOrWhiteSpace(controllersPath) || !Directory.Exists(controllersPath))
            {
                MessageBox.Show("Selecciona una carpeta válida.");
                return;
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                MessageBox.Show("Escribe una URL base válida.");
                return;
            }

            if (string.IsNullOrWhiteSpace(csvName))
                csvName = "resultados_endpoints.csv";

            btnRun.Enabled = false;
            lblStatus.Text = "Procesando...";

            try
            {
                var outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, csvName);
                var results = await EndpointAnalyzer.RunAsync(controllersPath, baseUrl, outputPath);
                dgvResults.DataSource = results;
                lblStatus.Text = $"Terminado. {results.Count} endpoints procesados.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error");
                lblStatus.Text = "Error.";
            }
            finally
            {
                btnRun.Enabled = true;
            }
        }
    }

    public static class EndpointAnalyzer
    {
        public static async Task<List<ResultRow>> RunAsync(string controllersPath, string baseUrl, string outputCsv)
        {
            var files = Directory.GetFiles(controllersPath, "*.cs", SearchOption.AllDirectories);
            var rows = new List<ResultRow>();
            var client = new HttpClient();

            foreach (var file in files)
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetCompilationUnitRoot();

                foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var controllerName = StripControllerSuffix(cls.Identifier.Text);
                    var controllerRoute = GetControllerRoute(cls) ?? "api/[controller]";

                    foreach (var method in cls.Members.OfType<MethodDeclarationSyntax>())
                    {
                        var httpAttr = GetHttpAttribute(method);
                        if (httpAttr is null) continue;

                        var httpMethod = GetHttpMethod(httpAttr);
                        var methodRoute = GetMethodRoute(httpAttr);
                        var routeTemplate = ResolveTokens(CombineRoutes(controllerRoute, methodRoute), controllerName, method.Identifier.Text);
                        var url = $"{baseUrl.TrimEnd('/')}/{routeTemplate.TrimStart('/')}";
                        var hasBody = HasBody(method, httpMethod);
                        var requiresAuthHint = HasAuthorize(cls, method);
                        var payload = hasBody ? BuildPayload(method) : null;

                        var row = new ResultRow
                        {
                            Controller = cls.Identifier.Text,
                            Action = method.Identifier.Text,
                            HttpMethod = httpMethod,
                            RouteTemplate = routeTemplate,
                            Url = url,
                            RequiresAuthHint = requiresAuthHint ? "SI" : "NO",
                            HasRouteParam = HasRouteParams(routeTemplate) ? "SI" : "NO",
                            HasBody = hasBody ? "SI" : "NO",
                            TestPayload = payload is null ? "" : Newtonsoft.Json.JsonConvert.SerializeObject(payload)
                        };

                        var sw = Stopwatch.StartNew();
                        try
                        {
                            HttpResponseMessage resp;
                            if (hasBody && payload != null)
                            {
                                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                                var req = new HttpRequestMessage(new HttpMethod(httpMethod), url) { Content = content };
                                resp = await client.SendAsync(req);
                            }
                            else
                            {
                                var req = new HttpRequestMessage(new HttpMethod(httpMethod), url);
                                resp = await client.SendAsync(req);
                            }

                            sw.Stop();
                            row.StatusCode = (int)resp.StatusCode;
                            row.Accessible = resp.IsSuccessStatusCode ? "SI" : "NO";
                            row.TokenRequired = resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                                                resp.StatusCode == System.Net.HttpStatusCode.Forbidden ? 
                                                "SI" : "NO";
                            row.Notes = resp.ReasonPhrase ?? "";
                            row.Ms = sw.ElapsedMilliseconds;
                        }
                        catch (Exception ex)
                        {
                            sw.Stop();
                            row.Accessible = "NO";
                            row.TokenRequired = "DESCONOCIDO";
                            row.Notes = ex.GetType().Name + ": " + ex.Message;
                            row.Ms = sw.ElapsedMilliseconds;
                        }

                        rows.Add(row);
                    }
                }
            }

            File.WriteAllText(outputCsv, ToCsv(rows), Encoding.UTF8);
            return rows;
        }

        static bool HasAuthorize(ClassDeclarationSyntax cls, MethodDeclarationSyntax method)
            => HasAttribute(cls, "Authorize") || HasAttribute(method, "Authorize");

        static bool HasAttribute(SyntaxNode node, string name)
            => node.DescendantNodesAndSelf().OfType<AttributeSyntax>()
                .Any(a => a.Name.ToString().IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);


        static bool HasBody(MethodDeclarationSyntax method, string httpMethod)
            => httpMethod == "POST" || httpMethod == "PUT" || httpMethod == "PATCH" ||
               method.ParameterList.Parameters.Any(p =>
                   p.AttributeLists.SelectMany(a => a.Attributes)
                       .Any(a => a.Name.ToString().IndexOf("FromBody", StringComparison.OrdinalIgnoreCase) >= 0));

        static object BuildPayload(MethodDeclarationSyntax method)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in method.ParameterList.Parameters)
            {
                var name = p.Identifier.Text;
                dict[name] = SampleValue(name, p.Type?.ToString() ?? "");
            }

            return dict.Count > 0 ? (object)dict : new { id = 1, nombre = "test", descripcion = "prueba" };
        }

        static object SampleValue(string name, string typeName)
        {
            var n = name.ToLowerInvariant();
            var t = typeName.ToLowerInvariant();

            if (t.Contains("int") || t.Contains("long") || n.Contains("id") || n.Contains("codigo")) return 1;
            if (t.Contains("decimal") || t.Contains("double") || t.Contains("float")) return 1.5;
            if (t.Contains("bool")) return true;
            if (t.Contains("datetime") || n.Contains("fecha")) return DateTime.UtcNow;
            if (n.Contains("lat")) return "18.73";
            if (n.Contains("lng") || n.Contains("lon")) return "-70.16";
            return "test";
        }

        static bool HasRouteParams(string template) => template.Contains('{') && template.Contains('}');

        static string StripControllerSuffix(string name)
            => name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - "Controller".Length)
                : name;


        static string ResolveTokens(string template, string controllerName, string actionName)
        {
            string result = Regex.Replace(template, @"\[controller\]", controllerName, RegexOptions.IgnoreCase);
            return Regex.Replace(result, @"\[action\]", actionName, RegexOptions.IgnoreCase);
        }

        static string CombineRoutes(string controllerRoute, string methodRoute)
            => string.IsNullOrWhiteSpace(methodRoute)
                ? controllerRoute
                : controllerRoute.TrimEnd('/') + "/" + methodRoute.TrimStart('/');

        static string GetControllerRoute(ClassDeclarationSyntax cls)
        {
            var attr = cls.AttributeLists.SelectMany(a => a.Attributes)
                .FirstOrDefault(a => a.Name.ToString().IndexOf("Route", StringComparison.OrdinalIgnoreCase) >= 0);
            return attr?.ArgumentList?.Arguments.FirstOrDefault()?.ToString()?.Trim('"');
        }


        static AttributeSyntax GetHttpAttribute(MethodDeclarationSyntax method)
            => method.AttributeLists.SelectMany(a => a.Attributes)
                .FirstOrDefault(a => a.Name.ToString().StartsWith("Http", StringComparison.OrdinalIgnoreCase));

        static string GetMethodRoute(AttributeSyntax attr)
            => attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"') ?? "";

        static string GetHttpMethod(AttributeSyntax attr)
        {
            var name = attr.Name.ToString();
            return name.StartsWith("Http", StringComparison.OrdinalIgnoreCase)
                ? name.Substring("Http".Length).ToUpperInvariant()
                : "GET";
        }


        static string ToCsv(List<ResultRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Controller,Action,HttpMethod,RouteTemplate,Url,RequiresAuthHint,HasRouteParam,HasBody,TestPayload,StatusCode,Accessible,TokenRequired,Notes,Ms");
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",",
                    Csv(r.Controller),
                    Csv(r.Action),
                    Csv(r.HttpMethod),
                    Csv(r.RouteTemplate),
                    Csv(r.Url),
                    Csv(r.RequiresAuthHint),
                    Csv(r.HasRouteParam),
                    Csv(r.HasBody),
                    Csv(r.TestPayload),
                    Csv(r.StatusCode?.ToString() ?? ""),
                    Csv(r.Accessible),
                    Csv(r.TokenRequired),
                    Csv(r.Notes),
                    Csv(r.Ms.ToString())));
            }
            return sb.ToString();
        }

        static string Csv(string value)
        {
            value = value ?? "";
            return $"\"{value.Replace("\"", "\"\"")}\""; // Assuming this is your escaping logic below
        }

    }

    public class ResultRow
    {
        public string Controller { get; set; } = "";
        public string Action { get; set; } = "";
        public string HttpMethod { get; set; } = "";
        public string RouteTemplate { get; set; } = "";
        public string Url { get; set; } = "";
        public string RequiresAuthHint { get; set; } = "";
        public string HasRouteParam { get; set; } = "";
        public string HasBody { get; set; } = "";
        public string TestPayload { get; set; } = "";
        public int? StatusCode { get; set; }
        public string Accessible { get; set; } = "";
        public string TokenRequired { get; set; } = "";
        public string Notes { get; set; } = "";
        public long Ms { get; set; }
    }
}
