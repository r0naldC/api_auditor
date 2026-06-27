using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace api_auditor_v2
{
    internal class RouteInfo
    {
        public string Controller { get; set; }
        public string Action { get; set; }
        public string HttpMethod { get; set; }
        public string RouteTemplate { get; set; }
        public string FullUrl { get; set; }
        public string NeedsBody { get; set; }
    }
    class EndpointGenerator
    {
        public void Run(string controllersFolder, string baseUrl)
        {
            var files = Directory.GetFiles(controllersFolder, "*.cs", SearchOption.AllDirectories);
            var results = new List<RouteInfo>();

            foreach (var file in files)
            {
                var code = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = tree.GetCompilationUnitRoot();

                foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    var isController = cls.Identifier.Text.EndsWith("Controller");
                    if (!isController) continue;

                    var controllerRoute = GetControllerRoute(cls) ?? $"api/{cls.Identifier.Text.Replace("Controller", "")}";

                    foreach (var method in cls.Members.OfType<MethodDeclarationSyntax>())
                    {
                        var httpAttr = GetHttpAttribute(method);
                        if (httpAttr == null) continue;

                        var httpMethod = GetHttpMethod(httpAttr);
                        var methodTemplate = GetMethodRoute(httpAttr);
                        //var fullTemplate = CombineRoutes(controllerRoute, methodTemplate);
                        //var fullUrl = BuildUrl(baseUrl, fullTemplate, method.ParameterList.Parameters);

                        var controllerName = BuildControllerName(cls.Identifier.Text);
                        var fullTemplate = CombineRoutes(controllerRoute, methodTemplate);
                        var fullUrl = BuildUrl(baseUrl, controllerName, method.Identifier.Text, fullTemplate, method.ParameterList.Parameters);

                        results.Add(new RouteInfo
                        {
                            Controller = cls.Identifier.Text,
                            Action = method.Identifier.Text,
                            HttpMethod = httpMethod,
                            RouteTemplate = fullTemplate,
                            FullUrl = fullUrl,
                            NeedsBody = (httpMethod == "POST" || httpMethod == "PUT" || httpMethod == "PATCH") ? "SI" : "NO"
                        });
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Controller|Action|HttpMethod|RouteTemplate|FullUrl|NeedsBody");
            foreach (var r in results)
                sb.AppendLine($"{r.Controller}|{r.Action}|{r.HttpMethod}|{r.RouteTemplate}|{r.FullUrl}|{r.NeedsBody}");

            File.WriteAllText("rutas_generadas.txt", sb.ToString(), Encoding.UTF8);
            Console.WriteLine($"Generadas {results.Count} rutas.");
        }


        static AttributeSyntax GetHttpAttribute(MethodDeclarationSyntax method)
            => method.AttributeLists.SelectMany(a => a.Attributes)
                .FirstOrDefault(a => a.Name.ToString().StartsWith("Http"));

        static string GetHttpMethod(AttributeSyntax attr)
        {
            var name = attr.Name.ToString();
            return name.StartsWith("Http") ? name.Substring(4).ToUpperInvariant() : "GET";
        }

        static string GetMethodRoute(AttributeSyntax attr)
        {
            var args = attr.ArgumentList?.Arguments;
            if (args == null || args.Value.Count == 0) return "";
            return args.Value[0].ToString().Trim('"');
        }

        static string GetControllerRoute(ClassDeclarationSyntax cls)
        {
            foreach (var attr in cls.AttributeLists.SelectMany(a => a.Attributes))
            {
                if (!attr.Name.ToString().Contains("Route")) continue;
                var arg = attr.ArgumentList?.Arguments.FirstOrDefault()?.ToString().Trim('"');
                if (!string.IsNullOrWhiteSpace(arg)) return arg;
            }
            return null;
        }

        static string CombineRoutes(string controllerRoute, string methodRoute)
        {
            if (string.IsNullOrWhiteSpace(methodRoute)) return controllerRoute;
            return controllerRoute.TrimEnd('/') + "/" + methodRoute.TrimStart('/');
        }

        static string GetExampleValue(string name)
        {
            var n = name.ToLowerInvariant();
            if (n.Contains("id") || n.Contains("codigo") || n.Contains("cli")) return "1";
            if (n.Contains("lat")) return "18.73";
            if (n.Contains("lng") || n.Contains("lon")) return "-70.16";
            return "test";
        }

        static string BuildControllerName(string className)
        {
            return className.EndsWith("Controller")
                ? className.Substring(0, className.Length - "Controller".Length)
                : className;
        }

        static string ResolveTokens(string template, string controllerName, string actionName)
        {
            return template
                .Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase)
                .Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);
        }

        static string BuildUrl(string baseUrl, string controllerName, string actionName, string template, SeparatedSyntaxList<ParameterSyntax> parameters)
        {
            var url = ResolveTokens(template, controllerName, actionName);

            foreach (var p in parameters)
            {
                var name = p.Identifier.Text;
                var example = GetExampleValue(name);
                url = url.Replace("{" + name + "}", example);
                url = url.Replace("{" + name + "?", example);
            }

            return baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
        }
    }
}
