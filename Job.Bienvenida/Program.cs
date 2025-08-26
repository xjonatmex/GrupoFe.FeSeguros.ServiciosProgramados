using Job.Bienvenida.Datos;
using Job.Bienvenida.Servicios;
using Microsoft.Extensions.Configuration;

var cfg = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var conexion = cfg["Conexion"]!;
var rutas = cfg.GetSection("Rutas");
var repo = new PendientesRepo(conexion);
var pdfSrv = new CronogramaPdf(conexion); // <-- pasa la cadena aquí
var mail = new CorreoSmtp(cfg);

var pendientes = await repo.ListarPendientesAsync();
foreach (var p in pendientes)
{
    try
    {
        var pdf = await pdfSrv.GenerarAsync(
            p.NroSolicitud,
            rutas["PlantillaCronograma"]!,
            rutas["Logo"]!,
            rutas["Salida"]!);

        await mail.EnviarBienvenidaAsync(
            p.Correo,
            p.NombreCliente,
            pdf,
            "Cronograma_de_pagos.pdf",
            rutas["Carta"]!
        );

        await repo.MarcarEnviadoAsync(p.NroSolicitud);
        Console.WriteLine($"OK → {p.NroSolicitud} → {p.Correo}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ERROR → {p.NroSolicitud}: {ex.Message}");
    }
}

Console.WriteLine("Proceso finalizado.");
