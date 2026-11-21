// Revisor de errores: lee los registros de Windows (Sistema y Aplicación) y explica en español
// fácil qué falló: apps que se cerraron solas, pantallas azules, apagones de golpe, disco, etc.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

class Problema
{
    public DateTime Fecha;
    public string Tipo;        // categoría en español
    public string Titulo;      // explicación corta
    public string Origen;      // quién lo reportó (nombre técnico)
    public int Id;
    public int Gravedad;       // 3 = grave, 2 = importante, 1 = menor
    public string Detalle;     // texto completo de Windows
    public string Consejo;     // qué hacer
    public Color ColorPropio;  // si no está vacío, se pinta de este color (los del código secret)
    public string Clave;       // identifica a este error siempre igual (para poder eliminarlo del Revisor)
}

static class Revisor
{
    // Lee los errores de los últimos "dias" días
    public static List<Problema> Leer(int dias, Action<string> progreso)
    {
        List<Problema> lista = new List<Problema>();
        long ms = (long)dias * 24 * 3600 * 1000;
        foreach (string log in new[] { "System", "Application" })
        {
            progreso("Leyendo registro " + (log == "System" ? "del sistema" : "de aplicaciones") + "...");
            string q = "*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= " + ms + "]]]";
            try
            {
                EventLogQuery consulta = new EventLogQuery(log, PathType.LogName, q) { ReverseDirection = true };
                using (EventLogReader r = new EventLogReader(consulta))
                {
                    int n = 0;
                    for (EventRecord e = r.ReadEvent(); e != null && n < 3000; e = r.ReadEvent(), n++)
                        using (e) { Problema p = Clasificar(e); if (p != null) { p.Clave = log + "#" + e.RecordId; lista.Add(p); } }
                }
            }
            catch (Exception ex) { progreso("No se pudo leer " + log + ": " + ex.Message); }
        }
        // apagones de golpe y pantallas azules también vienen como "crítico" o "información" en otros lados
        progreso("Buscando pantallas azules...");
        try
        {
            string q2 = "*[System[Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting' or @Name='BugCheck'] and TimeCreated[timediff(@SystemTime) <= " + ms + "]]]";
            using (EventLogReader r = new EventLogReader(new EventLogQuery("System", PathType.LogName, q2)))
                for (EventRecord e = r.ReadEvent(); e != null; e = r.ReadEvent())
                    using (e)
                        if (!lista.Any(x => x.Fecha == e.TimeCreated && x.Id == e.Id)) { Problema pa = PantallaAzul(e); pa.Clave = "System#" + e.RecordId; lista.Add(pa); }
        }
        catch { }
        progreso("Buscando actualizaciones instaladas...");
        try
        {
            string q3 = "*[System[Provider[@Name='Microsoft-Windows-WindowsUpdateClient'] and (EventID=19) and TimeCreated[timediff(@SystemTime) <= " + ms + "]]]";
            using (EventLogReader r = new EventLogReader(new EventLogQuery("System", PathType.LogName, q3)))
                for (EventRecord e = r.ReadEvent(); e != null; e = r.ReadEvent())
                    using (e)
                    {
                        string nombre = Prop(e, 0);
                        lista.Add(new Problema
                        {
                            Fecha = e.TimeCreated ?? DateTime.MinValue, Origen = e.ProviderName, Id = e.Id, Gravedad = 0,
                            Clave = "System#" + e.RecordId,
                            Tipo = "Actualización instalada",
                            Titulo = "Se instaló una actualización" + (nombre.Length > 0 ? ": " + nombre : ""),
                            Detalle = Texto(e),
                            Consejo = "No hay que hacer nada: es información, todo salió bien."
                        });
                    }
        }
        catch { }
        progreso("Buscando cosas raras...");
        LeerSecret(dias, lista);
        return lista.OrderByDescending(x => x.Fecha).ToList();
    }

    // ---- código secreto SECRET del DVD Rebote ----
    // El DVD anota en %LOCALAPPDATA%\DvdRebote\secreto.log cada "NO ESCAPE" y cada ventanita de "Error".
    // Aquí salen como errores: "NO ESCAPE" en rojo y "Error" en amarillo, con letras cursed.
    public static readonly Color Amarillo = Color.FromArgb(225, 185, 0);
    static void LeerSecret(int dias, List<Problema> lista)
    {
        string ruta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DvdRebote", "secreto.log");
        if (!System.IO.File.Exists(ruta)) return;
        string[] lineas;
        try { lineas = System.IO.File.ReadAllLines(ruta, System.Text.Encoding.UTF8); } catch { return; }
        DateTime desde = DateTime.Now.AddDays(-dias);
        for (int i = 0; i < lineas.Length; i++)
        {
            string l = lineas[i];
            string[] c = l.Split(new[] { '|' }, 3);
            if (c.Length < 3) continue;
            DateTime fecha;
            if (!DateTime.TryParse(c[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out fecha) || fecha < desde) continue;
            Problema p = new Problema { Fecha = fecha, Origen = "DVD Rebote (código secret)", Id = 666, Detalle = c[2], Clave = "secret#" + i + "#" + c[0] };
            switch (c[1])
            {
                case "NO ESCAPE":
                    p.Tipo = "NO ESCAPE"; p.Gravedad = 3; p.ColorPropio = Color.FromArgb(220, 55, 55);
                    p.Titulo = Maldecir("NO ESCAPE", 3, 6);
                    break;
                case "MALDITO":
                    p.Tipo = "NO ESCAPE"; p.Gravedad = 3; p.ColorPropio = Color.FromArgb(220, 55, 55);
                    p.Titulo = Maldecir("No intentes escapar, hermano.", 2, 4);
                    break;
                case "ERROR":
                    p.Tipo = "Error"; p.Gravedad = 2; p.ColorPropio = Amarillo;
                    p.Titulo = Maldecir("Error", 3, 6);
                    break;
                case "PANTALLAZO":
                    // el pantallazo azul falso que sale después de darle "Aceptar" a NO ESCAPE
                    p.Tipo = "Pantalla azul"; p.Gravedad = 3; p.ColorPropio = Color.FromArgb(0, 120, 215);
                    p.Titulo = "Pantallazo azul";
                    break;
                default: continue;   // ACEPTAR y CERRADO no son errores
            }
            p.Consejo = "Viene del código secreto del DVD Rebote. Se quita con \"Aceptar\" en la ventana NO ESCAPE, " +
                        "con el .bat \"Cerrar ventanas de error\" o cerrando el DVD. Tu compu está bien.";
            lista.Add(p);
        }
    }

    // letras "cursed": acentos raros arriba y abajo de cada letra (sin tachar, para que se lea)
    static readonly Random rndMaldito = new Random();
    static string Maldecir(string s, int min, int max)
    {
        var arriba = new List<char>(); var abajo = new List<char>();
        int[] ra = { 0x300, 0x315, 0x33D, 0x344, 0x346, 0x346, 0x34A, 0x34C, 0x350, 0x352, 0x357, 0x357, 0x35B, 0x35B, 0x363, 0x36F };
        int[] rb = { 0x316, 0x319, 0x31C, 0x333, 0x339, 0x33C, 0x345, 0x345, 0x347, 0x349, 0x34D, 0x34E, 0x353, 0x356, 0x359, 0x35A };
        for (int i = 0; i < ra.Length; i += 2) for (int c = ra[i]; c <= ra[i + 1]; c++) arriba.Add((char)c);
        for (int i = 0; i < rb.Length; i += 2) for (int c = rb[i]; c <= rb[i + 1]; c++) abajo.Add((char)c);
        var b = new System.Text.StringBuilder();
        foreach (char ch in s)
        {
            b.Append(ch);
            if (ch == ' ' || ch == ',' || ch == '.') continue;
            int n = min + rndMaldito.Next(max - min + 1);
            for (int i = 0; i < n; i++) { var m = i % 2 == 0 ? arriba : abajo; b.Append(m[rndMaldito.Next(m.Count)]); }
        }
        return b.ToString();
    }

    static string Texto(EventRecord e)
    {
        try { string s = e.FormatDescription(); if (!string.IsNullOrEmpty(s)) return s.Trim(); } catch { }
        try { return string.Join(" | ", e.Properties.Select(p => Convert.ToString(p.Value))); } catch { return ""; }
    }

    static string Prop(EventRecord e, int i)
    {
        try { return i < e.Properties.Count ? Convert.ToString(e.Properties[i].Value) : ""; } catch { return ""; }
    }

    static Problema PantallaAzul(EventRecord e)
    {
        return new Problema
        {
            Fecha = e.TimeCreated ?? DateTime.MinValue, Origen = e.ProviderName, Id = e.Id, Gravedad = 3,
            Tipo = "Pantalla azul",
            Titulo = "Windows tuvo una pantalla azul y se reinició",
            Detalle = Texto(e),
            Consejo = "Si pasa seguido: actualiza los drivers (sobre todo el de video), revisa la memoria RAM " +
                      "(Diagnóstico de memoria de Windows) y que la compu no se caliente."
        };
    }

    static Problema Clasificar(EventRecord e)
    {
        string prov = e.ProviderName ?? "";
        int id = e.Id;
        Problema p = new Problema
        {
            Fecha = e.TimeCreated ?? DateTime.MinValue, Origen = prov, Id = id, Detalle = Texto(e), Gravedad = 1,
            Tipo = "Otro error", Titulo = "Un componente de Windows reportó un error",
            Consejo = "Casi siempre no es grave. Si se repite mucho, busca en internet el origen y el número."
        };

        if (prov == "Application Error" && id == 1000)
        {
            string app = Prop(e, 0);
            p.Tipo = "App que se cerró sola"; p.Gravedad = 2;
            p.Titulo = "\"" + app + "\" se cerró sola (falló)";
            p.Consejo = "Actualiza o reinstala esa app. Si es un juego, actualiza el driver de video.";
            if (app.IndexOf("DvdRebote", StringComparison.OrdinalIgnoreCase) >= 0 || app.IndexOf("dvd_config", StringComparison.OrdinalIgnoreCase) >= 0)
                p.Consejo = "Es el DVD Rebote. Copia este error y pásaselo a Claude para arreglarlo.";
        }
        else if (prov == "Application Hang")
        {
            string app = Prop(e, 0);
            p.Tipo = "App que se trabó"; p.Gravedad = 2;
            p.Titulo = "\"" + app + "\" dejó de responder y se cerró";
            p.Consejo = "Puede ser falta de memoria o que la app esté vieja. Cierra programas que no uses y actualízala.";
        }
        else if (prov == ".NET Runtime" && id == 1026)
        {
            p.Tipo = "App que se cerró sola"; p.Gravedad = 2;
            // en Windows en inglés dice "Application:", en español "Aplicación:"
            string d = p.Detalle, app = "una app .NET";
            foreach (string clave in new[] { "Application:", "Aplicación:" })
            {
                int i = d.IndexOf(clave, StringComparison.OrdinalIgnoreCase);
                if (i >= 0) { app = d.Substring(i + clave.Length).Split('\n')[0].Trim(); break; }
            }
            p.Titulo = "\"" + app + "\" tuvo un error y se cerró";
            p.Consejo = app.IndexOf("DvdRebote", StringComparison.OrdinalIgnoreCase) >= 0 || app.IndexOf("dvd_config", StringComparison.OrdinalIgnoreCase) >= 0
                ? "Es el DVD Rebote. Copia este error y pásaselo a Claude para arreglarlo."
                : "Actualiza o reinstala esa app.";
        }
        else if (prov == "Microsoft-Windows-Kernel-Power" && id == 41)
        {
            p.Tipo = "Apagón de golpe"; p.Gravedad = 3;
            p.Titulo = "La compu se apagó o reinició de golpe (sin apagarse bien)";
            p.Consejo = "Pasa si se fue la luz, si dejaste presionado el botón de encendido o si se acabó la batería. " +
                        "Si pasa solo, puede ser calor o la fuente de poder.";
        }
        else if (prov == "Microsoft-Windows-WER-SystemErrorReporting" || prov == "BugCheck")
        {
            return PantallaAzul(e);
        }
        else if (prov == "disk" || prov == "Ntfs" || prov == "Microsoft-Windows-Ntfs" || prov == "volmgr" || prov == "stornvme" || prov == "storahci" || prov == "iaStorA")
        {
            p.Tipo = "Disco"; p.Gravedad = 3;
            p.Titulo = "Hubo un problema leyendo o escribiendo en el disco";
            p.Consejo = "Haz respaldo de tus cosas importantes. Si se repite, revisa el disco (chkdsk) o puede que esté fallando.";
        }
        else if (prov == "Microsoft-Windows-WindowsUpdateClient")
        {
            p.Tipo = "Actualización"; p.Gravedad = 2;
            p.Titulo = "Una actualización de Windows no se pudo instalar";
            p.Consejo = "Reinicia y vuelve a buscar actualizaciones. Si sigue, usa Configuración > Sistema > Solucionar problemas > Windows Update.";
        }
        else if (prov == "Service Control Manager")
        {
            p.Tipo = "Servicio"; p.Gravedad = 1;
            p.Titulo = "Un servicio de Windows no arrancó o se detuvo";
            p.Consejo = "Normalmente Windows lo vuelve a intentar solo. Solo preocúpate si algo deja de funcionar.";
        }
        else if (prov.IndexOf("WHEA", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            p.Tipo = "Hardware"; p.Gravedad = 3;
            p.Titulo = "Windows detectó una falla de hardware (procesador, memoria o tarjeta)";
            p.Consejo = "Revisa temperaturas y que no tengas overclock. Si se repite, puede ser una pieza fallando.";
        }
        else if (prov.IndexOf("Display", StringComparison.OrdinalIgnoreCase) >= 0 || prov == "nvlddmkm" || prov == "amdkmdag" || prov == "igfx")
        {
            p.Tipo = "Video"; p.Gravedad = 2;
            p.Titulo = "El driver de video tuvo un problema (la pantalla pudo parpadear o ponerse negra)";
            p.Consejo = "Actualiza el driver de tu tarjeta de video desde la página de NVIDIA, AMD o Intel.";
        }
        else if (prov == "Microsoft-Windows-DistributedCOM" || prov == "DCOM")
        {
            return null;   // Windows lo reporta todo el tiempo y no es nada
        }
        return p;
    }
}

class Ventana : Form
{
    readonly ComboBox cmbDias = new ComboBox();
    readonly Button btnRevisar = new Button();
    readonly CheckBox chkOscuro = new CheckBox();
    readonly Label lblEstado = new Label();
    readonly Label lblVeredicto = new Label();
    readonly FlowLayoutPanel tarjetas = new FlowLayoutPanel();
    readonly Panel grafica = new Panel();
    readonly Label lblFiltro = new Label();
    readonly LinkLabel lnkTodo = new LinkLabel();
    readonly Button btnHoy = new Button();
    readonly ListView lista = new ListView();
    readonly TextBox detalle = new TextBox();
    readonly CheckBox chkMenores = new CheckBox();
    readonly ToolTip tip = new ToolTip();
    List<Problema> problemas = new List<Problema>();
    static readonly Font LetraEncabezado = new Font("Segoe UI Semibold", 9f);   // una sola, no una nueva en cada redibujo
    DateTime? diaFiltro;          // null = todos los días
    int diasRevisados = 7;

    // colores (cambian con el modo oscuro)
    bool oscuro;
    Color Fondo, Tarjeta, Texto, TextoSuave, Borde;
    static readonly Color Rojo = Color.FromArgb(220, 55, 55), Naranja = Color.FromArgb(230, 140, 30), Gris = Color.FromArgb(130, 130, 130), Verde = Color.FromArgb(40, 165, 80), Azul = Color.FromArgb(0, 103, 192);

    public Ventana()
    {
        Text = "Revisor de errores";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Font = new Font("Segoe UI", 9.5f);
        ClientSize = new Size(1000, Math.Min(800, Screen.PrimaryScreen.WorkingArea.Height - 40));
        MinimumSize = new Size(780, 560);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        // ---- barra de arriba ----
        Panel arriba = new Panel { Dock = DockStyle.Top, Height = 64, Tag = "tarjeta" };
        Label titulo = new Label { Text = "Revisor de errores", Font = new Font("Segoe UI Semibold", 16f), AutoSize = true, Location = new Point(16, 14) };
        Label txtDias = new Label { Text = "Revisar los últimos:", AutoSize = true };
        cmbDias.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbDias.Items.AddRange(new object[] { "1 día", "7 días", "30 días", "90 días" });
        cmbDias.SelectedIndex = 1; cmbDias.Width = 90;
        btnRevisar.Text = "Revisar"; btnRevisar.Size = new Size(100, 30);
        btnRevisar.FlatStyle = FlatStyle.Flat; btnRevisar.BackColor = Azul; btnRevisar.ForeColor = Color.White;
        btnRevisar.FlatAppearance.BorderSize = 0; btnRevisar.Tag = "fijo";
        btnRevisar.Click += delegate { Revisar(); };
        cmbDias.SelectedIndexChanged += delegate { if (IsHandleCreated) Revisar(); };
        chkOscuro.Text = "Modo oscuro"; chkOscuro.AutoSize = true;
        chkOscuro.CheckedChanged += delegate { PonerTema(chkOscuro.Checked); GuardarTema(); };
        arriba.Controls.AddRange(new Control[] { titulo, chkOscuro, txtDias, cmbDias, btnRevisar });
        arriba.Resize += delegate
        {
            btnRevisar.Location = new Point(arriba.Width - 16 - btnRevisar.Width, 17);
            cmbDias.Location = new Point(btnRevisar.Left - 10 - cmbDias.Width, 20);
            txtDias.Location = new Point(cmbDias.Left - 6 - txtDias.PreferredWidth, 23);
            chkOscuro.Location = new Point(txtDias.Left - 24 - chkOscuro.PreferredSize.Width, 22);
        };

        // ---- veredicto + tarjetas ----
        Panel resumen = new Panel { Dock = DockStyle.Top, Height = 128, Padding = new Padding(16, 10, 16, 4) };
        lblVeredicto.Dock = DockStyle.Top; lblVeredicto.Height = 34; lblVeredicto.Tag = "fijo";
        lblVeredicto.Font = new Font("Segoe UI Semibold", 13f);
        tarjetas.Dock = DockStyle.Fill; tarjetas.WrapContents = false; tarjetas.AutoScroll = true;
        resumen.Controls.Add(tarjetas);
        resumen.Controls.Add(lblVeredicto);

        // ---- gráfica por día ----
        Panel marcoGrafica = new Panel { Dock = DockStyle.Top, Height = 178, Padding = new Padding(16, 4, 16, 6) };
        grafica.Dock = DockStyle.Fill; grafica.Tag = "tarjeta";
        typeof(Panel).InvokeMember("DoubleBuffered", System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, grafica, new object[] { true });
        grafica.Paint += PintarGrafica;
        grafica.MouseClick += ClicGrafica;
        grafica.MouseMove += MoverGrafica;
        grafica.Cursor = Cursors.Hand;
        marcoGrafica.Controls.Add(grafica);

        // ---- filtro + lista ----
        Panel centro = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 0) };
        Panel barraFiltro = new Panel { Dock = DockStyle.Top, Height = 34 };
        btnHoy.Text = "Solo hoy"; btnHoy.Size = new Size(90, 28); btnHoy.Location = new Point(0, 3);
        btnHoy.FlatStyle = FlatStyle.Flat; btnHoy.FlatAppearance.BorderSize = 1;
        btnHoy.Click += delegate { diaFiltro = DateTime.Today; Llenar(); };
        lblFiltro.AutoSize = true; lblFiltro.Location = new Point(100, 9);
        lnkTodo.Text = "Ver todos los días"; lnkTodo.AutoSize = true; lnkTodo.Tag = "fijo";
        lnkTodo.LinkClicked += delegate { diaFiltro = null; Llenar(); };
        chkMenores.Text = "Mostrar también los errores menores"; chkMenores.AutoSize = true;
        chkMenores.CheckedChanged += delegate { Llenar(); grafica.Invalidate(); };
        barraFiltro.Controls.AddRange(new Control[] { btnHoy, lblFiltro, lnkTodo, chkMenores });
        barraFiltro.Resize += delegate { chkMenores.Location = new Point(barraFiltro.Width - chkMenores.PreferredSize.Width, 7); };
        lista.View = View.Details; lista.FullRowSelect = true; lista.HideSelection = false;
        lista.Dock = DockStyle.Fill; lista.BorderStyle = BorderStyle.FixedSingle;
        lista.Columns.Add("Fecha", 140); lista.Columns.Add("Tipo", 150); lista.Columns.Add("Qué pasó", 560);
        lista.SelectedIndexChanged += delegate { MostrarDetalle(); };
        lista.MultiSelect = true;
        lista.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) { Eliminar(Seleccionados()); e.Handled = true; } };
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("Eliminar", null, delegate { Eliminar(Seleccionados()); });
        menu.Items.Add("Eliminar todos los de este tipo", null, delegate
        {
            var sel = Seleccionados(); if (sel.Count == 0) return;
            string tipo = sel[0].Tipo;
            Eliminar(problemas.Where(p => p.Tipo == tipo).ToList());
        });
        menu.Items.Add("Eliminar todos los que se ven en la lista", null, delegate
        {
            Eliminar(lista.Items.Cast<ListViewItem>().Select(it => (Problema)it.Tag).ToList());
        });
        lista.ContextMenuStrip = menu;
        // el encabezado se dibuja a mano para que se lea bien en claro y en oscuro
        lista.OwnerDraw = true;
        // las filas también a mano: al seleccionarlas el fondo se pone azul clarito pero el texto
        // conserva SU color (rojo NO ESCAPE, azul pantallazo, amarillo Error...) en vez de ponerse negro
        lista.DrawItem += (s, e) => e.DrawDefault = false;
        lista.DrawSubItem += (s, e) =>
        {
            bool sel = e.Item.Selected;
            Color fondo = sel ? (oscuro ? Color.FromArgb(46, 66, 96) : Color.FromArgb(204, 228, 252)) : lista.BackColor;
            using (Brush b = new SolidBrush(fondo)) e.Graphics.FillRectangle(b, e.Bounds);
            if (sel && e.ColumnIndex == 0)   // rayita a la izquierda para que se note cuál está seleccionada
                using (Brush b = new SolidBrush(Color.FromArgb(0, 103, 192))) e.Graphics.FillRectangle(b, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, lista.Font,
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), e.Item.ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        };
        lista.DrawColumnHeader += (s, e) =>
        {
            using (Brush fondo = new SolidBrush(oscuro ? Color.FromArgb(44, 48, 55) : Color.FromArgb(238, 240, 243)))
                e.Graphics.FillRectangle(fondo, e.Bounds);
            using (Pen linea = new Pen(Borde))
            {
                e.Graphics.DrawLine(linea, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
                e.Graphics.DrawLine(linea, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
            TextRenderer.DrawText(e.Graphics, e.Header.Text, LetraEncabezado, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height),
                Texto, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };
        lista.Resize += delegate { AjustarColumnas(); };
        centro.Controls.Add(lista);
        centro.Controls.Add(barraFiltro);

        // ---- detalle + botones ----
        Panel abajo = new Panel { Dock = DockStyle.Bottom, Height = 132, Padding = new Padding(16, 8, 16, 10) };
        detalle.Multiline = true; detalle.ReadOnly = true; detalle.ScrollBars = ScrollBars.Vertical;
        detalle.Dock = DockStyle.Fill; detalle.Font = new Font("Segoe UI", 9.5f);
        FlowLayoutPanel botones = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 6, 0, 0) };
        botones.Controls.Add(Boton("Copiar este error", delegate { Copiar(detalle.Text); }));
        botones.Controls.Add(Boton("Copiar todos", delegate { CopiarTodos(); }));
        botones.Controls.Add(Boton("Eliminar", delegate { Eliminar(Seleccionados()); }));
        btnRecuperar = Boton("Recuperar eliminados", delegate { Recuperar(); });
        botones.Controls.Add(btnRecuperar);
        botones.Controls.Add(Boton("Abrir Visor de eventos", delegate { Abrir("eventvwr.msc", null, false); }));
        botones.Controls.Add(Boton("Historial de confiabilidad", delegate { Abrir("perfmon.exe", "/rel", false); }));
        botones.Controls.Add(Boton("Reparar archivos de Windows", delegate { Reparar(); }));
        lblEstado.Dock = DockStyle.Bottom; lblEstado.Height = 22;
        abajo.Controls.Add(detalle);
        abajo.Controls.Add(botones);
        abajo.Controls.Add(lblEstado);

        Controls.Add(centro);
        Controls.Add(abajo);
        Controls.Add(marcoGrafica);
        Controls.Add(resumen);
        Controls.Add(arriba);

        oscuro = LeerTema();
        chkOscuro.Checked = oscuro;   // (esto llama a PonerTema)
        PonerTema(oscuro);
        Shown += delegate { Revisar(); };
    }

    // ---------- tema claro / oscuro ----------
    static bool LeerTema()
    {
        try { using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\RevisorErrores")) return k != null && Convert.ToInt32(k.GetValue("Oscuro", 0)) == 1; }
        catch { return false; }
    }
    void GuardarTema()
    {
        try { using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\RevisorErrores")) k.SetValue("Oscuro", oscuro ? 1 : 0); } catch { }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int valor, int tam);

    void PonerTema(bool o)
    {
        oscuro = o;
        Fondo = o ? Color.FromArgb(24, 26, 30) : Color.FromArgb(245, 246, 248);
        Tarjeta = o ? Color.FromArgb(36, 39, 45) : Color.White;
        Texto = o ? Color.FromArgb(232, 232, 232) : Color.FromArgb(30, 30, 30);
        TextoSuave = o ? Color.FromArgb(160, 160, 165) : Color.FromArgb(110, 110, 110);
        Borde = o ? Color.FromArgb(60, 64, 72) : Color.FromArgb(215, 218, 222);
        BackColor = Fondo; ForeColor = Texto;
        Pintar(this);
        lista.BackColor = Tarjeta; lista.ForeColor = Texto;
        detalle.BackColor = Tarjeta; detalle.ForeColor = Texto;
        cmbDias.BackColor = Tarjeta; cmbDias.ForeColor = Texto;
        cmbDias.FlatStyle = o ? FlatStyle.Flat : FlatStyle.Standard;
        lblEstado.ForeColor = TextoSuave; lblFiltro.ForeColor = TextoSuave;
        lnkTodo.LinkColor = o ? Color.FromArgb(110, 170, 255) : Azul;
        // barra de título oscura (Windows 10/11)
        if (IsHandleCreated) BarraTitulo();
        else HandleCreated += delegate { BarraTitulo(); };
        if (problemas.Count > 0) Llenar();
        grafica.Invalidate();
    }
    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr h, string app, string id);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);

    void BarraTitulo()
    {
        int v = oscuro ? 1 : 0;
        try { DwmSetWindowAttribute(Handle, 20, ref v, 4); DwmSetWindowAttribute(Handle, 19, ref v, 4); } catch { }
        // encabezado de la lista y barras de desplazamiento también oscuras
        try
        {
            SetWindowTheme(lista.Handle, oscuro ? "DarkMode_Explorer" : "Explorer", null);
            SetWindowTheme(detalle.Handle, oscuro ? "DarkMode_Explorer" : "Explorer", null);
            lista.Invalidate(); detalle.Invalidate();
        }
        catch { }
    }
    void Pintar(Control padre)
    {
        foreach (Control c in padre.Controls)
        {
            if ((c.Tag as string) == "fijo") { Pintar(c); continue; }
            if ((c.Tag as string) == "tarjeta") c.BackColor = Tarjeta;
            else if (c is Panel || c is FlowLayoutPanel || c is Label || c is CheckBox || c is LinkLabel) c.BackColor = c.Parent is Panel && (c.Parent.Tag as string) == "tarjeta" ? Tarjeta : Fondo;
            if (c is Button)
            {
                Button b = (Button)c;
                b.FlatStyle = FlatStyle.Flat; b.BackColor = Tarjeta; b.ForeColor = Texto; b.FlatAppearance.BorderColor = Borde;
            }
            else c.ForeColor = Texto;
            Pintar(c);
        }
    }

    Button Boton(string t, EventHandler h)
    {
        Button b = new Button { Text = t, AutoSize = true, Height = 30, Margin = new Padding(0, 0, 8, 0) };
        b.Click += h;
        return b;
    }

    int Dias() { return new[] { 1, 7, 30, 90 }[Math.Max(0, cmbDias.SelectedIndex)]; }

    void Revisar()
    {
        btnRevisar.Enabled = false; cmbDias.Enabled = false;
        lblVeredicto.Text = "Revisando..."; lblVeredicto.ForeColor = TextoSuave;
        lista.Items.Clear(); detalle.Text = "";
        int dias = Dias();
        Thread t = new Thread(delegate ()
        {
            List<Problema> r = Revisor.Leer(dias, s => EnVentana(delegate { lblEstado.Text = s; }));
            EnVentana(delegate
            {
                problemas = r.Where(p => p.Clave == null || !eliminados.Contains(p.Clave)).ToList();
                diasRevisados = dias;
                ActualizarBotonRecuperar();
                if (diaFiltro.HasValue && diaFiltro.Value < DateTime.Today.AddDays(-dias + 1)) diaFiltro = null;
                btnRevisar.Enabled = true; cmbDias.Enabled = true;
                // el total sin contar los que eliminaste
                lblEstado.Text = "Revisado: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "   (" + problemas.Count + " errores en total)";
                Llenar();
            });
        });
        t.IsBackground = true; t.Start();
    }

    // el hilo de fondo solo toca la ventana si sigue abierta (si la cerraste a media revisión, no pasa nada)
    void EnVentana(Action a)
    {
        try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    // copiar al portapapeles sin tronar si otro programa lo está usando en ese momento
    void Copiar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        for (int intento = 0; intento < 5; intento++)
        {
            try { Clipboard.SetText(texto); return; }
            catch (System.Runtime.InteropServices.ExternalException) { Thread.Sleep(100); }
        }
        MessageBox.Show("Otro programa está usando el portapapeles. Inténtalo otra vez.", "Copiar", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    bool Visible(Problema p) { return p.Gravedad > 1 || chkMenores.Checked; }

    void Llenar()
    {
        // tarjetas por tipo (de todo el periodo revisado)
        tarjetas.Controls.Clear();
        string[] orden = { "Pantalla azul", "NO ESCAPE", "Error", "Apagón de golpe", "Disco", "Hardware", "App que se cerró sola", "App que se trabó", "Video", "Actualización", "Servicio", "Otro error" };
        foreach (string tipo in orden)
        {
            int n = problemas.Count(p => p.Tipo == tipo);
            if (n == 0) continue;
            Problema primero = problemas.First(p => p.Tipo == tipo);
            int grav = problemas.Where(p => p.Tipo == tipo).Max(p => p.Gravedad);
            tarjetas.Controls.Add(HacerTarjeta(tipo, n, primero.ColorPropio.IsEmpty ? ColorDe(grav) : primero.ColorPropio));
        }
        if (tarjetas.Controls.Count == 0) tarjetas.Controls.Add(HacerTarjeta("Sin errores", 0, Verde));

        int graves = problemas.Count(p => p.Gravedad == 3), importantes = problemas.Count(p => p.Gravedad == 2);
        if (graves > 0) { lblVeredicto.Text = "⚠ Hay " + graves + " problema(s) grave(s) que conviene revisar"; lblVeredicto.ForeColor = Rojo; }
        else if (importantes > 0) { lblVeredicto.Text = "Tu compu está bien, pero hay " + importantes + " cosa(s) que fallaron"; lblVeredicto.ForeColor = Naranja; }
        else { lblVeredicto.Text = "✔ Todo bien: no hay errores importantes en " + cmbDias.Text; lblVeredicto.ForeColor = Verde; }

        // filtro por día
        if (diaFiltro.HasValue)
        {
            lblFiltro.Text = "Mostrando: " + (diaFiltro.Value == DateTime.Today ? "hoy" : diaFiltro.Value.ToString("dddd dd/MM"));
            lnkTodo.Visible = true;
            lnkTodo.Location = new Point(lblFiltro.Right + 10, 9);
        }
        else { lblFiltro.Text = "Mostrando: todos los días (toca un día de la gráfica para ver solo ese día)"; lnkTodo.Visible = false; }

        lista.BeginUpdate();
        lista.Items.Clear();
        // hasta arriba: primero las pantallas azules, luego los "NO ESCAPE"; lo demás, del más nuevo al más viejo
        foreach (Problema p in problemas.OrderByDescending(x => x.Tipo == "Pantalla azul").ThenByDescending(x => x.Tipo == "NO ESCAPE").ThenByDescending(x => x.Fecha))
        {
            if (!Visible(p)) continue;
            if (diaFiltro.HasValue && p.Fecha.Date != diaFiltro.Value) continue;
            ListViewItem it = new ListViewItem(new[] { p.Fecha.ToString("dd/MM/yyyy HH:mm"), p.Tipo, p.Titulo });
            it.ForeColor = !p.ColorPropio.IsEmpty ? p.ColorPropio : p.Gravedad == 1 ? TextoSuave : ColorDe(p.Gravedad);
            it.Tag = p;
            lista.Items.Add(it);
        }
        lista.EndUpdate();
        AjustarColumnas();
        if (lista.Items.Count > 0) lista.Items[0].Selected = true;
        else detalle.Text = diaFiltro.HasValue ? "Ese día no hubo errores (de los que se muestran)."
                          : chkMenores.Checked ? "No hay errores en este tiempo." : "No hay errores importantes. (Marca la casilla para ver también los menores.)";
        grafica.Invalidate();
    }

    // la última columna llena justo el ancho que queda (con o sin barra de desplazamiento)
    void AjustarColumnas()
    {
        if (!lista.IsHandleCreated || lista.Columns.Count < 3) return;
        lista.Columns[2].Width = Math.Max(200, lista.ClientSize.Width - lista.Columns[0].Width - lista.Columns[1].Width);
    }

    Color ColorDe(int g) { return g == 3 ? Rojo : g == 2 ? Naranja : Gris; }

    Control HacerTarjeta(string titulo, int n, Color c)
    {
        Panel p = new Panel { Width = 150, Height = 74, BackColor = Tarjeta, Margin = new Padding(0, 0, 10, 0) };
        Panel barra = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = c };
        Label num = new Label { Text = n == 0 ? "✔" : n.ToString(), Font = new Font("Segoe UI Semibold", 17f), ForeColor = c, BackColor = Tarjeta, Location = new Point(12, 4), AutoSize = true };
        Label txt = new Label { Text = titulo, Location = new Point(12, 40), Size = new Size(134, 32), ForeColor = Texto, BackColor = Tarjeta };
        p.Controls.Add(num); p.Controls.Add(txt); p.Controls.Add(barra);
        return p;
    }

    // ---------- gráfica como el "Historial de confiabilidad" de Windows ----------
    // Una columna por día y 5 filas: errores de aplicación, de Windows, varios, advertencias e información,
    // con el icono de Windows en cada cuadrito donde hubo algo. El día elegido se pinta de azul.
    static readonly string[] Filas = { "Errores de aplicación", "Errores de Windows", "Errores varios", "Advertencias", "Información" };
    static int FilaDe(Problema p)
    {
        switch (p.Tipo)
        {
            case "App que se cerró sola": case "App que se trabó": case "NO ESCAPE": case "Error": return 0;
            case "Pantalla azul": case "Apagón de golpe": case "Disco": case "Hardware": return 1;
            case "Actualización": return 3;
            case "Actualización instalada": return 4;
            default: return 2;
        }
    }

    // iconos de error / advertencia / información del propio Windows
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    struct SII { public uint cbSize; public IntPtr hIcon; public int iSysImageIndex; public int iIcon; [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)] public string szPath; }
    [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern int SHGetStockIconInfo(uint id, uint flags, ref SII info);
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int SHDefExtractIcon(string file, int index, uint flags, out IntPtr grande, out IntPtr chico, uint tam);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    static Bitmap IconoWindows(uint siid, int tam)
    {
        try
        {
            SII s = new SII(); s.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(SII));
            SHGetStockIconInfo(siid, 0, ref s);
            IntPtr g, c;
            if (SHDefExtractIcon(s.szPath, s.iIcon, 0, out g, out c, (uint)tam) == 0 && g != IntPtr.Zero)
            {
                Bitmap b;
                using (Icon ic = Icon.FromHandle(g)) b = new Bitmap(ic.ToBitmap());
                DestroyIcon(g); if (c != IntPtr.Zero) DestroyIcon(c);
                return b;
            }
        }
        catch { }
        return (siid == 80 ? SystemIcons.Error : siid == 78 ? SystemIcons.Warning : SystemIcons.Information).ToBitmap();
    }
    // iconos clásicos de Windows 10 (vienen en user32.dll: 1 = advertencia, 3 = error, 4 = información, 6 = escudo)
    public static Bitmap IconoClasico(int indice, int tam)
    {
        try
        {
            IntPtr g, c;
            string user32 = System.IO.Path.Combine(Environment.SystemDirectory, "user32.dll");
            if (SHDefExtractIcon(user32, indice, 0, out g, out c, (uint)tam) == 0 && g != IntPtr.Zero)
            {
                Bitmap b;
                using (Icon ic = Icon.FromHandle(g)) b = new Bitmap(ic.ToBitmap());
                DestroyIcon(g); if (c != IntPtr.Zero) DestroyIcon(c);
                return b;
            }
        }
        catch { }
        return (indice == 6 ? SystemIcons.Shield : SystemIcons.Warning).ToBitmap();
    }
    Bitmap icoError, icoAdvertencia, icoInfo;

    Rectangle areaRejilla;     // la parte con los cuadritos
    int colsVisibles = 7;      // cuántos días caben
    int anchoCol = 24;
    DateTime inicioVisible = DateTime.Today;
    int DiasGrafica() { return Math.Max(diasRevisados, 7); }

    void PintarGrafica(object s, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Tarjeta);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if (icoError == null) { icoError = IconoWindows(80, 32); icoAdvertencia = IconoClasico(1, 32); icoInfo = IconoWindows(79, 32); }
        using (Font f = new Font("Segoe UI", 8.5f))
        using (Font ft = new Font("Segoe UI Semibold", 9.5f))
        using (Brush tx = new SolidBrush(Texto))
        using (Brush txs = new SolidBrush(TextoSuave))
        {
            g.DrawString("Historial por día", ft, tx, 10, 6);
            int altoFila = 20, arriba = 28, anchoEtiquetas = 150;
            int anchoDisp = grafica.Width - 50 - anchoEtiquetas;
            int dias = DiasGrafica();
            anchoCol = Math.Max(24, Math.Min(46, anchoDisp / dias));
            colsVisibles = Math.Max(1, Math.Min(dias, anchoDisp / anchoCol));
            inicioVisible = DateTime.Today.AddDays(-colsVisibles + 1);
            areaRejilla = new Rectangle(46, arriba, colsVisibles * anchoCol, altoFila * Filas.Length);
            if (grafica.Height < arriba + areaRejilla.Height + 20) return;

            // cuántos hay de cada fila en cada día
            int[,] cuenta = new int[colsVisibles, Filas.Length];
            foreach (Problema p in problemas)
            {
                int d = (int)(p.Fecha.Date - inicioVisible).TotalDays;
                if (d >= 0 && d < colsVisibles) cuenta[d, FilaDe(p)]++;
            }

            Color linea = oscuro ? Color.FromArgb(70, 74, 82) : Color.FromArgb(205, 208, 212);
            Color finde = oscuro ? Color.FromArgb(44, 47, 54) : Color.FromArgb(236, 237, 239);
            Color elegido = oscuro ? Color.FromArgb(40, 70, 110) : Color.FromArgb(200, 225, 248);
            for (int d = 0; d < colsVisibles; d++)
            {
                DateTime dia = inicioVisible.AddDays(d);
                int x = areaRejilla.Left + d * anchoCol;
                // fondo: azul el día elegido, gris los fines de semana
                Color? fondo = diaFiltro == dia ? elegido : (dia.DayOfWeek == DayOfWeek.Saturday || dia.DayOfWeek == DayOfWeek.Sunday) ? finde : (Color?)null;
                if (fondo.HasValue) using (Brush b = new SolidBrush(fondo.Value)) g.FillRectangle(b, x, areaRejilla.Top, anchoCol, areaRejilla.Height);
                // días que NO se revisaron (por ejemplo con "1 día"): rayados en gris, para que no parezca "todo bien"
                if (NoRevisado(dia))
                    using (Brush b = new System.Drawing.Drawing2D.HatchBrush(System.Drawing.Drawing2D.HatchStyle.BackwardDiagonal, linea, Color.Transparent))
                        g.FillRectangle(b, x, areaRejilla.Top, anchoCol, areaRejilla.Height);
                for (int fi = 0; fi < Filas.Length; fi++)
                {
                    if (cuenta[d, fi] == 0) continue;
                    Bitmap ico = fi <= 2 ? icoError : fi == 3 ? icoAdvertencia : icoInfo;
                    int tam = 16;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(ico, x + (anchoCol - tam) / 2, areaRejilla.Top + fi * altoFila + (altoFila - tam) / 2, tam, tam);
                }
                // fecha abajo, inclinada (como en Windows)
                int cada = anchoCol >= 30 ? 1 : 2;
                if ((colsVisibles - 1 - d) % cada == 0)
                {
                    System.Drawing.Drawing2D.GraphicsState st = g.Save();
                    g.TranslateTransform(x + anchoCol / 2f + 6, areaRejilla.Bottom + 4);
                    g.RotateTransform(-40);
                    string et = dia == DateTime.Today ? "hoy" : dia.ToString("dd/MM");
                    SizeF sz = g.MeasureString(et, f);
                    g.DrawString(et, f, txs, -sz.Width, 0);
                    g.Restore(st);
                }
            }
            // rejilla
            using (Pen p = new Pen(linea))
            {
                for (int fi = 0; fi <= Filas.Length; fi++) g.DrawLine(p, areaRejilla.Left, areaRejilla.Top + fi * altoFila, areaRejilla.Right, areaRejilla.Top + fi * altoFila);
                for (int d = 0; d <= colsVisibles; d++) g.DrawLine(p, areaRejilla.Left + d * anchoCol, areaRejilla.Top, areaRejilla.Left + d * anchoCol, areaRejilla.Bottom);
            }
            // nombres de las filas a la derecha
            for (int fi = 0; fi < Filas.Length; fi++)
                g.DrawString(Filas[fi], f, tx, areaRejilla.Right + 8, areaRejilla.Top + fi * altoFila + 4);
        }
    }

    // "N días" son las últimas N×24 horas: un día está fuera solo si terminó antes de ese momento
    bool NoRevisado(DateTime dia) { return dia.AddDays(1) <= DateTime.Now.AddDays(-diasRevisados); }

    int DiaEn(Point p)
    {
        if (p.X < areaRejilla.Left || p.X >= areaRejilla.Right || p.Y < areaRejilla.Top || p.Y > areaRejilla.Bottom + 50) return -1;
        int d = (p.X - areaRejilla.Left) / Math.Max(1, anchoCol);
        return d >= 0 && d < colsVisibles ? d : -1;
    }

    void ClicGrafica(object s, MouseEventArgs e)
    {
        int d = DiaEn(e.Location);
        if (d < 0) return;
        DateTime dia = inicioVisible.AddDays(d);
        if (NoRevisado(dia)) return;   // ese día no se revisó: no hay nada que mostrar
        diaFiltro = diaFiltro == dia ? (DateTime?)null : dia;   // tocar el mismo día otra vez = ver todos
        Llenar();
    }

    int ultimoTip = -2;
    void MoverGrafica(object s, MouseEventArgs e)
    {
        int d = DiaEn(e.Location);
        if (d == ultimoTip) return;
        ultimoTip = d;
        if (d < 0) { tip.Hide(grafica); return; }
        DateTime dia = inicioVisible.AddDays(d);
        var delDia = problemas.Where(p => p.Fecha.Date == dia).ToList();
        string t = (dia == DateTime.Today ? "Hoy" : dia.ToString("dddd dd/MM")) + ":";
        if (NoRevisado(dia)) { tip.Show(t + " no se revisó (elige más días arriba)", grafica, e.X + 12, e.Y + 12, 4000); return; }
        if (delDia.Count == 0) t += " todo bien";
        for (int fi = 0; fi < Filas.Length; fi++)
        {
            int n = delDia.Count(p => FilaDe(p) == fi);
            if (n > 0) t += "\n" + Filas[fi] + ": " + n;
        }
        tip.Show(t, grafica, e.X + 12, e.Y + 12, 4000);
    }
    // ---------- eliminar errores del Revisor ----------
    // Windows no deja borrar errores sueltos de sus registros, así que el Revisor guarda cuáles
    // eliminaste (en %LOCALAPPDATA%\RevisorErrores\eliminados.txt) y ya no los vuelve a mostrar.
    // Con "Recuperar eliminados" vuelven todos.
    static string RutaEliminados
    {
        get { return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevisorErrores", "eliminados.txt"); }
    }
    readonly HashSet<string> eliminados = CargarEliminados();
    Button btnRecuperar;

    static HashSet<string> CargarEliminados()
    {
        try { if (System.IO.File.Exists(RutaEliminados)) return new HashSet<string>(System.IO.File.ReadAllLines(RutaEliminados, System.Text.Encoding.UTF8).Where(x => x.Length > 0)); }
        catch { }
        return new HashSet<string>();
    }
    void GuardarEliminados()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(RutaEliminados));
            System.IO.File.WriteAllLines(RutaEliminados, eliminados.ToArray(), System.Text.Encoding.UTF8);
        }
        catch { }
    }

    List<Problema> Seleccionados()
    {
        return lista.SelectedItems.Cast<ListViewItem>().Select(it => (Problema)it.Tag).ToList();
    }

    void Eliminar(List<Problema> cuales)
    {
        cuales = cuales.Where(p => p.Clave != null).ToList();
        if (cuales.Count == 0) return;
        var secretos = cuales.Where(p => p.Clave.StartsWith("secret#")).ToList();
        var normales = cuales.Where(p => !p.Clave.StartsWith("secret#")).ToList();
        if (MessageBox.Show("¿Eliminar " + cuales.Count + " error(es) del Revisor?", "Eliminar errores",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        if (normales.Count > 0)
        {
            foreach (Problema p in normales) eliminados.Add(p.Clave);
            GuardarEliminados();
        }
        Action terminar = delegate
        {
            if (secretos.Count > 0) { BorrarDeBitacora(secretos); Revisar(); }   // se vuelve a leer (las líneas cambiaron)
            else
            {
                var quitar = new HashSet<string>(normales.Select(p => p.Clave));
                problemas = problemas.Where(p => p.Clave == null || !quitar.Contains(p.Clave)).ToList();
                Llenar();
            }
            ActualizarBotonRecuperar();
        };
        var noEscape = secretos.Where(p => p.Tipo == "NO ESCAPE").ToList();
        if (noEscape.Count > 0) Esfumar(noEscape, terminar); else terminar();
    }

    // los "NO ESCAPE" se van desvaneciendo en la lista (1 segundo) antes de desaparecer
    void Esfumar(List<Problema> cuales, Action alTerminar)
    {
        var claves = new HashSet<string>(cuales.Select(p => p.Clave));
        var filas = lista.Items.Cast<ListViewItem>().Where(it => claves.Contains(((Problema)it.Tag).Clave)).ToList();
        if (filas.Count == 0) { alTerminar(); return; }
        var desde = filas.Select(it => it.ForeColor).ToList();
        int paso = 0, pasos = 25;   // 25 pasos de 40 ms = 1 segundo
        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer { Interval = 40 };
        t.Tick += delegate
        {
            paso++;
            float k = Math.Min(1f, paso / (float)pasos);
            Color fondo = lista.BackColor;
            for (int i = 0; i < filas.Count; i++)
            {
                Color c = desde[i];
                filas[i].ForeColor = Color.FromArgb((int)(c.R + (fondo.R - c.R) * k), (int)(c.G + (fondo.G - c.G) * k), (int)(c.B + (fondo.B - c.B) * k));
                filas[i].Selected = false;
            }
            if (paso >= pasos) { t.Stop(); t.Dispose(); alTerminar(); }
        };
        t.Start();
    }

    // borra para siempre esas líneas de %LOCALAPPDATA%\DvdRebote\secreto.log
    static void BorrarDeBitacora(List<Problema> cuales)
    {
        try
        {
            string ruta = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DvdRebote", "secreto.log");
            if (!System.IO.File.Exists(ruta)) return;
            string[] lineas = System.IO.File.ReadAllLines(ruta, System.Text.Encoding.UTF8);
            var borrar = new HashSet<int>();
            foreach (Problema p in cuales)
            {
                // la clave es "secret#<número de línea>#<fecha>": se borra solo si la línea sigue siendo esa
                string[] c = p.Clave.Split('#');
                int i;
                if (c.Length >= 3 && int.TryParse(c[1], out i) && i >= 0 && i < lineas.Length && lineas[i].StartsWith(c[2])) borrar.Add(i);
            }
            var quedan = new List<string>();
            for (int i = 0; i < lineas.Length; i++) if (!borrar.Contains(i)) quedan.Add(lineas[i]);
            System.IO.File.WriteAllLines(ruta, quedan.ToArray(), System.Text.Encoding.UTF8);
        }
        catch { }
    }
    void Recuperar()
    {
        if (eliminados.Count == 0) { MessageBox.Show("No hay errores eliminados.", "Recuperar eliminados", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        DialogResult r = MessageBox.Show("¿Recuperar los " + eliminados.Count + " error(es) que eliminaste?", "Recuperar eliminados",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        eliminados.Clear();
        GuardarEliminados();
        ActualizarBotonRecuperar();
        Revisar();
    }

    void ActualizarBotonRecuperar()
    {
        if (btnRecuperar != null) btnRecuperar.Text = eliminados.Count > 0 ? "Recuperar eliminados (" + eliminados.Count + ")" : "Recuperar eliminados";
    }

    void MostrarDetalle()
    {
        if (lista.SelectedItems.Count == 0) { detalle.Text = ""; return; }   // sin selección: no se queda el texto viejo
        Problema p = (Problema)lista.SelectedItems[0].Tag;
        // los del código secreto no dicen de dónde vienen ni cómo se quitan: solo qué y cuándo;
        // el pantallazo azul además dice la razón (la de la pantalla), NO ESCAPE y Error no
        if (EsSecreto(p))
        {
            string t = p.Titulo + "\r\n" + "Cuándo: " + p.Fecha.ToString("dddd dd/MM/yyyy HH:mm:ss");
            if (p.Tipo == "Pantalla azul")
                t += "\r\nRazón: Tu dispositivo se encontró con NO ESCAPE y tuvo que reiniciarse.\r\nCódigo de detención: NO_ESCAPE";
            detalle.Text = t;
            return;
        }
        detalle.Text =
            p.Titulo + "\r\n" +
            "Cuándo: " + p.Fecha.ToString("dddd dd/MM/yyyy HH:mm:ss") + "\r\n" +
            "Qué hacer: " + p.Consejo + "\r\n\r\n" +
            "Origen: " + p.Origen + "   (número " + p.Id + ")\r\n" +
            "Lo que dice Windows:\r\n" + p.Detalle.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }

    void CopiarTodos()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Revisor de errores - últimos " + cmbDias.Text + (diaFiltro.HasValue ? " (solo " + diaFiltro.Value.ToString("dd/MM") + ")" : ""));
        foreach (ListViewItem it in lista.Items)
        {
            Problema p = (Problema)it.Tag;
            sb.AppendLine(p.Fecha.ToString("dd/MM/yyyy HH:mm") + " | " + p.Tipo + " | " + p.Titulo + (EsSecreto(p) ? "" : " | " + p.Origen + " #" + p.Id));
        }
        Copiar(sb.ToString());
    }

    static bool EsSecreto(Problema p) { return p.Clave != null && p.Clave.StartsWith("secret#"); }

    void Abrir(string exe, string args, bool admin)
    {
        try
        {
            ProcessStartInfo i = new ProcessStartInfo(exe, args ?? "") { UseShellExecute = true };
            if (admin) i.Verb = "runas";
            Process.Start(i);
        }
        catch { }
    }

    void Reparar()
    {
        DialogResult r = MessageBox.Show(
            "Esto abre una ventana de administrador que revisa los archivos de Windows y repara los dañados (sfc /scannow).\n\n" +
            "Tarda varios minutos y Windows te va a pedir permiso. ¿Continuar?",
            "Reparar archivos de Windows", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r == DialogResult.Yes)
            Abrir("cmd.exe", "/k sfc /scannow", true);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Ventana());
    }
}