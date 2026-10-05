namespace MiniDebit;

public sealed class SettingsForm : Form
{
    private readonly TabControl tabs = new();
    public bool MeasurementChanged { get; private set; }
    internal void SelectGraphTab() => tabs.SelectedIndex = 1;
    internal void SelectRatesTab() => tabs.SelectedIndex = 2;

    public SettingsForm(Settings settings, Icon icon)
    {
        Text = "MiniDébit — Paramètres";
        Icon = icon;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(560, 690);
        BackColor = Color.FromArgb(246, 249, 253);
        ForeColor = Color.FromArgb(27, 42, 61);
        Controls.Add(new Label { Text = "Un widget à votre mesure", Font = new Font(Font.FontFamily, 16, FontStyle.Bold), AutoSize = true, Location = new Point(24, 20) });
        Controls.Add(new Label { Text = "Enregistrez pour appliquer vos réglages au widget.", AutoSize = true, Location = new Point(25, 54) });
        tabs.Location = new Point(24, 83);
        tabs.Size = new Size(512, 540);
        var general = new TabPage("Général") { BackColor = BackColor };
        var graph = new TabPage("Graphique") { BackColor = BackColor };
        var ratePage = new TabPage("Débits") { BackColor = BackColor };
        tabs.TabPages.AddRange([general, graph, ratePage]);
        Controls.Add(tabs);
        ComboBox Choice(params object[] values)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            combo.Items.AddRange(values); return combo;
        }
        NumericUpDown Number(decimal min, decimal max, decimal value, decimal step = 1) => new()
            { Minimum = min, Maximum = max, Value = value, Increment = step, Dock = DockStyle.Fill, ThousandsSeparator = true };
        TableLayoutPanel Table(TabPage page, int rows, int rowHeight)
        {
            var table = new TableLayoutPanel { Location = new Point(12, 12), Size = new Size(476, rows * rowHeight), ColumnCount = 2, RowCount = rows };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 195));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
            page.Controls.Add(table); return table;
        }
        void Row(TableLayoutPanel table, int row, string label, Control control)
        {
            table.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            table.Controls.Add(control, 1, row);
        }
        var table = Table(general, 9, 35);
        var adapter = Choice(new AdapterChoice("auto", "Automatique (une carte)"), new AdapterChoice("all", "Toutes les cartes (cumul)"));
        try { adapter.Items.AddRange(NetworkMonitor.Choices().Cast<object>().ToArray()); }
        catch (System.Net.NetworkInformation.NetworkInformationException) { }
        if (!adapter.Items.Cast<AdapterChoice>().Any(a => a.Id == settings.AdapterId)) adapter.Items.Add(new AdapterChoice(settings.AdapterId, "Carte sélectionnée (indisponible)"));
        adapter.SelectedIndex = adapter.Items.Cast<AdapterChoice>().ToList().FindIndex(a => a.Id == settings.AdapterId);
        Row(table, 0, "Carte réseau", adapter);
        var interval = Number(250, 5000, settings.IntervalMs, 250); Row(table, 1, "Mesure toutes les (ms)", interval);
        var history = Number(0.25m, 60, settings.HistorySeconds / 60m, 0.25m); history.DecimalPlaces = 2;
        Row(table, 2, "Période affichée (min)", history);
        var units = Choice("Octets/s : Ko/s, Mo/s", "Bits/s : Mbit/s, Gbit/s");
        units.SelectedIndex = settings.Bits ? 1 : 0; Row(table, 3, "Unités du débit", units);
        var theme = Choice("Sombre", "Clair"); theme.SelectedIndex = settings.Dark ? 0 : 1; Row(table, 4, "Apparence du widget", theme);
        var opacity = Number(35, 100, settings.OpacityPercent, 5); Row(table, 5, "Opacité du widget (%)", opacity);
        var scale = Number(0, 100000, (decimal)settings.FixedScaleMbps); scale.DecimalPlaces = 2;
        Row(table, 6, "Échelle (Mbit/s, 0 = auto)", scale);
        var onTop = new CheckBox { Text = "Garder au premier plan", Checked = settings.AlwaysOnTop, Dock = DockStyle.Fill };
        Row(table, 7, "Affichage", onTop);
        var hidden = new CheckBox { Text = "Dans la zone de notification", Checked = settings.StartHidden, Dock = DockStyle.Fill };
        Row(table, 8, "Au lancement", hidden);
        general.Controls.Add(new Label { Location = new Point(12, 343), Size = new Size(476, 80), ForeColor = Color.FromArgb(94, 115, 141), Text = "La période affichée est indépendante du délai de mesure.\n\nLe cumul peut compter deux fois le trafic d’un VPN.\nPour une mesure précise, choisissez une seule carte réseau." });

        var appearance = settings.Graph;
        var graphTable = Table(graph, 11, 30);
        var down = new ColorField(appearance.DownColor, appearance.Reception);
        var up = new ColorField(appearance.UpColor, appearance.Sending);
        var background = new ColorField(appearance.BackgroundColor, appearance.Background(settings.Dark), true);
        var gridColor = new ColorField(appearance.GridColor, appearance.Grid(settings.Dark), true);
        Row(graphTable, 0, "Couleur de réception", down);
        Row(graphTable, 1, "Couleur d’envoi", up);
        Row(graphTable, 2, "Fond du graphique", background);
        Row(graphTable, 3, "Couleur de la grille", gridColor);
        var thickness = Number(.5m, 6, (decimal)appearance.LineWidth, .1m); thickness.DecimalPlaces = 1;
        Row(graphTable, 4, "Épaisseur du trait (px)", thickness);
        var lineStyle = Choice("Continu", "Pointillé", "Tirets"); lineStyle.SelectedIndex = appearance.LineStyle;
        Row(graphTable, 5, "Style des courbes", lineStyle);
        var showGrid = new CheckBox { Text = "Afficher la grille", Checked = appearance.ShowGrid, Dock = DockStyle.Fill };
        Row(graphTable, 6, "Grille", showGrid);
        var gridStyle = Choice("Continu", "Pointillé", "Tirets"); gridStyle.SelectedIndex = appearance.GridStyle;
        Row(graphTable, 7, "Style de la grille", gridStyle);
        var fill = new CheckBox { Text = "Colorer sous les courbes", Checked = appearance.FillArea, Dock = DockStyle.Fill };
        Row(graphTable, 8, "Remplissage", fill);
        var fillOpacity = Number(0, 100, appearance.FillOpacityPercent, 1);
        Row(graphTable, 9, "Opacité du remplissage (%)", fillOpacity);
        var showMaximum = new CheckBox { Text = "Ligne et valeur du maximum", Checked = appearance.ShowMaximum, Dock = DockStyle.Fill };
        Row(graphTable, 10, "Maximum de la période", showMaximum);
        GraphAppearance ReadAppearance() => new()
        {
            DownColor = down.Value, UpColor = up.Value, BackgroundColor = background.Value, GridColor = gridColor.Value,
            LineWidth = (double)thickness.Value, LineStyle = lineStyle.SelectedIndex, ShowGrid = showGrid.Checked,
            GridStyle = gridStyle.SelectedIndex, FillArea = fill.Checked, FillOpacityPercent = (int)fillOpacity.Value, ShowMaximum = showMaximum.Checked
        };
        graph.Controls.Add(new Label { Text = "Aperçu · données d’exemple", Location = new Point(12, 350), AutoSize = true });
        var preview = new GraphPreview(ReadAppearance, () => theme.SelectedIndex == 0) { Location = new Point(12, 374), Size = new Size(476, 90) };
        graph.Controls.Add(preview);
        var rateTable = Table(ratePage, 4, 40);
        var rateBackground = new ColorField(settings.Rates.BackgroundColor, settings.Rates.Background(settings.Graph, settings.Dark), true, "Fond graphique");
        var rateText = new ColorField(settings.Rates.TextColor, settings.Rates.Text(settings.Graph, settings.Dark), true, "Auto (contraste)");
        var draftFont = new RateAppearance { FontFamily = settings.Rates.FontFamily, FontSizePixels = settings.Rates.FontSizePixels, Style = settings.Rates.Style };
        Row(rateTable, 0, "Fond du bandeau", rateBackground);
        Row(rateTable, 1, "Couleur du texte", rateText);
        var chooseFont = new Button { Text = "Choisir la police…", Dock = DockStyle.Fill };
        Row(rateTable, 2, "Police des débits", chooseFont);
        var compact = new CheckBox { Text = "Réduire les marges verticales", Checked = settings.Rates.Compact, Dock = DockStyle.Fill };
        Row(rateTable, 3, "Bandeau compact", compact);
        var fontDescription = new Label { Location = new Point(12, 187), Size = new Size(476, 30) };
        ratePage.Controls.Add(fontDescription);
        ratePage.Controls.Add(new Label { Location = new Point(12, 232), Size = new Size(476, 62), ForeColor = Color.FromArgb(94, 115, 141),
            Text = "« Fond graphique » reprend automatiquement la couleur du graphique.\nLa police s’applique aux valeurs et aux libellés Réception / Envoi.\nLes flèches gardent les couleurs de leurs courbes." });
        RateAppearance ReadRates() => new()
        {
            BackgroundColor = rateBackground.Value, TextColor = rateText.Value,
            FontFamily = draftFont.FontFamily, FontSizePixels = draftFont.FontSizePixels, Style = draftFont.Style, Compact = compact.Checked
        };
        ratePage.Controls.Add(new Label { Text = "Aperçu · données d’exemple", Location = new Point(12, 313), AutoSize = true });
        var ratePreview = new RatePreview(ReadRates, ReadAppearance, () => theme.SelectedIndex == 0)
            { Location = new Point(12, 339), Size = new Size(476, 48) };
        ratePage.Controls.Add(ratePreview);
        void RefreshRates()
        {
            var graphStyle = ReadAppearance();
            bool dark = theme.SelectedIndex == 0;
            rateBackground.UpdateAutomaticColor(graphStyle.Background(dark));
            rateText.UpdateAutomaticColor(ReadRates().Text(graphStyle, dark));
            fontDescription.Text = $"Police sélectionnée : {draftFont.FontFamily} · {draftFont.FontSizePixels * 72 / 96:0.#} pt";
            using var numbers = draftFont.CreateFont();
            using var captions = draftFont.CreateFont(true);
            using var arrows = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
            ratePreview.Height = (int)Math.Ceiling(RateRenderer.Measure(numbers, captions, arrows, compact.Checked).Height * ratePreview.DeviceDpi / 96f);
            ratePreview.Invalidate();
        }
        rateBackground.Changed += (_, _) => RefreshRates();
        rateText.Changed += (_, _) => RefreshRates();
        compact.CheckedChanged += (_, _) => RefreshRates();
        chooseFont.Click += (_, _) =>
        {
            using var currentFont = draftFont.CreateFont();
            using var picker = new FontDialog { Font = currentFont, MinSize = 6, MaxSize = 18, FontMustExist = true, ShowEffects = true, ShowColor = false };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            draftFont.FontFamily = picker.Font.Name;
            draftFont.FontSizePixels = picker.Font.SizeInPoints * 96 / 72;
            draftFont.Style = (int)picker.Font.Style;
            draftFont.Normalize();
            RefreshRates();
        };
        var resetRates = new Button { Text = "Rétablir le bandeau par défaut", Location = new Point(12, 430), Size = new Size(256, 30) };
        ratePage.Controls.Add(resetRates);
        resetRates.Click += (_, _) =>
        {
            draftFont = new RateAppearance();
            compact.Checked = false;
            rateBackground.Value = ""; rateText.Value = "";
            RefreshRates();
        };
        RefreshRates();
        void RefreshPreview()
        {
            bool dark = theme.SelectedIndex == 0;
            background.UpdateAutomaticColor(new GraphAppearance().Background(dark));
            gridColor.UpdateAutomaticColor(new GraphAppearance().Grid(dark));
            gridStyle.Enabled = gridColor.Enabled = showGrid.Checked;
            fillOpacity.Enabled = fill.Checked;
            preview.Invalidate();
            RefreshRates();
        }
        foreach (var color in new[] { down, up, background, gridColor }) color.Changed += (_, _) => RefreshPreview();
        thickness.ValueChanged += (_, _) => RefreshPreview();
        fillOpacity.ValueChanged += (_, _) => RefreshPreview();
        lineStyle.SelectedIndexChanged += (_, _) => RefreshPreview();
        gridStyle.SelectedIndexChanged += (_, _) => RefreshPreview();
        showGrid.CheckedChanged += (_, _) => RefreshPreview();
        fill.CheckedChanged += (_, _) => RefreshPreview();
        showMaximum.CheckedChanged += (_, _) => RefreshPreview();
        theme.SelectedIndexChanged += (_, _) => RefreshPreview();
        var reset = new Button { Text = "Rétablir l’apparence par défaut", Location = new Point(12, 474), Size = new Size(256, 28) };
        graph.Controls.Add(reset);
        reset.Click += (_, _) =>
        {
            var defaults = new GraphAppearance();
            down.Value = defaults.DownColor; up.Value = defaults.UpColor;
            background.Value = defaults.BackgroundColor; gridColor.Value = defaults.GridColor;
            thickness.Value = (decimal)defaults.LineWidth; lineStyle.SelectedIndex = defaults.LineStyle;
            showGrid.Checked = defaults.ShowGrid; gridStyle.SelectedIndex = defaults.GridStyle;
            fill.Checked = defaults.FillArea; fillOpacity.Value = defaults.FillOpacityPercent;
            showMaximum.Checked = defaults.ShowMaximum;
            RefreshPreview();
        };
        RefreshPreview();
        var save = new Button { Text = "Enregistrer", Location = new Point(402, 639), Size = new Size(134, 34), BackColor = Color.FromArgb(21, 110, 137), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        var cancel = new Button { Text = "Annuler", Location = new Point(288, 639), Size = new Size(104, 34), DialogResult = DialogResult.Cancel };
        Controls.AddRange([save, cancel]);
        AcceptButton = save; CancelButton = cancel;
        save.Click += (_, _) =>
        {
            var id = ((AdapterChoice)adapter.SelectedItem!).Id;
            MeasurementChanged = settings.AdapterId != id;
            settings.AdapterId = id;
            settings.IntervalMs = (int)interval.Value;
            settings.HistorySeconds = (int)Math.Round(history.Value * 60);
            settings.Bits = units.SelectedIndex == 1;
            settings.Dark = theme.SelectedIndex == 0;
            settings.OpacityPercent = (int)opacity.Value;
            settings.FixedScaleMbps = (double)scale.Value;
            settings.AlwaysOnTop = onTop.Checked;
            settings.StartHidden = hidden.Checked;
            settings.Graph = ReadAppearance();
            settings.Rates = ReadRates();
            DialogResult = DialogResult.OK;
        };
    }
}

internal sealed class ColorField : UserControl
{
    private readonly Button button = new() { Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat };
    private readonly CheckBox? automatic;
    private string hex;
    public event EventHandler? Changed;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Value
    {
        get => automatic?.Checked == true ? "" : hex;
        set
        {
            if (!string.IsNullOrEmpty(value)) hex = value;
            if (automatic is not null) automatic.Checked = string.IsNullOrEmpty(value);
            UpdateSwatch(); Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public ColorField(string value, Color fallback, bool allowAutomatic = false, string automaticLabel = "Auto (thème)")
    {
        Dock = DockStyle.Fill;
        hex = string.IsNullOrEmpty(value) ? $"#{fallback.R:X2}{fallback.G:X2}{fallback.B:X2}" : value;
        Controls.Add(button);
        if (allowAutomatic)
        {
            automatic = new CheckBox { Text = automaticLabel, Dock = DockStyle.Right, Width = 134, Checked = string.IsNullOrEmpty(value) };
            automatic.CheckedChanged += (_, _) => { UpdateSwatch(); Changed?.Invoke(this, EventArgs.Empty); };
            Controls.Add(automatic);
        }
        button.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { Color = GraphAppearance.Resolve(hex, fallback), FullOpen = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            Value = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        };
        UpdateSwatch();
    }
    public void UpdateAutomaticColor(Color color)
    {
        if (automatic?.Checked != true) return;
        hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        UpdateSwatch();
    }
    private void UpdateSwatch()
    {
        var color = GraphAppearance.Resolve(hex, Color.Black);
        button.BackColor = color;
        button.ForeColor = color.GetBrightness() > .55 ? Color.Black : Color.White;
        button.Text = hex;
    }
}
