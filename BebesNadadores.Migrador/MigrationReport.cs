using System.Text;

namespace BebesNadadores.Migrador;

public sealed class MigrationReport
{
    private readonly string _dir;
    private readonly List<string[]> _warnings = [];
    private readonly List<string[]> _unknownAttendance = [];
    private readonly List<string[]> _waitlist = [];
    private readonly List<string[]> _nameReview = [];
    private readonly List<string[]> _missingPersons = [];
    private readonly List<string[]> _invalidBirths = [];
    private readonly List<string[]> _quotaReview = [];
    private readonly List<string[]> _paymentAudit = [];
    private readonly List<string[]> _unlinkedPayments = [];
    private readonly List<string[]> _reclassifiedPersons = [];
    private readonly List<string[]> _monthlyReconciliation = [];
    private readonly List<string[]> _recoveryAudit = [];
    private readonly List<string[]> _paymentComplete = [];
    private readonly List<string[]> _dateSourceCandidates = [];
    private readonly List<string[]> _financialRisk = [];

    public int BlockingCount => _unknownAttendance.Count + _waitlist.Count + _missingPersons.Count + _invalidBirths.Count + _quotaReview.Count + _unlinkedPayments.Count;
    public bool HasBlockingIssues => BlockingCount > 0;

    public MigrationReport(string dir) => _dir = dir;

    public void Warn(string category, string id, string message) => _warnings.Add([category,id,message]);
    public void UnknownAttendance(LegacyClase x) => _unknownAttendance.Add([x.Id.ToString(),x.PersonaId.ToString(),x.Fecha.ToString("yyyy-MM-dd"),x.Codigo,x.Dia,x.Hora,x.NivelNombre]);
    public void Waitlist(LegacyListaEspera x) => _waitlist.Add([x.Id.ToString(),x.PersonaId.ToString(),x.Fecha.ToString("yyyy-MM-dd"),x.Codigo,x.TurnoId?.ToString()??"",x.PresenteAusenteId?.ToString()??""]);
    public void NameReview(long id,string original,string nombre,string apellido,string tipo) => _nameReview.Add([tipo,id.ToString(),original,nombre,apellido]);

    public void MissingPerson(long id,string reason) => _missingPersons.Add([id.ToString(),reason]);
    public void InvalidBirth(long id,string name,string date,string reason) => _invalidBirths.Add([id.ToString(),name,date,reason]);
    public void QuotaReview(long id,int month,int year,decimal paid,decimal debt,string types) =>
        _quotaReview.Add([id.ToString(),year.ToString(),month.ToString(),paid.ToString(System.Globalization.CultureInfo.InvariantCulture),debt.ToString(System.Globalization.CultureInfo.InvariantCulture),types]);
    public void PaymentAudit(long id,long person,int year,int month,decimal amount,DateTime date,string type,long typeId) =>
        _paymentAudit.Add([id.ToString(),person.ToString(),year.ToString(),month.ToString(),amount.ToString(System.Globalization.CultureInfo.InvariantCulture),date.ToString("yyyy-MM-dd HH:mm:ss"),type,typeId.ToString()]);
    public void UnlinkedPayment(long id,long person,decimal amount,string reason) =>
        _unlinkedPayments.Add([id.ToString(),person.ToString(),amount.ToString(System.Globalization.CultureInfo.InvariantCulture),reason]);

    public void ReclassifiedPerson(long id, string name, string birth, string reason) =>
        _reclassifiedPersons.Add([id.ToString(), name, birth, reason]);

    public void MonthlyReconciliation(long person, int year, int month, decimal monthly, decimal perClass,
        decimal halfMonth, decimal other, decimal debt, int paymentCount, int debtCount) =>
        _monthlyReconciliation.Add([person.ToString(), year.ToString(), month.ToString(),
            F(monthly), F(perClass), F(halfMonth), F(other), F(debt), paymentCount.ToString(), debtCount.ToString()]);

    public void RecoveryAudit(LegacyListaEspera x, string directCode, string sameDayCodes, int sameDayRecords, string result, string rationale) =>
        _recoveryAudit.Add([x.Id.ToString(), x.PersonaId.ToString(), x.Fecha.ToString("yyyy-MM-dd"), x.Codigo,
            x.TurnoId?.ToString() ?? "", x.TurnoPersonaId?.ToString() ?? "", x.PresenteAusenteId?.ToString() ?? "",
            directCode, sameDayCodes, sameDayRecords.ToString(), result, rationale]);

    public void PaymentComplete(LegacyPago p, string state) =>
        _paymentComplete.Add([p.Id.ToString(), p.PersonaId.ToString(), p.FechaPago.ToString("yyyy-MM-dd HH:mm:ss"),
            p.AnioPago.ToString(), p.Mes.ToString(), F(p.Importe), p.TipoPagoId.ToString(), state]);

    public void DateSourceCandidate(string table, string columns, string reason) =>
        _dateSourceCandidates.Add([table, columns, reason]);

    public void FinancialRisk(long person, int month, decimal paid, decimal debt, string reason) =>
        _financialRisk.Add([person.ToString(), month.ToString(), F(paid), F(debt), reason]);

    private static string F(decimal amount) => amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public void Save()
    {
        Directory.CreateDirectory(_dir);
        Write("advertencias.csv", ["categoria","legacy_id","mensaje"], _warnings);
        Write("asistencias_codigos_no_mapeados.csv", ["idhpc","persona_id","fecha","codigo","dia","hora","nivel"], _unknownAttendance);
        Write("lista_espera_recuperos_legacy_2026.csv", ["id","persona_id","fecha","codigo","turno_id","presenteausente_id"], _waitlist);
        Write("nombres_para_revisar.csv", ["tipo","legacy_id","original","nombre","apellido"], _nameReview);
        Write("personas_sin_ficha_origen.csv", ["legacy_persona_id","motivo"], _missingPersons);
        Write("nacimientos_invalidos.csv", ["legacy_persona_id","nombre","nacimiento","motivo"], _invalidBirths);
        Write("cuotas_a_conciliar.csv", ["legacy_persona_id","anio","mes","cobrado","morosidad","tipos_pago"], _quotaReview);
        Write("pagos_2026_a_auditar.csv", ["legacy_pago_id","legacy_persona_id","anio","mes","importe","fecha","tipo_catalogo","idtipopago"], _paymentAudit);
        Write("pagos_sin_alumno_operativo.csv", ["legacy_pago_id","legacy_persona_id","importe","motivo"], _unlinkedPayments);
        Write("personas_tipo_1_con_actividad_2026.csv", ["legacy_persona_id","nombre_original","nacimiento","observacion"], _reclassifiedPersons);
        Write("recuperos_2026_a_conciliar_v4.csv", ["lista_id","persona_id","fecha_reserva","codigo_recupera","turno_id","turno_persona_id","presenteausente_id","codigo_vinculo_directo","codigos_historico_mismo_dia","registros_historicos_mismo_dia","resultado","motivo"], _recoveryAudit);
        Write("pagos_2026_control_total_v4.csv", ["pago_id","persona_id","fecha_real","anio_periodo","mes_periodo","importe","idtipopago","resultado"], _paymentComplete);
        Write("posibles_fuentes_clases_noviembre_v4.csv", ["tabla","columnas_relevantes","observacion"], _dateSourceCandidates);
        Write("riesgos_morosidad_v4.csv", ["persona_id","mes","pagos_cuota","morosidad","motivo"], _financialRisk);
        Write("conciliacion_financiera_2026.csv", ["legacy_persona_id","anio","mes","cobros_cuota","cobros_clase","cobros_medio_mes","otros_cobros","morosidad","cantidad_pagos","cantidad_deudas"], _monthlyReconciliation);
    }

    private void Write(string name,string[] headers,List<string[]> rows)
    {
        using var w = new StreamWriter(Path.Combine(_dir,name),false,new UTF8Encoding(true));
        w.WriteLine(string.Join(';',headers.Select(E)));
        foreach(var r in rows) w.WriteLine(string.Join(';',r.Select(E)));
    }
    private static string E(string s)=>'"'+(s??"").Replace("\"","\"\"")+'"';
}
