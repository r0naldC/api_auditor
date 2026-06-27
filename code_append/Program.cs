using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace code_append
{
    class Program
    {
        static void Main()
        {
            //string rutaCarpeta = @"C:\Users\ProgramacionA\Documents\injection_test";
            string rutaCarpeta = @"C:\Users\ProgramacionA\Documents\Proyectos\Git_TvCable\TvCable\reporte";
            string nuevoUsing = "using TvCable.Helpers;";

            // Se aplican dos tabulaciones (\t\t) para empujar el código a la derecha
            string lineaInyectada = "\t\t\tUtilidadesSeguridad.ValidarSesionYPantalla(this, new Sesion());";

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

            // 1. Insertar el nuevo 'using' debajo del último using existente
            if (!contenido.Contains(nuevoUsing))
            {
                // Busca la última ocurrencia de una línea que empiece con "using " y termine con ";"
                MatchCollection matches = Regex.Matches(contenido, @"^using\s+[^;]+;", RegexOptions.Multiline);

                if (matches.Count > 0)
                {
                    // Obtiene la posición del último using encontrado
                    Match ultimoUsing = matches[matches.Count - 1];
                    int posicionInsercionUsing = ultimoUsing.Index + ultimoUsing.Length;

                    // Inserta el nuevo using en una nueva línea justo abajo
                    contenido = contenido.Insert(posicionInsercionUsing, "\r\n" + nuevoUsing);
                    huboCambios = true;
                }
                else
                {
                    // Si por alguna razón el archivo no tiene ningún using previo, lo pone al inicio
                    contenido = nuevoUsing + "\r\n" + contenido;
                    huboCambios = true;
                }
            }

            // 2. Insertar el código dentro del Page_Load con doble tabulación
            if (!contenido.Contains(lineaInyectada.Trim()))
            {
                int indiceFirma = contenido.IndexOf(firmaPageLoad);
                if (indiceFirma != -1)
                {
                    // Busca la llave '{' que abre el cuerpo del Page_Load
                    int indiceLlave = contenido.IndexOf("{", indiceFirma);
                    if (indiceLlave != -1)
                    {
                        // Inserta justo después de la llave '{'
                        int posicionInsercionCodigo = indiceLlave + 1;
                        contenido = contenido.Insert(posicionInsercionCodigo, "\r\n" + lineaInyectada);
                        huboCambios = true;
                    }
                }
                else
                {
                    Console.WriteLine($"[-] Saltado (No tiene Page_Load): {Path.GetFileName(ruta)}");
                    if (huboCambios)
                    {
                        // Si se agregó el using pero no tenía Page_Load, guardamos el avance del using
                        File.WriteAllText(ruta, contenido, Encoding.UTF8);
                    }
                    return;
                }
            }

            // Guardar en el disco solo si el archivo realmente fue modificado
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