## ¿Qué cambia?

<!-- Una a tres frases. -->

## ¿Por qué?

<!-- Historia (HU) y ticket (TK) que lo originan; decisión o devolución que lo motiva. -->

## ¿Cómo probarlo?

<!-- Comandos concretos y lo que debe verse. Ejemplo: `dotnet test`, `openspec validate --all --strict`. -->

## Change de OpenSpec

- Change: `openspec/changes/<nombre>/` (o `archive/AAAA-MM-DD-<nombre>/` si ya se archivó)
- Capability que agrega o modifica:
- `openspec validate --all --strict`:

```text
<pegar la salida>
```

## Salida de `/openspec-verify-change`

```text
<pegar la salida>
```

## Decisiones y alternativas descartadas

<!-- Opcional: lo que se consideró y no se hizo, y por qué. -->

## Checklist

- [ ] Los tests nuevos se vieron fallar antes de implementar.
- [ ] No se modificó ningún test existente sin aprobación.
- [ ] Sólo datos sintéticos; ningún secreto en el diff.
- [ ] Entrada externa revisada contra OWASP Top 10, si el cambio la toca.
- [ ] `AGENTS.md` y `openspec/config.yaml` siguen vigentes después de este cambio.
