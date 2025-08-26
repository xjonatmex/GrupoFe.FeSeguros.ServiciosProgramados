using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Job.Bienvenida.Modelos
{
    public sealed class Pendiente
    {
        public string NroSolicitud { get; init; } = "";
        public string NombreCliente { get; init; } = "";
        public string Correo { get; init; } = "";
    }
}
