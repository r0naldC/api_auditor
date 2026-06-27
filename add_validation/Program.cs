using System;
using System.IO;
using System.Text;

namespace add_validation
{
    class Program
    {
        static void Main()
        {
            // 1. CONFIGURACIÓN: Cambia estos valores con tus datos reales
            string rutaCarpeta = @"C:\Users\ProgramacionA\Documents\injection_test";
            string nuevoUsing = "using TvCable.Helpers;";
            string lineaInyectada = "        // Código inyectado\n          UtilidadesSeguridad.ValidarSesionYPantalla(this, new Sesion());";

            // Firma del evento que buscará el script
            string firmaPageLoad = "protected void Page_Load(object sender, EventArgs e)";

            try
            {
                if (!Directory.Exists(rutaCarpeta))
                {
                    Console.WriteLine("La carpeta especificada no existe.");
                    return;
                }

                string[] archivos = Directory.GetFiles(rutaCarpeta, "*.aspx.cs", SearchOption.AllDirectories);
                Console.WriteLine($"Se encontraron {archivos.Length} archivos. Iniciando proceso...\n");

                foreach (string archivo in archivos)
                {
                    ModificarArchivo(archivo, nuevoUsing, firmaPageLoad, lineaInyectada);
                }

                Console.WriteLine("\n¡Proceso finalizado con éxito!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general en la aplicación: {ex.Message}");
            }
        }

        static void ModificarArchivo(string ruta, string nuevoUsing, string firmaPageLoad, string lineaInyectada)
        {
            string contenido = File.ReadAllText(ruta, Encoding.UTF8);
            bool huboCambios = false;

            // 1. Insertar el 'using' al principio del todo (fuera de namespace y clase)
            if (!contenido.Contains(nuevoUsing))
            {
                // Se agrega al inicio seguido de dos saltos de línea para mantener ordenado el código
                contenido = nuevoUsing + "\r\n" + contenido;
                huboCambios = true;
            }

            // 2. Insertar el código dentro del Page_Load
            if (!contenido.Contains(lineaInyectada.Trim()))
            {
                int indiceFirma = contenido.IndexOf(firmaPageLoad);
                if (indiceFirma != -1)
                {
                    // Busca la llave '{' que abre el cuerpo del Page_Load
                    int indiceLlave = contenido.IndexOf("{", indiceFirma);
                    if (indiceLlave != -1)
                    {
                        // Inserta justo después de la llave '{' abriendo un salto de línea
                        int posicionInsercion = indiceLlave + 1;
                        contenido = contenido.Insert(posicionInsercion, "\r\n" + lineaInyectada);
                        huboCambios = true;
                    }
                }
                else
                {
                    Console.WriteLine($"[-] Saltado (No tiene Page_Load): {Path.GetFileName(ruta)}");
                    if (huboCambios)
                    {
                        // Si se agregó el using pero no tenía Page_Load, guardamos solo el using
                        File.WriteAllText(ruta, contenido, Encoding.UTF8);
                    }
                    return;
                }
            }

            // Guardar en el disco solo si el archivo realmente requería cambios
            if (huboCambios)
            {
                File.WriteAllText(ruta, contenido, Encoding.UTF8);
                Console.WriteLine($"[+] Modificado correctamente: {Path.GetFileName(ruta)}");
            }
            else
            {
                Console.WriteLine($"[.] Sin cambios (Ya modificado previamente): {Path.GetFileName(ruta)}");
            }
        }

    }
}