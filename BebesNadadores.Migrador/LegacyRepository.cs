using MySqlConnector;

namespace BebesNadadores.Migrador;

public sealed class LegacyRepository(string connectionString)
{
    private MySqlConnection Open() => new(connectionString);

    public async Task<List<LegacyPersona>> PersonasAsync(CancellationToken ct)
    {
        const string sql = """
SELECT IDPERSONA, TRIM(apenom), TRIM(domicilio), TRIM(telefijo), TRIM(teleceo), TRIM(correoe),
       FECHANAC, TRIM(observacion), TRIM(telecelp), TRIM(telecelm), ESTADO, FECHAINS
FROM persona WHERE IDTIPOPERSONA = 2;
""";
        var list = new List<LegacyPersona>();
        await using var c = Open(); await c.OpenAsync(ct); await using var cmd = new MySqlCommand(sql,c); await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new(r.GetInt64(0), S(r,1), S(r,2), S(r,3), S(r,4), S(r,5), D(r,6), S(r,7), S(r,8), S(r,9), r.GetBoolean(10), DT(r,11)));
        return list;
    }

    // Recupera solo IDs con movimiento en 2026 que no eran IDTIPOPERSONA=2.
    // El tipo 1 se audita; no se altera el padrón de origen.
    public async Task<List<LegacyPersona>> PersonasAdicionalesAsync(IEnumerable<long> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToArray();
        var list = new List<LegacyPersona>();
        await using var c = Open();
        await c.OpenAsync(ct);
        foreach (var batch in wanted.Chunk(200))
        {
            await using var cmd = c.CreateCommand();
            var placeholders = new List<string>();
            for (var i = 0; i < batch.Length; i++)
            {
                var key = $"@id{i}";
                placeholders.Add(key);
                cmd.Parameters.AddWithValue(key, batch[i]);
            }
            cmd.CommandText = $"""
SELECT IDPERSONA, TRIM(apenom), TRIM(domicilio), TRIM(telefijo), TRIM(teleceo), TRIM(correoe),
       FECHANAC, TRIM(observacion), TRIM(telecelp), TRIM(telecelm), ESTADO, FECHAINS
FROM persona
WHERE IDTIPOPERSONA = 1 AND IDPERSONA IN ({string.Join(",", placeholders)});
""";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new(r.GetInt64(0), S(r,1), S(r,2), S(r,3), S(r,4), S(r,5), D(r,6), S(r,7), S(r,8), S(r,9), r.GetBoolean(10), DT(r,11)));
        }
        return list;
    }

    public async Task<List<LegacyResponsable>> ResponsablesAsync(CancellationToken ct)
    {
        const string sql = "SELECT idalumno, TRIM(nombre), TRIM(dni) FROM alumnofactura WHERE idalumno IS NOT NULL;";
        var list = new List<LegacyResponsable>();
        await using var c = Open(); await c.OpenAsync(ct); await using var cmd = new MySqlCommand(sql,c); await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0), S(r,1), S(r,2)));
        return list;
    }

    public async Task<List<LegacyClase>> ClasesAsync(int desde, int hasta, DateOnly? corte, CancellationToken ct)
    {
        var sql = """
SELECT IDHPC, PRESENTE, FECHACLASE, TRIM(dia), TRIM(horaturno), TRIM(nombreturno), IDPERSONA
FROM historicopersonaclase
WHERE YEAR(FECHACLASE) BETWEEN @desde AND @hasta
""" + (corte is null ? "" : " AND FECHACLASE < @hastaExclusivo") + ";";
        var list = new List<LegacyClase>();
        await using var c = Open(); await c.OpenAsync(ct); await using var cmd = new MySqlCommand(sql,c);
        cmd.Parameters.AddWithValue("@desde", desde); cmd.Parameters.AddWithValue("@hasta", hasta); if (corte is not null) cmd.Parameters.AddWithValue("@hastaExclusivo", corte.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0), S(r,1), DateOnly.FromDateTime(r.GetDateTime(2)), S(r,3), S(r,4), S(r,5), r.GetInt64(6)));
        return list;
    }

    public async Task<List<LegacyPago>> PagosAsync(int anio, DateOnly? corte, CancellationToken ct)
    {
        var sql = """
SELECT IDPAGO, IDPERSONA, IDMESPAGO, IDTIPOPAGO, COALESCE(MONTOPAGO,0), FECHAPAGO,
       ANIOPAGO, TURNOSPAGO, DESCUENTO, TRIM(TIPOPAGO)
FROM pago WHERE ANIOPAGO=@anio
""" + (corte is null ? "" : " AND FECHAPAGO < @hastaExclusivo") + ";";
        var list = new List<LegacyPago>();
        await using var c=Open(); await c.OpenAsync(ct); await using var cmd=new MySqlCommand(sql,c); cmd.Parameters.AddWithValue("@anio",anio); if(corte is not null) cmd.Parameters.AddWithValue("@hastaExclusivo",corte.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)); await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),r.GetInt64(3),r.GetDecimal(4),r.GetDateTime(5),r.IsDBNull(6)?anio:r.GetInt32(6),r.GetInt32(7),r.IsDBNull(8)?null:r.GetDecimal(8),S(r,9)));
        return list;
    }

    public async Task<List<LegacyMoroso>> MorososAsync(int anio, CancellationToken ct)
    {
        const string sql="SELECT IDMOROSO, IDPERSONA, IDMESPAGO, ANIOPAGO, COALESCE(MONTOPAGO,0), IDTIPOPAGO FROM moroso WHERE ANIOPAGO=@anio;";
        var list=new List<LegacyMoroso>(); await using var c=Open(); await c.OpenAsync(ct); await using var cmd=new MySqlCommand(sql,c); cmd.Parameters.AddWithValue("@anio",anio); await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),r.GetInt32(3),r.GetDecimal(4),r.IsDBNull(5)?null:r.GetInt64(5))); return list;
    }

    public async Task<List<LegacyListaEspera>> ListaEsperaAsync(int anio, DateOnly? corte, CancellationToken ct)
    {
        var sql="SELECT IDLISTAESPERA, IDPERSONA, IDTURNO, IDTURNOPERSONA, FECHAESPERA, RECUPERA, IDPRESENTEAUSENTE FROM listaespera WHERE YEAR(FECHAESPERA)=@anio"+(corte is null?"":" AND FECHAESPERA < @hastaExclusivo")+";";
        var list=new List<LegacyListaEspera>(); await using var c=Open(); await c.OpenAsync(ct); await using var cmd=new MySqlCommand(sql,c); cmd.Parameters.AddWithValue("@anio",anio); if(corte is not null)cmd.Parameters.AddWithValue("@hastaExclusivo",corte.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)); await using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)) list.Add(new(r.GetInt64(0),r.GetInt64(1),r.IsDBNull(2)?null:r.GetInt32(2),r.IsDBNull(3)?null:r.GetInt64(3),DateOnly.FromDateTime(r.GetDateTime(4)),S(r,5),r.IsDBNull(6)?null:r.GetInt64(6))); return list;
    }

    // Solo lectura: la FK casi siempre no tiene coincidencia, por lo que NO se
    // infiere que un código de listaespera describa el uso de un recupero.
    public async Task<Dictionary<long,string>> CodigosVinculoRecuperoAsync(int anio, DateOnly? corte, CancellationToken ct)
    {
        var sql = """
SELECT l.IDLISTAESPERA, COALESCE(pa.PRESENTE, '')
FROM listaespera l
LEFT JOIN presenteausente pa ON pa.IDPRESENTEAUSENTE = l.IDPRESENTEAUSENTE
WHERE l.FECHAESPERA >= @desde AND l.FECHAESPERA < @hasta
""";
        var result = new Dictionary<long,string>();
        await using var c = Open(); await c.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, c);
        cmd.Parameters.AddWithValue("@desde", new DateTime(anio,1,1));
        cmd.Parameters.AddWithValue("@hasta", corte is null ? new DateTime(anio+1,1,1) : corte.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result[reader.GetInt64(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1).Trim();
        return result;
    }

    // No asumimos dónde se registraban las clases 1 y 2 de noviembre:
    // descubriendo columnas se genera una lista de tablas candidatas para auditar.
    public async Task<List<(string Table, string Columns)>> TablasCandidatasClasesAsync(CancellationToken ct)
    {
        const string sql = """
SELECT TABLE_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY ORDINAL_POSITION SEPARATOR ', ')
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND (LOWER(COLUMN_NAME) LIKE '%fecha%'
       OR LOWER(COLUMN_NAME) LIKE '%clase%'
       OR LOWER(COLUMN_NAME) LIKE '%turno%'
       OR LOWER(COLUMN_NAME) LIKE '%present%')
GROUP BY TABLE_NAME
HAVING SUM(LOWER(COLUMN_NAME) LIKE '%fecha%') > 0
   AND SUM(LOWER(COLUMN_NAME) LIKE '%clase%' OR LOWER(COLUMN_NAME) LIKE '%turno%' OR LOWER(COLUMN_NAME) LIKE '%present%') > 0
ORDER BY TABLE_NAME;
""";
        var result = new List<(string, string)>();
        await using var c = Open(); await c.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql,c);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct)) result.Add((reader.GetString(0),reader.IsDBNull(1) ? "" : reader.GetString(1)));
        return result;
    }


    // Asignaciones vigentes reales del sistema anterior (no inferir desde el histórico).
    public async Task<List<LegacyTurnoVigente>> TurnosVigentesAsync(CancellationToken ct)
    {
        const string sql = """
SELECT tp.IDPERSONA, tp.IDTURNO, t.IDDIASEMANA, TRIM(t.HORATURNO),
       TRIM(n.NOMBRETURNO), t.TURNOACTIVO
FROM turnopersona tp
JOIN turno t ON t.IDTURNO = tp.IDTURNO
JOIN nombreturno n ON n.IDNOMBRETURNO = t.IDNOMBRETURNO
WHERE tp.ACTIVO = 'S'
ORDER BY tp.IDPERSONA;
""";
        var result = new List<LegacyTurnoVigente>();
        await using var c = Open(); await c.OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, c);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result.Add(new LegacyTurnoVigente(r.GetInt64(0), r.GetInt32(1),
                r.GetInt32(2), S(r,3), S(r,4), S(r,5)));
        return result;
    }

    static string S(MySqlDataReader r,int i)=>r.IsDBNull(i)?"":r.GetString(i).Trim();
    static DateOnly? D(MySqlDataReader r,int i)=>r.IsDBNull(i)?null:DateOnly.FromDateTime(r.GetDateTime(i));
    static DateTime? DT(MySqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetDateTime(i);
}
