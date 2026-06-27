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

    class Program
    {
        static async Task Main(string[] args)
        {
            var controllersFolder = "C:\\Users\\ProgramacionA\\Documents\\Proyectos\\Git_TvCable\\ApiOficable\\Controllers";
            var baseUrl = "http://localhost:54340";
            EndpointGenerator endpointGenerator = new EndpointGenerator();
            EndpointInspector endpointInsp = new EndpointInspector();

            endpointGenerator.Run(controllersFolder, baseUrl);
            await endpointInsp.Run();
        }

    }
}
