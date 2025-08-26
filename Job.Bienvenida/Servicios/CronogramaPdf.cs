using Dapper;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.tool.xml;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;
using System.Text;

namespace Job.Bienvenida.Servicios
{
    public sealed class CronogramaPdf
    {
        private readonly string _cs;
        public CronogramaPdf(string cadenaConexion) => _cs = cadenaConexion;

        // ==== MODELO que mapea lo que retorna el SP ====
        private sealed class CronoRow
        {
            public string num_solicitud { get; set; } = "";
            public string num_poliza { get; set; } = "";
            public int nrocuota { get; set; }
            public int diasatraso { get; set; }
            public string fecvencuo { get; set; } = "";
            public decimal mtocuota { get; set; }
            public decimal mtoigvcuo { get; set; }
            public decimal totalcuota { get; set; }
            public string nrodocumento { get; set; } = "";
            public string nombres { get; set; } = "";
            public string s_estadocuo { get; set; } = "";
            public int numcuotas { get; set; }
            public string tipoplan { get; set; } = "";
            public string prima { get; set; } = "";
            public string moneda { get; set; } = "";
            public string tipopago { get; set; } = "";
            public string? fechapago { get; set; }

            public string fecinivig { get; set; } = "";
            public string fecfinvig { get; set; } = "";

            public int cod_ret_out { get; set; }
            public int cod_ope_out { get; set; }
            public string msg_ope_out { get; set; } = "";
        }

        public async Task<byte[]> GenerarAsync(
            string nroSolicitud,
            string rutaPlantillaRel,
            string rutaLogoRel,
            string dirSalidaRel)
        {
            // 1) Rutas absolutas basadas en el ejecutable
            var baseDir = AppContext.BaseDirectory;
            var rutaPlant = Path.Combine(baseDir, rutaPlantillaRel);
            var rutaLogo = Path.Combine(baseDir, rutaLogoRel);
            var dirSalida = Path.Combine(baseDir, dirSalidaRel);
            Directory.CreateDirectory(dirSalida);

            // 2) Traer cronograma desde el SP
            var cronograma = await ObtenerCronogramaAsync(nroSolicitud);
            if (cronograma.Count == 0)
                throw new InvalidOperationException($"No hay datos de cronograma para {nroSolicitud}.");

            // 3) Armar HTML desde la plantilla (reemplazos)
            var html = ConstruirHtmlDesdePlantilla(rutaPlant, rutaLogo, cronograma);

            // 4) Generar PDF con iTextSharp (XMLWorker)
            var nombrePdf = $"{DateTime.Now:ddMMyyyyHHmmss}.pdf";
            var rutaPdf = Path.Combine(dirSalida, nombrePdf);

            using (var fs = new FileStream(rutaPdf, FileMode.Create, FileAccess.Write))
            {
                using var pdfDoc = new iTextSharp.text.Document(PageSize.A4, 25, 25, 25, 25);
                var writer = PdfWriter.GetInstance(pdfDoc, fs);
                pdfDoc.Open();

                // Logo (opcional): si tu plantilla ya lo tiene como <img>, puedes omitir esto.
                if (File.Exists(rutaLogo))
                {
                    var logoImg = iTextSharp.text.Image.GetInstance(rutaLogo);
                    logoImg.SetAbsolutePosition(20, 770);
                    logoImg.ScalePercent(10);
                    pdfDoc.Add(logoImg);
                }

                using var sr = new StringReader(html);
                XMLWorkerHelper.GetInstance().ParseXHtml(writer, pdfDoc, sr); // ← sin Encoding
                pdfDoc.Close();
            }

            return await File.ReadAllBytesAsync(rutaPdf);
        }

        // ================== PRIVADOS ==================

        private async Task<List<CronoRow>> ObtenerCronogramaAsync(string nroSolicitud)
        {
            using var cn = new SqlConnection(_cs);
            var filas = await cn.QueryAsync<CronoRow>(
                "sp_list_PolizaCronograma",
                new { @as_num_solicitud = nroSolicitud },
                commandType: CommandType.StoredProcedure);
            return filas.AsList();
        }

        private static string ConstruirHtmlDesdePlantilla(string rutaPlantilla, string rutaLogo, List<CronoRow> data)
        {
            var plantilla = File.ReadAllText(rutaPlantilla);

            // Acumuladores y cabecera
            decimal total1 = 0, total2 = 0, total3 = 0;
            string sCliente = "", sProducto = "", sMonto = "", sMoneda = "";
            int iPlazo = 0;

            var sbFilas = new StringBuilder();
            foreach (var it in data)
            {
                var fPago = string.IsNullOrWhiteSpace(it.fechapago) ? "" : it.fechapago;
                sbFilas.AppendLine("<tr>");
                sbFilas.AppendLine($"<td>{it.nrocuota}</td>");
                sbFilas.AppendLine($"<td align='center'>{it.fecvencuo}</td>");
                sbFilas.AppendLine($"<td align='right'>{it.mtocuota.ToString("#,###.00", CultureInfo.InvariantCulture)}</td>");
                sbFilas.AppendLine($"<td align='right'>{it.mtoigvcuo.ToString("#,###.00", CultureInfo.InvariantCulture)}</td>");
                sbFilas.AppendLine($"<td align='right'>{it.totalcuota.ToString("#,###.00", CultureInfo.InvariantCulture)}</td>");
                sbFilas.AppendLine($"<td align='center'>{it.s_estadocuo}</td>");
                sbFilas.AppendLine($"<td align='center'>{it.diasatraso}</td>");
                sbFilas.AppendLine($"<td align='center'>{fPago}</td>");
                sbFilas.AppendLine("</tr>");

                total1 += it.mtocuota;
                total2 += it.mtoigvcuo;
                total3 += it.totalcuota;

                sCliente = $"{it.nrodocumento} - {it.nombres}";
                sProducto = it.tipoplan;
                sMoneda = it.moneda;
                iPlazo = it.numcuotas;
                sMonto = total3.ToString("#,###.00", CultureInfo.InvariantCulture); // total acumulado
            }

            // Reemplazos (iguales a tu lógica)
            plantilla = plantilla.Replace("@CLIENTE", sCliente)
                                 .Replace("@FECHA", DateTime.Now.ToString("dd/MM/yyyy"))
                                 .Replace("@PRODUCTO", sProducto)
                                 .Replace("@HORA", DateTime.Now.ToString("HH:mm:ss"))
                                 .Replace("@MONTOSOLES", $"{sMonto} {sMoneda}")
                                 .Replace("@PLAZO", $"{iPlazo} meses")
                                 .Replace("@FILAS", sbFilas.ToString())
                                 .Replace("@TOTAL1", total1.ToString("#,###.00", CultureInfo.InvariantCulture))
                                 .Replace("@TOTAL2", total2.ToString("#,###.00", CultureInfo.InvariantCulture))
                                 .Replace("@TOTAL3", total3.ToString("#,###.00", CultureInfo.InvariantCulture));

            // Si tu plantilla tiene @LOGO, lo resolvemos a file:// ruta absoluta
            if (plantilla.Contains("@LOGO"))
            {
                var logoUrl = $"file:///{rutaLogo.Replace("\\", "/")}";
                plantilla = plantilla.Replace("@LOGO", logoUrl);
            }

            return plantilla;
        }
    }
}
