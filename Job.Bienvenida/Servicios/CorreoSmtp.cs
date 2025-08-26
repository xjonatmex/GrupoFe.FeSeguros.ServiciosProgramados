using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace Job.Bienvenida.Servicios
{
    public sealed class CorreoSmtp
    {
        private readonly IConfiguration _cfg;
        public CorreoSmtp(IConfiguration cfg) => _cfg = cfg;

        public async Task EnviarBienvenidaAsync(string para, string nombre, byte[] pdf, string nombrePdf, string cartaPngRutaRelativa)
        {
            var c = _cfg.GetSection("Correo");
            var asunto = $"¡Bienvenid@ {nombre} a FeSeguro Familia!";
            var html = $@"<div style='font-family:Segoe UI,Arial'>

         <a href='https://m.facebook.com/Confiaenfeseguros/' target='_blank' rel='noopener'>
         <img src='cid:carta' alt='Carta' style='max-width:100%;height:auto;border:0;display:block'/>
            </a>            <p>Adjuntamos tu cronograma de pagos.</p></div>";

            using var msg = new MailMessage
            {
                From = new MailAddress(c["De"]!, c["NombreDe"]),
                Subject = asunto,
                Body = html,
                IsBodyHtml = true
            };
            msg.To.Add(para);
            if (!string.IsNullOrWhiteSpace(c["CC"])) msg.CC.Add(c["CC"]!);

            var htmlView = AlternateView.CreateAlternateViewFromString(html, System.Text.Encoding.UTF8, MediaTypeNames.Text.Html);
            var cartaFullPath = Path.Combine(AppContext.BaseDirectory, _cfg["Rutas:Carta"]!);
            var carta = new LinkedResource(cartaFullPath, "image/png") { ContentId = "carta", TransferEncoding = TransferEncoding.Base64 };
            htmlView.LinkedResources.Add(carta);
            msg.AlternateViews.Add(htmlView);

            msg.Attachments.Add(new Attachment(new MemoryStream(pdf), nombrePdf, "application/pdf"));

            using var smtp = new SmtpClient(c["Host"], int.Parse(c["Puerto"]!))
            {
                EnableSsl = bool.Parse(c["Ssl"]!),
                Credentials = new NetworkCredential(c["Usuario"], c["Clave"])
            };
            await Task.Run(() => smtp.Send(msg));
        }
    }
}
