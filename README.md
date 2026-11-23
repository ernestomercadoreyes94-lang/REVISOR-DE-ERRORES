# 🔍 Revisor de errores

App para Windows que revisa los errores de tu compu y te los explica en **español fácil**.

ESTE PROYECTO LO CREO LA IA, ES FREESOURCE SI LO QUIEREN MODIFICAR
CREDITOS:CLAUDE
CREDITOS:ERNESTOMERCADOREYES94

## Qué hace

- Lee los registros de errores de Windows (Sistema y Aplicación) de los últimos **1, 7, 30 o 90 días**.
- Te dice en grande si **todo está bien**, si **algo falló** o si hay **algo grave**.
- Los separa por tipo: pantallas azules, apagones de golpe, fallas de disco, fallas de hardware,
  apps que se cerraron solas, apps que se trabaron, video, actualizaciones y servicios.
- **Gráfica por día**: toca una barra para ver solo los errores de ese día.
- Botón **Solo hoy**.
- **Modo oscuro** (se acuerda de tu elección).
- En cada error te explica **qué pasó** y **qué hacer**.
- Botones para copiar los errores, abrir el Visor de eventos, el Historial de confiabilidad
  y reparar los archivos de Windows (`sfc /scannow`).

## Cómo usarlo

Descarga **`Revisor de errores.exe`** y ábrelo. No hay que instalar nada.
Necesita .NET Framework 4 (ya viene con Windows 10 y 11).
si te da cosa tener muchosssss errores usa en cmd como admin se recomienda crear un punto de restauracion
wevtutil cl System
wevtutil cl Application

## ⚠️ Si sale "Windows protegió su PC" (SmartScreen)

**No es un virus.** Sale porque el programa es nuevo y no tiene firma digital.

1. Haz clic en **Más información**.
2. Haz clic en **Ejecutar de todas formas**.

## Compilarlo tú mismo

El código está en la carpeta `codigo`. Se compila con el compilador que ya trae Windows:

```
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /win32icon:codigo\revisor.ico /r:System.Core.dll /out:"Revisor de errores.exe" codigo\RevisorErrores.cs
```
