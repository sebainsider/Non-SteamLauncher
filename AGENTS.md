# Reglas Estrictas del Proyecto — NonSteamLauncher

## 1. Sistema de Diseño: Microsoft Fluent Design & Fluent 2 Adaptativo al Sistema (Regla Estricta)

Toda interfaz gráfica, ventana, diálogo o componente visual en este repositorio **DEBE** diseñarse e implementarse siguiendo estrictamente los lineamientos oficiales de **Microsoft Fluent Design System** y **Fluent 2 Design System** (Windows 11 / WinUI 3), siendo **100% adaptativo a la configuración del sistema operativo** en tiempo real:
1. **Tema del sistema (Light Theme / Dark Theme)**: Sincronizado con `AppsUseLightTheme` y `SystemUsesLightTheme` (`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`).
2. **Color de acento del sistema (`AccentPalette` / `AccentColor`)**: Los controles primarios y de acento deben tomar la rampa de acento configurada por el usuario en Windows (`HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent\AccentPalette`: `AccentDark1` en modo claro, `AccentLight2` en modo oscuro, con cálculo automático de contraste para `TextOnAccentFillColorPrimary`).
3. **Temas de contraste (High Contrast / Accesibilidad)**: Respetar `SPI_GETHIGHCONTRAST` y colores del sistema (`GetSysColor`) cuando el usuario active un tema de alto contraste en Windows.
4. **Escalado DPI, Couch Gaming (100%–200%+) e idioma del sistema**:
   - Activar explícitamente **Per-Monitor DPI Awareness V2** (`DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2`) antes de crear cualquier ventana para evitar el escalado por *bitmap stretching* borroso de Windows cuando el sistema está al 125%, 150%, 175% o 200%+.
   - Calcular el rectángulo no-cliente con `AdjustWindowRectExForDpi` (con fallback a `AdjustWindowRectEx`) y limitar las dimensiones máximas al área de trabajo del monitor (`SPI_GETWORKAREA`) para pantallas de salón (*Couch Gaming* 1080p/4K a 150%–200% y consolas portátiles).
   - Medir dinámicamente el texto con `GetTextExtentPoint32` para botones, insignias (*badges*) y contadores, evitando anchos fijos que trunquen textos largos o escalados al 150%+.
   - Responder en vivo a `WM_DPICHANGED` y al idioma del sistema (`CultureInfo.CurrentUICulture`).

Queda prohibido el uso de estilos ad-hoc, temas fijos sin adaptación al sistema, imitaciones de temas antiguos de Steam, colores saturados sin tokens o geometrías arbitrarias.

### 1.1 Tokens Semánticos Fluent 2 (`Fluent2Palette`)

| Token Semántico Fluent 2 | Light Theme | Dark Theme | Uso |
| :--- | :--- | :--- | :--- |
| `SolidBackgroundFillColorBase` | `#F3F3F3` (`243, 243, 243`) | `#202020` (`32, 32, 32`) | Fondo base de la ventana y barra de título (con DWM Mica). |
| `SolidBackgroundFillColorSecondary` | `#EEEEEE` (`238, 238, 238`) | `#181818` (`24, 24, 24`) | Área inferior de comandos / pie de diálogo (*ContentDialog Footer*). |
| `LayerFillColorDefault` | `#F9F9F9` (`249, 249, 249`) | `#272727` (`39, 39, 39`) | Contenedor agrupador o superficie de lista. |
| `CardBackgroundFillColorDefault` | `#FFFFFF` (`255, 255, 255`) | `#2D2D2D` (`45, 45, 45`) | Superficie de tarjetas (*Cards*) y filas de elementos. |
| `CardBackgroundFillColorSecondary` | `#F6F6F6` (`246, 246, 246`) | `#363636` (`54, 54, 54`) | Estado *hover* de tarjeta o fila interactiva. |
| `CardStrokeColorDefault` | `#E5E5E5` (`229, 229, 229`) | `#3A3A3A` (`58, 58, 58`) | Borde sutil de `1px` para tarjetas y contenedores. |
| `DividerStrokeColorDefault` | `#E5E5E5` (`229, 229, 229`) | `#303030` (`48, 48, 48`) | Línea divisoria de `1px` entre cuerpo y pie de diálogo. |
| `TextFillColorPrimary` | `#1A1A1A` (`26, 26, 26`) | `#FFFFFF` (`255, 255, 255`) | Títulos y texto principal. |
| `TextFillColorSecondary` | `#5C5C5C` (`92, 92, 92`) | `#C5C5C5` (`197, 197, 197`) | Subtítulos, etiquetas secundarias y metadatos. |
| `TextFillColorTertiary` | `#8A8A8A` (`138, 138, 138`) | `#8A8A8A` (`138, 138, 138`) | Texto terciario, contadores y marcas de agua. |
| `AccentFillColorDefault` | `AccentDark1` (fallback `#0067C0`) | `AccentLight2` (fallback `#60CDFF`) | Botón primario de acento del sistema (estado *Rest*). |
| `AccentFillColorSecondary` | `90%` opacidad / hover | `90%` opacidad / hover | Botón primario de acento del sistema (estado *Hover*). |
| `AccentFillColorTertiary` | `80%` opacidad / pressed | `80%` opacidad / pressed | Botón primario de acento del sistema (estado *Pressed*). |
| `TextOnAccentFillColorPrimary` | `#FFFFFF` / según luminancia | `#000000` / según luminancia | Texto e iconografía sobre superficies de acento. |
| `SystemFillColorSuccess` / `Bg` | `#0F7B0F` / `#DFF6DD` | `#6CCB5F` / `#1E2C1E` | `InfoBar` e indicadores de éxito. |
| `SystemFillColorAttention` / `Bg` | `AccentDark1` / `#EBF3FC` | `AccentLight2` / `#1C272E` | `InfoBar` informativo / biblioteca al día. |
| `SystemFillColorCritical` / `Bg` | `#C42B1C` / `#FDE7E9` | `#FF99A4` / `#321F21` | `InfoBar` e indicadores de error crítico. |

### 1.2 Escala Tipográfica (Fluent 2 Type Ramp)
- **Fuentes obligatorias**: `Segoe UI Variable Display` / `Segoe UI Variable Text` (con fallback a `Segoe UI`) e iconografía vectorial con `Segoe Fluent Icons` (con fallback a `Segoe MDL2 Assets` / Unicode).
- **Jerarquía**:
  - **Subtitle / Dialog Title**: `20px`, Semibold (`600`), `Segoe UI Variable Display`.
  - **Body Strong**: `14px`, Semibold (`600`), `Segoe UI Variable Text`.
  - **Body**: `14px`, Regular (`400`), `Segoe UI Variable Text`.
  - **Caption**: `12px`, Regular (`400`) o Semibold (`600`), `Segoe UI Variable Text`.
- Prohibido usar texto todo en mayúsculas (*ALL CAPS*) en encabezados de tarjetas o botones; Fluent 2 exige *Sentence case* para legibilidad y jerarquía natural.

### 1.3 Geometría, Espaciado, Elevación y DWM Nativo
- **Grilla de 4px / 8px**: Todo margen, padding y separación debe ser múltiplo de `4px` (`4, 8, 12, 16, 20, 24`). El margen exterior de un diálogo (`ContentDialog` padding) es de `24px`.
- **Radios de esquina (Corner Radius)**:
  - Ventana de nivel superior: Esquinas redondeadas nativas de Windows 11 (`DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND`, `8px`).
  - Contenedores y tarjetas (`OverlayCornerRadius` / `Card`): `8px`.
  - Controles, botones y *badges* (`ControlCornerRadius`): `4px` a `6px`.
- **Integración DWM (Desktop Window Manager) y Eventos del Sistema**:
  - Sincronizar `DWMWA_USE_IMMERSIVE_DARK_MODE`, `DWMWA_SYSTEMBACKDROP_TYPE` (`DWMSBT_MAINWINDOW` Mica), `DWMWA_CAPTION_COLOR`, `DWMWA_TEXT_COLOR` y `DWMWA_BORDER_COLOR`.
  - Escuchar `WM_SETTINGCHANGE`, `WM_SYSCOLORCHANGE`, `WM_THEMECHANGED`, `WM_DWMCOLORIZATIONCOLORCHANGED` y `WM_DPICHANGED` para adaptar la ventana en tiempo real ante cualquier cambio del sistema operativo.
- **Calidad de Renderizado y Ergonomía**:
  - **Doble búfer obligatorio (`Double Buffering`)**: Todo renderizado GDI debe usar un `Memory DC` (`CreateCompatibleDC` + `CreateCompatibleBitmap` + `BitBlt`) y suprimir `WM_ERASEBKGND`.
  - **Dimensionamiento exacto del área cliente**: Usar `AdjustWindowRectEx` / `GetClientRect` para que la barra de título de Windows nunca recorte el pie de página ni los controles inferiores.
