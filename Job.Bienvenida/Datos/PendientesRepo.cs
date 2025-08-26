using Dapper;
using Job.Bienvenida.Modelos;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Job.Bienvenida.Datos
{
    public sealed class PendientesRepo
    {
        private readonly string _cs;
        public PendientesRepo(string cadenaConexion) => _cs = cadenaConexion;

        public async Task<IReadOnlyList<Pendiente>> ListarPendientesAsync()
        {
            using var cn = new SqlConnection(_cs);
            var rows = await cn.QueryAsync<Pendiente>(
                "dbo.sp_list_correo_pendiente",
                commandType: CommandType.StoredProcedure);
            return rows.AsList();
        }

        public async Task MarcarEnviadoAsync(string nroSolicitud)
        {
            using var cn = new SqlConnection(_cs);
            await cn.ExecuteAsync("dbo.sp_upd_NotificadoEmail",
             new { num_solicitud = nroSolicitud, flg_NotificadoEmail = true },
             commandType: CommandType.StoredProcedure);
        }
    }
}
