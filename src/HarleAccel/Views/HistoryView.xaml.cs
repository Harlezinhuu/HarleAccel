// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class HistoryView : UserControl, IRefreshable
    {
        public HistoryView()
        {
            InitializeComponent();
        }

        public void Refresh()
        {
            Status.Text = "";
            Build();
        }

        void Build()
        {
            List.Children.Clear();
            var st = AppState.Current;

            if (st.History.Count == 0)
            {
                List.Children.Add(Ui.Text("Nada aqui ainda. Aplique uma curva na tela Curva e ela aparece aqui.", 14, "Muted"));
                return;
            }

            foreach (var entry in st.History)
            {
                bool isActive = st.Applied != null &&
                                RawAccelJson.Build(st.Applied) == RawAccelJson.Build(entry.Profile);

                var card = new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(16, 12, 16, 12) };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                var top = new StackPanel { Orientation = Orientation.Horizontal };
                top.Children.Add(Ui.Text(entry.Date.ToString("dd/MM/yyyy HH:mm"), 12, "Muted"));
                if (isActive) top.Children.Add(Ui.Text("   ● EM USO", 11, "Ok", FontWeights.Bold));
                info.Children.Add(top);
                info.Children.Add(Ui.Text(entry.Title, 14, "Silver", FontWeights.SemiBold));
                info.Children.Add(Ui.Text($"{entry.Profile.Dpi} DPI", 12, "Muted"));
                grid.Children.Add(info);

                var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var restore = new Button { Content = "Restaurar e aplicar", Style = (Style)FindResource("Primary"), Margin = new Thickness(0, 0, 8, 0) };
                restore.Click += async (_, _) =>
                {
                    restore.IsEnabled = false;
                    var p = entry.Profile.Clone();
                    p.Dpi = st.Dpi;
                    var (ok, msg) = await DriverService.ApplyAsync(p);
                    restore.IsEnabled = true;
                    if (ok)
                    {
                        st.Profile = p.Clone();
                        st.Applied = p.Clone();
                        AppState.Save();
                        Build();
                        Ui.SetStatus(Status, "Curva restaurada e aplicada.", true);
                    }
                    else Ui.SetStatus(Status, msg, false);
                };
                var edit = new Button { Content = "Abrir no editor", Margin = new Thickness(0, 0, 8, 0) };
                edit.Click += (_, _) =>
                {
                    st.Profile = entry.Profile.Clone();
                    AppState.Save();
                    MainWindow.Instance?.Navigate("curve");
                };
                var del = new Button { Content = "Excluir", Style = (Style)FindResource("Ghost") };
                del.Click += (_, _) =>
                {
                    st.History.Remove(entry);
                    AppState.Save();
                    Build();
                };
                buttons.Children.Add(restore);
                buttons.Children.Add(edit);
                buttons.Children.Add(del);
                Grid.SetColumn(buttons, 1);
                grid.Children.Add(buttons);

                card.Child = grid;
                List.Children.Add(card);
            }
        }
    }
}
