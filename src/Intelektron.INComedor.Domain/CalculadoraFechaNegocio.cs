namespace Intelektron.INComedor.Domain;

/// <summary>Calcula la fecha de negocio con la zona y el corte configurados en el puesto (RB04).</summary>
public static class CalculadoraFechaNegocio
{
    public static FechaNegocio Calcular(DateTimeOffset instanteUtc, string zonaHorariaId, TimeOnly inicioDiaLocal)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(zonaHorariaId);
        var local = TimeZoneInfo.ConvertTime(instanteUtc, zona).DateTime;

        var fecha = DateOnly.FromDateTime(local);
        if (TimeOnly.FromDateTime(local) < inicioDiaLocal)
        {
            fecha = fecha.AddDays(-1);
        }

        return new FechaNegocio(fecha);
    }
}
