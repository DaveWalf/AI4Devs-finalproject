# Procedencia de las imágenes del puesto

Las imágenes de esta carpeta las usa la presentación táctil del puesto (D3 y D4 de `presentar-atencion-tactil`).
Ninguna contiene datos de personas ni del cliente.

## `fondo-cocina.jpg`

- **Qué es**: el fondo «Cocina con sonrisas», elegido por David el 03/11/2026 como dirección gráfica del puesto.
- **Origen**: imagen generada por Codex el 03/11/2026 con su herramienta integrada de generación de imágenes, sin
  referencias ni imágenes de terceros. El original es un PNG de 1536 × 1024; esta es su copia JPEG, sin recortar ni
  alterar la composición, tomada de la carpeta de investigación del proyecto
  (`investigacion\estilos-graficos-20261103\cocina-sonrisas\fondo-chefs.jpg`).
- **SHA-256**: `ca2e02877ded16fc953d4001e483a133c0441a1d37ac0d65d7f4fa80da607bc1` (195.923 bytes).
- **Prompt completo**:

```text
Use case: illustration-story
Asset type: High quality illustrated background for a warm cafeteria touchscreen selection interface, landscape 3:2 composition.
Primary request: A woman chef on the LEFT and a man chef on the RIGHT, both adult, appealing original animated-film-quality cartoon characters, joyfully preparing lunch for the diners. Both have warm, genuine smiles and look directly toward the viewer/customer who is choosing a meal. They are in the same inviting professional kitchen, behind a wooden food preparation counter.
Style/medium: Polished premium 3D animation illustration, nuanced soft materials, beautifully expressive kind faces, detailed but tasteful environment, not photorealistic, not flat clipart, no existing franchise characters.
Composition/framing: Wide frontal scene. Woman's head and hat centered around x=16%, y=24%; man's head and hat around x=84%, y=24%. Show both complete heads, tall white chef hats, shoulders and upper torsos, plus natural visible hands actively preparing food around y=62%. The woman gently stirs or plates a dish; the man holds a serving spoon next to a cooking pot. Eyes of both face the viewer while their hands work. Keep characters in the outer quarters; the middle 55% is a quiet, softly illuminated kitchen area with cream tiles, faint shelving and uncluttered negative space that can sit behind a translucent menu. No important face, hand or food in the middle area. Keep a little space outside both figures to avoid cropping. A continuous counter along the lower part connects the two characters. Clear professional kitchen context, a few pans and copper utensils, restrained small herb plant, subtle steam from cooked food.
Lighting/mood: Soft golden daylight and cozy ambient kitchen light, generous hospitality, a relaxing lunch break, warm inviting smiles, warm cream, muted sage, honey wood and soft copper, gentle natural color gradients, subtle depth of field.
Constraints: Exactly two people: one woman chef on the viewer's left and one man chef on the viewer's right, both smiling and looking at the viewer, both preparing food. No other people. Anatomically sensible hands. No text, no lettering, no logos, no watermark, no interface, no buttons, no menu panels, no border. This is only the background image, not a screenshot. Opaque complete kitchen background.
```

## `Recursos\incomedor-puesto.ico`

- **Qué es**: el ícono del ejecutable y de la ventana del puesto (D16 de `configurar-puesto-por-pestanas`): un plato que
  sonríe entre un tenedor y una cuchara. Está en `src\Intelektron.INComedor.Kiosk\Recursos\`, no en esta carpeta.
- **Origen**: generado el 04/11/2026 por `C:\INComedor\herramientas\icono\generar-icono.ps1`, fuera del repo, que lo
  dibuja con System.Drawing con la paleta de `puesto.css` (`--acento` `#3d5a40` en un degradé de `#4d7051` a `#33503a`, y
  `--sobre-acento` `#fbf5ea`) y el cobre `#b5652b` del logo de ejemplo de la concesionaria. Tiene nueve tamaños en
  entradas PNG: 16, 20, 24, 32, 40, 48, 64, 128 y 256 px; hasta 40 px, sin cubiertos ni borde del plato, para que se
  lea. El 07/11/2026 se volvió a generar desde una copia del script y salió el mismo archivo, byte a byte.
- **Marcas**: no contiene marcas de clientes ni de terceros, ni texto: sólo las formas que dibuja el script.
- **SHA-256**: `4180432911cc1a752be8f9faf7cf6724079ec5b9610ddd462b238c647803f1dd` (24.423 bytes).

## `intelektron.svg`

- **Qué es**: la marca Intelektron, que va discreta en el pie del puesto.
- **Origen**: el archivo oficial `https://www.intelektron.com/Images/logo-intelektron.svg`, bajado el 03/11/2026 para la
  maqueta, sin redibujar. Lo confirma David como dueño de la marca en el gate del change (punto 4).
- **SHA-256**: `832a1e7961d05146941d1bdc561eb0432d389701262321cbebf6655642ce322b` (4.164 bytes).

## `empresa-ejemplo.svg` y `concesionaria-ejemplo.svg`

- **Qué son**: logos neutros dibujados para el repo, que el puesto muestra cuando la carpeta de datos no tiene
  `marcas\empresa.*` ni `marcas\concesionaria.*`. Los logos reales no entran al repo: el cliente va anonimizado.
