// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using HarleAccel.Core;

namespace HarleAccel.Views
{
    public partial class CreditsView : UserControl
    {
        public CreditsView()
        {
            InitializeComponent();
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"HarleAccel v{v?.Major}.{v?.Minor}.{v?.Build} · usa Raw Accel {DriverService.Version}";

            try
            {
                using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("RawAccel-LICENSE.txt");
                if (s != null) LicenseText.Text = new StreamReader(s).ReadToEnd();
            }
            catch { LicenseText.Text = "MIT License — Copyright (c) 2020 a1xd"; }
        }

        void Link_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string url })
            {
                SafeLinks.Open(url);
            }
        }
    }
}
