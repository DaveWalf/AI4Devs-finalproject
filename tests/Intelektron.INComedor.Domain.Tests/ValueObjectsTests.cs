using Intelektron.INComedor.Domain;

namespace Intelektron.INComedor.Domain.Tests;

public class ValueObjectsTests
{
    private static readonly FechaNegocio Hoy = new(new DateOnly(2026, 10, 19));

    [Fact(DisplayName = "Invariante · CodigoIntencion rechaza el UUID vacío")]
    public void CodigoIntencion_rechaza_uuid_vacio()
    {
        Assert.Throws<ArgumentException>(() => new CodigoIntencion(Guid.Empty));
    }

    [Fact(DisplayName = "Invariante · CodigoIntencion conserva un UUID no vacío")]
    public void CodigoIntencion_conserva_uuid_valido()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, new CodigoIntencion(id).Valor);
    }

    [Fact(DisplayName = "Invariante · FechaNegocio rechaza la fecha sin inicializar")]
    public void FechaNegocio_rechaza_fecha_sin_inicializar()
    {
        Assert.Throws<ArgumentException>(() => new FechaNegocio(default));
    }

    [Fact(DisplayName = "Invariante · FechaNegocio conserva una fecha válida")]
    public void FechaNegocio_conserva_fecha_valida()
    {
        Assert.Equal(new DateOnly(2026, 10, 19), Hoy.Valor);
    }

    [Theory(DisplayName = "Invariante · ClaveCupo rechaza un UUID vacío en persona, ámbito o categoría")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ClaveCupo_rechaza_uuid_vacio(int posicion)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ids[posicion] = Guid.Empty;

        Assert.Throws<ArgumentException>(() => new ClaveCupo(ids[0], ids[1], ids[2], Hoy));
    }

    [Fact(DisplayName = "Invariante · ClaveCupo con los mismos cuatro datos es igual y sirve de clave de diccionario")]
    public void ClaveCupo_igual_por_valor()
    {
        var persona = Guid.NewGuid();
        var ambito = Guid.NewGuid();
        var categoria = Guid.NewGuid();
        var uso = new Dictionary<ClaveCupo, int> { [new ClaveCupo(persona, ambito, categoria, Hoy)] = 1 };

        Assert.Equal(1, uso[new ClaveCupo(persona, ambito, categoria, new FechaNegocio(new DateOnly(2026, 10, 19)))]);
    }
}
