# Sistema visual de Klinara

**Versión:** borrador de diseño para el MVP  
**Estado:** dirección visual aprobada; falta integrarla y validarla en las pantallas Blazor.  
**Alcance:** web administrativa y portal del paciente.  
**Idioma del producto:** español de México (`es-MX`).

## 1. Dirección de diseño

Klinara debe sentirse confiable, ordenada, humana y fácil de usar durante una jornada clínica. La interfaz prioriza legibilidad, agenda y acciones frecuentes. El estilo es SaaS limpio con base azul, superficies claras, divisiones discretas y acentos de estado moderados; toma inspiración de interfaces de producto como Coinbase, adaptada a un contexto de salud y no como reproducción literal de otra marca.

Principios:

- **Claridad antes que decoración:** cada pantalla debe destacar su tarea principal y el siguiente paso.
- **Móvil primero:** controles táctiles cómodos, formularios simples y contenido que no requiera desplazamiento horizontal.
- **Confianza y privacidad:** identificar consultorio y rol, mostrar solo datos autorizados y evitar revelar información clínica en vistas no pertinentes.
- **Consistencia:** reutilizar navegación, encabezados, botones, campos, estados y espacios entre módulos.
- **Estado explícito:** diferenciar confirmado, pendiente, cancelado, atendido y no-show con texto/icono y color.
- **Datos honestos:** todo contenido ficticio lleva una marca visible de demostración; no simular funciones aún no conectadas.

## 2. Paleta de color

La paleta parte del Design System de Coinbase (`templates/coinbase.md`) y se adapta a Klinara: se conservan su azul, superficies blanca/negra, gris frío, DM Sans y CTA tipo píldora; los estados clínicos usan colores semánticos accesibles. No se replica la composición ni la marca de Coinbase. Medir contraste antes de cerrar la implementación.

| Token | Tema claro | Tema oscuro | Uso |
|---|---|---|---|
| Azul de marca | `#0052FF` | `#0052FF` | Acción primaria y selección |
| Azul hover | `#578BFA` | `#578BFA` | Hover/realce de acciones |
| Azul de enlace | `#0667D0` | `#80ADFF` | Enlaces secundarios |
| Fondo principal | `#FFFFFF` | `#0A0B0D` | Fondo de aplicación |
| Superficie secundaria | `#EEF0F3` | `#282B31` | Campos, superficies y controles secundarios |
| Texto principal | `#0A0B0D` | `#FFFFFF` | Contenido y títulos |
| Texto secundario | `#5B616E` | `#C1C5CE` | Ayuda y metadatos |
| Borde | `rgba(91,97,110,.20)` | `rgba(255,255,255,.16)` | Separadores y contenedores |
| Éxito | `#087A5B` | `#59C99D` | Confirmación/atendida |
| Advertencia | `#8A5B00` | `#F0C263` | Pendiente/por confirmar |
| Riesgo | `#B4233B` | `#FF8FA3` | Error/cancelación cuando aplique |

Los colores de estado no sustituyen el texto. Por ejemplo, usar “Por confirmar” junto con el color y no solo un punto amarillo. Verificar contraste WCAG AA, especialmente texto pequeño y controles en tema oscuro.

## 3. Tipografía, espaciado y forma

- **Familia:** DM Sans; stack `'DM Sans', system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif`. Usar `font-feature-settings: "ss01"`; números/tablas con `"tnum"`.
- **Jerarquía orientativa:** encabezado de página 24–32 px; títulos de panel 18–20 px; lectura de trabajo 16 px; texto secundario 14–16 px; captions 13–14 px. No bajar de 13 px en información necesaria para operar.
- **Escala de espaciado:** base 8 px; usar 4, 8, 12, 16, 24, 32 y 48 px para controles y secciones.
- **Radios:** 4–8 px en controles compactos; 12–16 px en tarjetas/superficies; 56 px solo en CTA primarios tipo píldora, no en tarjetas ni tablas.
- **Bordes:** 1 px, bajo contraste; usar sombra ligera solo para separar tarjetas de la superficie.
- **Foco:** contorno visible de 3 px con separación de 2 px; no quitar `outline` sin reemplazo accesible.
- **Iconografía:** SVG simple, trazo consistente; iconos decorativos ocultos a lectores de pantalla y controles con nombre accesible.

## 4. Estructura de aplicación

### Escritorio

- Barra lateral de aproximadamente 252 px con marca, consultorio activo, navegación por grupos y usuario/sesión.
- Barra superior con ubicación actual, distintivo de demostración cuando aplique, notificaciones y selector de tema.
- Área de trabajo con ancho máximo cercano a 1450 px y contenido organizado en tarjetas.
- Dashboard: cuatro indicadores; agenda diaria junto a próxima cita y acciones rápidas; actividad semanal y pagos recientes.

### Móvil y tablet

- En anchos menores a aproximadamente 820 px, la navegación lateral se transforma en menú desplegable con fondo superpuesto y cierre accesible.
- Las tarjetas pasan a una columna; indicadores usan dos columnas en teléfono cuando la legibilidad lo permita.
- Tablas se convierten en filas/tarjetas con acciones visibles; no exigir desplazamiento horizontal para completar tareas principales.
- Mantener controles táctiles con área suficiente y separar acciones peligrosas de acciones comunes.

Puntos de quiebre iniciales: 1100 px para compactar, 820 px para convertir el shell en móvil y 560 px para ajustar composición de teléfono. Se afinan con pruebas reales, no se consideran valores inmutables.

## 5. Componentes y patrones

- **Navegación:** área actual resaltada, grupos cortos y opción de consultorio claramente identificada. Ocultar acciones no autorizadas; enlaces aún no implementados deben estar deshabilitados y etiquetados, no conducir a 404.
- **Botones:** primario para la acción principal, DM Sans semibold de 16 px y forma píldora (56 px); secundarios compactos con radio 8 px; destructivo solo para cancelar/eliminar y con confirmación cuando haya pérdida de datos. Respetar un área táctil mínima de 44 px.
- **Tarjetas:** contenedor para cifras, agenda, próxima cita y actividad; mantener títulos y subtítulos de lectura rápida.
- **Formularios:** etiquetas persistentes, requerido explícito, ayuda junto al campo, validación asociada y resumen de error cuando aplique.
- **Listas y tablas:** búsqueda/filtros visibles, encabezados comprensibles, paginación si se necesita; en móvil, conservar contexto del registro y acceso a acciones.
- **Estados de cita:** texto más color/icono; mantener el mismo orden visual en agenda, ficha y notificaciones.
- **Notificaciones:** confirmar guardado, explicar el error sin información técnica y ofrecer el siguiente paso.
- **Carga y vacío:** skeleton o indicador discreto; mensaje vacío con causa y acción útil si existe.
- **Tema oscuro:** conservar jerarquía y contraste; no invertir colores de forma automática sin validar estados, imágenes y formularios.

## 6. Reglas por área

- **Dashboard:** mock de referencia con agenda, próxima cita, pagos y métricas; mantener “Vista de demostración” hasta consumir datos reales.
- **Pacientes:** distinguir datos de contacto de información clínica; no añadir campos clínicos fuera del alcance aprobado.
- **Agenda:** presentar zona horaria del consultorio, duración, disponibilidad, descansos y estados; anticipación del portal (24 horas mínimo, 45 días máximo) explicada antes de confirmar.
- **Portal del paciente:** priorizar correo verificado, disponibilidad y confirmación; no exponer otros pacientes ni datos internos del consultorio.
- **Pagos:** mostrar monto, fecha, método, concepto y saldo registrado/real con etiquetas inequívocas; evitar representar el saldo como cálculo automático si se captura manualmente.
- **Administración:** mostrar el consultorio/contexto activo; las pantallas de administrador de plataforma deben distinguirse de las del administrador del consultorio.

## 7. Español y codificación

- Configurar el documento como `<html lang="es">` y conservar `<meta charset="utf-8">` en `Components/App.razor`.
- Guardar archivos Razor, C#, CSS y recursos de texto en UTF-8. Evitar regrabar archivos con una codificación ANSI o ejecutar conversiones de codificación globales sin verificar.
- Usar formatos de fecha, hora y moneda de `es-MX`; las citas deben renderizarse en la zona horaria configurada para el consultorio.
- Comprobar en el navegador acentos, `ñ`, apertura/cierre de signos y caracteres de moneda. Casos de regresión sugeridos: `Clínica Santa Lucía`, `Próxima cita`, `María Rodríguez`, `Configuración`, `Código de verificación`.
- La codificación se valida en dos niveles: bytes/texto fuente UTF-8 válido y texto visible correcto en el DOM/renderizado. La presencia de `�`, `Ã`, `Â` u otras secuencias de mojibake debe investigarse en su origen.

## 8. Accesibilidad y calidad visual

- Objetivo de contraste WCAG AA: 4.5:1 para texto normal y 3:1 para texto grande y controles relevantes.
- Todas las tareas principales se deben poder completar con teclado; foco siempre visible y orden lógico.
- Campos con etiquetas programáticas; botones con nombre accesible; mensajes de estado anunciables.
- No comunicar estados únicamente con color, posición o iconografía.
- Revisar zoom al 200 %, ancho móvil, textos largos, nombres extensos y errores de validación.
- Las capturas visuales sirven como regresión, pero no sustituyen pruebas funcionales ni de autorización.

## 9. Relación con la implementación

El inventario, orden y aceptación de la implementación están en [`PLAN_UI_REDESIGN.md`](PLAN_UI_REDESIGN.md). La dirección visual del dashboard está aprobada, pero el estado del repositorio revisado aún conserva la página de inicio de plantilla; los estilos y el script locales no están conectados desde `App.razor`. Por tanto, este documento define el objetivo visual y no declara que todas las pantallas estén rediseñadas ni listas para producción.
